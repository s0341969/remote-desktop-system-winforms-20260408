using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Models;
using RemoteDesktop.Agent.Options;

namespace RemoteDesktop.Agent.Services;

public sealed class FileTransferService
{
    private const int MaxChunkBytes = 16 * 1024;
    private const int MaxChunkBase64Characters = 24_000;
    private const int ProgressPublishThresholdBytes = 256 * 1024;
    private readonly AgentOptions _options;
    private readonly ILogger<FileTransferService> _logger;
    private readonly ConcurrentDictionary<string, UploadSession> _uploads = new(StringComparer.OrdinalIgnoreCase);

    public FileTransferService(IOptions<AgentOptions> options, ILogger<FileTransferService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> TryHandleAsync(
        ViewerCommandMessage command,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        switch (command.Type)
        {
            case "file-upload-start":
                await HandleStartAsync(command, publishStatusAsync, cancellationToken);
                return true;
            case "file-upload-chunk":
                await HandleChunkAsync(command, publishStatusAsync, cancellationToken);
                return true;
            case "file-upload-complete":
                await HandleCompleteAsync(command, publishStatusAsync, cancellationToken);
                return true;
            case "file-upload-abort":
                await HandleAbortAsync(command, publishStatusAsync, cancellationToken);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleStartAsync(
        ViewerCommandMessage command,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        var uploadId = command.UploadId?.Trim() ?? string.Empty;
        var originalFileName = Path.GetFileName(command.FileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(uploadId) || string.IsNullOrWhiteSpace(originalFileName) || command.FileSize < 0)
        {
            await PublishFailedAsync(uploadId, originalFileName, command.FileSize, AgentUiText.Bi("上傳開始資料無效。", "The upload start payload is invalid."), publishStatusAsync, cancellationToken);
            return;
        }

        if (_uploads.ContainsKey(uploadId))
        {
            await PublishFailedAsync(uploadId, originalFileName, command.FileSize, AgentUiText.Bi("此上傳 ID 已被使用。", "The upload id is already in use."), publishStatusAsync, cancellationToken);
            return;
        }

        try
        {
            var transferDirectory = ResolveTransferDirectory();
            Directory.CreateDirectory(transferDirectory);

            var targetPath = ResolveUniqueTargetPath(transferDirectory, originalFileName);
            var tempPath = $"{targetPath}.uploading";
            var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
            var session = new UploadSession(uploadId, originalFileName, Path.GetFileName(targetPath), targetPath, tempPath, command.FileSize, stream);
            if (!_uploads.TryAdd(uploadId, session))
            {
                await stream.DisposeAsync();
                File.Delete(tempPath);
                await PublishFailedAsync(uploadId, originalFileName, command.FileSize, AgentUiText.Bi("此上傳 ID 已被使用。", "The upload id is already in use."), publishStatusAsync, cancellationToken);
                return;
            }

            _logger.LogInformation("Started file upload {UploadId} for {FileName}.", uploadId, originalFileName);
            await publishStatusAsync(new AgentFileTransferStatusMessage
            {
                UploadId = uploadId,
                Status = "started",
                FileName = originalFileName,
                StoredFileName = session.StoredFileName,
                StoredFilePath = session.TargetPath,
                FileSize = command.FileSize,
                BytesTransferred = 0,
                Message = AgentUiText.Bi($"正在接收檔案到 {session.StoredFileName}。", $"Receiving file to {session.StoredFileName}.")
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to start file upload {UploadId}.", uploadId);
            await PublishFailedAsync(uploadId, originalFileName, command.FileSize, AgentUiText.Bi($"無法開始上傳：{exception.Message}", $"Upload could not be started: {exception.Message}"), publishStatusAsync, cancellationToken);
        }
    }

    private async Task HandleChunkAsync(
        ViewerCommandMessage command,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        var uploadId = command.UploadId?.Trim() ?? string.Empty;
        if (!_uploads.TryGetValue(uploadId, out var session))
        {
            await PublishFailedAsync(uploadId, command.FileName ?? string.Empty, command.FileSize, AgentUiText.Bi("找不到上傳工作階段。", "The upload session was not found."), publishStatusAsync, cancellationToken);
            return;
        }

        try
        {
            if (command.SequenceNumber != session.NextSequenceNumber)
            {
                throw new InvalidOperationException(AgentUiText.Bi("檔案分塊順序無效。", "The file chunk sequence is invalid."));
            }

            var chunkBase64 = command.ChunkBase64 ?? string.Empty;
            if (chunkBase64.Length == 0 || chunkBase64.Length > MaxChunkBase64Characters)
            {
                throw new InvalidOperationException(AgentUiText.Bi("檔案分塊大小無效。", "The file chunk size is invalid."));
            }

            var chunk = Convert.FromBase64String(chunkBase64);
            if (chunk.Length == 0 || chunk.Length > MaxChunkBytes)
            {
                throw new InvalidOperationException(AgentUiText.Bi("檔案分塊大小無效。", "The file chunk size is invalid."));
            }

            await session.Stream.WriteAsync(chunk, cancellationToken);
            session.BytesTransferred += chunk.Length;
            session.NextSequenceNumber++;

            if (ShouldPublishProgress(session))
            {
                session.LastPublishedBytesTransferred = session.BytesTransferred;
                await publishStatusAsync(new AgentFileTransferStatusMessage
                {
                    UploadId = session.UploadId,
                    Status = "progress",
                    FileName = session.FileName,
                    StoredFileName = session.StoredFileName,
                    StoredFilePath = session.TargetPath,
                    FileSize = session.FileSize,
                    BytesTransferred = session.BytesTransferred,
                    Message = AgentUiText.Bi($"已接收 {session.BytesTransferred} / {session.FileSize} 位元組。", $"Received {session.BytesTransferred} of {session.FileSize} bytes.")
                }, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to write upload chunk for {UploadId}.", uploadId);
            await FailAndCleanupAsync(session, AgentUiText.Bi($"檔案上傳失敗：{exception.Message}", $"File upload failed: {exception.Message}"), publishStatusAsync, cancellationToken);
        }
    }

    private async Task HandleCompleteAsync(
        ViewerCommandMessage command,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        var uploadId = command.UploadId?.Trim() ?? string.Empty;
        if (!_uploads.TryRemove(uploadId, out var session))
        {
            await PublishFailedAsync(uploadId, command.FileName ?? string.Empty, command.FileSize, AgentUiText.Bi("找不到上傳工作階段。", "The upload session was not found."), publishStatusAsync, cancellationToken);
            return;
        }

        try
        {
            if (session.FileSize > 0 && session.BytesTransferred != session.FileSize)
            {
                throw new InvalidOperationException(AgentUiText.Bi($"預期 {session.FileSize} 位元組，但實際收到 {session.BytesTransferred} 位元組。", $"Expected {session.FileSize} bytes but received {session.BytesTransferred} bytes."));
            }

            await session.Stream.FlushAsync(cancellationToken);
            await session.Stream.DisposeAsync();
            File.Move(session.TempPath, session.TargetPath, overwrite: false);

            _logger.LogInformation("Completed file upload {UploadId} to {TargetPath}.", uploadId, session.TargetPath);
            await publishStatusAsync(new AgentFileTransferStatusMessage
            {
                UploadId = session.UploadId,
                Status = "completed",
                FileName = session.FileName,
                StoredFileName = session.StoredFileName,
                StoredFilePath = session.TargetPath,
                FileSize = session.FileSize,
                BytesTransferred = session.BytesTransferred,
                Message = AgentUiText.Bi($"檔案已儲存到 {session.StoredFileName}。", $"Saved file to {session.StoredFileName}.")
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to finalize upload {UploadId}.", uploadId);
            await FailAndCleanupAsync(session, AgentUiText.Bi($"檔案上傳失敗：{exception.Message}", $"File upload failed: {exception.Message}"), publishStatusAsync, cancellationToken);
        }
    }

    private async Task HandleAbortAsync(
        ViewerCommandMessage command,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        var uploadId = command.UploadId?.Trim() ?? string.Empty;
        if (!_uploads.TryRemove(uploadId, out var session))
        {
            return;
        }

        await CleanupSessionFilesAsync(session);
        await publishStatusAsync(new AgentFileTransferStatusMessage
        {
            UploadId = session.UploadId,
            Status = "failed",
            FileName = session.FileName,
            StoredFileName = session.StoredFileName,
            StoredFilePath = session.TargetPath,
            FileSize = session.FileSize,
            BytesTransferred = session.BytesTransferred,
            Message = AgentUiText.Bi("檔案尚未完成即被取消上傳。", "The file upload was cancelled before completion.")
        }, cancellationToken);
    }

    private async Task FailAndCleanupAsync(
        UploadSession session,
        string message,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        _uploads.TryRemove(session.UploadId, out _);
        await CleanupSessionFilesAsync(session);
        await publishStatusAsync(new AgentFileTransferStatusMessage
        {
            UploadId = session.UploadId,
            Status = "failed",
            FileName = session.FileName,
            StoredFileName = session.StoredFileName,
            StoredFilePath = session.TargetPath,
            FileSize = session.FileSize,
            BytesTransferred = session.BytesTransferred,
            Message = message
        }, cancellationToken);
    }

    private static async Task CleanupSessionFilesAsync(UploadSession session)
    {
        try
        {
            await session.Stream.DisposeAsync();
        }
        catch
        {
        }

        if (File.Exists(session.TempPath))
        {
            try
            {
                File.Delete(session.TempPath);
            }
            catch
            {
            }
        }
    }

    private static async Task PublishFailedAsync(
        string uploadId,
        string fileName,
        long fileSize,
        string message,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        await publishStatusAsync(new AgentFileTransferStatusMessage
        {
            UploadId = uploadId,
            Status = "failed",
            FileName = fileName,
            StoredFileName = string.Empty,
            StoredFilePath = string.Empty,
            FileSize = fileSize,
            BytesTransferred = 0,
            Message = message
        }, cancellationToken);
    }

    private string ResolveTransferDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.FileTransferDirectory))
        {
            return _options.FileTransferDirectory.Trim();
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "RemoteDesktop Transfers");
    }

    private static bool ShouldPublishProgress(UploadSession session)
    {
        if (session.BytesTransferred <= 0)
        {
            return false;
        }

        if (session.FileSize > 0 && session.BytesTransferred >= session.FileSize)
        {
            return false;
        }

        return session.BytesTransferred - session.LastPublishedBytesTransferred >= ProgressPublishThresholdBytes;
    }

    private static string ResolveUniqueTargetPath(string directory, string fileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = Path.Combine(directory, fileName);
        var counter = 1;
        while (File.Exists(candidate) || File.Exists($"{candidate}.uploading"))
        {
            candidate = Path.Combine(directory, $"{nameWithoutExtension} ({counter++}){extension}");
        }

        return candidate;
    }

    private sealed class UploadSession
    {
        public UploadSession(string uploadId, string fileName, string storedFileName, string targetPath, string tempPath, long fileSize, FileStream stream)
        {
            UploadId = uploadId;
            FileName = fileName;
            StoredFileName = storedFileName;
            TargetPath = targetPath;
            TempPath = tempPath;
            FileSize = fileSize;
            Stream = stream;
        }

        public string UploadId { get; }
        public string FileName { get; }
        public string StoredFileName { get; }
        public string TargetPath { get; }
        public string TempPath { get; }
        public long FileSize { get; }
        public FileStream Stream { get; }
        public long BytesTransferred { get; set; }
        public long LastPublishedBytesTransferred { get; set; }
        public int NextSequenceNumber { get; set; }
    }
}

