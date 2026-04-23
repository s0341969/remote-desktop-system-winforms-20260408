using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Compatibility;
using RemoteDesktop.Agent.Models;
using RemoteDesktop.Agent.Options;

namespace RemoteDesktop.Agent.Services;

public sealed class FileTransferService
{
    private const int MaxChunkBytes = 16 * 1024;
    private const int MaxDownloadChunkBytes = 16 * 1024;
    private const int MaxChunkBase64Characters = 24_000;
    private const int ProgressPublishThresholdBytes = 256 * 1024;
    private const int MaxBrowserEntries = 2_000;
    private static readonly FileShare DownloadReadSharing = FileShare.ReadWrite | FileShare.Delete;
    private readonly AgentOptions _options;
    private readonly ILogger<FileTransferService> _logger;
    private readonly FileTransferTraceService _fileTransferTraceService;
    private readonly ConcurrentDictionary<string, UploadSession> _uploads = new(StringComparer.OrdinalIgnoreCase);

    public FileTransferService(IOptions<AgentOptions> options, ILogger<FileTransferService> logger, FileTransferTraceService fileTransferTraceService)
    {
        _options = options.Value;
        _logger = logger;
        _fileTransferTraceService = fileTransferTraceService;
    }

    public async Task<bool> TryHandleAsync(
        ViewerCommandMessage command,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        switch (command.Type)
        {
            case "file-upload-start":
                await LogAsync("agent-upload-command-start", "Received file-upload-start command.", new
                {
                    uploadId = command.UploadId,
                    fileName = command.FileName,
                    fileSize = command.FileSize
                }, cancellationToken);
                await HandleStartAsync(command, publishStatusAsync, cancellationToken);
                return true;
            case "file-upload-chunk":
                await HandleChunkAsync(command, publishStatusAsync, cancellationToken);
                return true;
            case "file-upload-complete":
                await LogAsync("agent-upload-command-complete", "Received file-upload-complete command.", new
                {
                    uploadId = command.UploadId,
                    fileName = command.FileName,
                    fileSize = command.FileSize
                }, cancellationToken);
                await HandleCompleteAsync(command, publishStatusAsync, cancellationToken);
                return true;
            case "file-upload-abort":
                await LogAsync("agent-upload-command-abort", "Received file-upload-abort command.", new
                {
                    uploadId = command.UploadId
                }, cancellationToken);
                await HandleAbortAsync(command, publishStatusAsync, cancellationToken);
                return true;
            case "file-download-start":
                await LogAsync("agent-download-command-start", "Received file-download-start command.", new
                {
                    transferId = command.UploadId,
                    remotePath = command.RemotePath
                }, cancellationToken);
                await HandleDownloadStartAsync(command, publishStatusAsync, cancellationToken);
                return true;
            case "file-browser-list":
                await LogAsync("agent-browser-command-list", "Received file-browser-list command.", new
                {
                    requestId = command.UploadId,
                    directoryPath = command.DirectoryPath
                }, cancellationToken);
                await HandleBrowseDirectoryAsync(command, publishStatusAsync, cancellationToken);
                return true;
            case "file-move":
                await LogAsync("agent-file-move-command", "Received file-move command.", new
                {
                    requestId = command.UploadId,
                    sourcePath = command.RemotePath,
                    destinationPath = command.DestinationPath
                }, cancellationToken);
                await HandleMoveAsync(command, publishStatusAsync, cancellationToken);
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
                await Net48Compat.DisposeAsyncCompat(stream);
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

            await session.Stream.WriteAsync(chunk, 0, chunk.Length, cancellationToken);
            session.BytesTransferred += chunk.Length;
            session.NextSequenceNumber++;

            if (session.NextSequenceNumber == 1 || session.NextSequenceNumber % 32 == 0)
            {
                await LogAsync("agent-upload-command-chunk", "Processed upload chunk.", new
                {
                    uploadId = session.UploadId,
                    sequenceNumber = session.NextSequenceNumber - 1,
                    chunkBytes = chunk.Length,
                    bytesTransferred = session.BytesTransferred,
                    fileSize = session.FileSize
                }, cancellationToken);
            }

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
            await Net48Compat.DisposeAsyncCompat(session.Stream);
            File.Move(session.TempPath, session.TargetPath);
            await LogAsync("agent-upload-saved", "Saved uploaded file to disk.", new
            {
                uploadId = session.UploadId,
                fileName = session.FileName,
                storedFileName = session.StoredFileName,
                storedFilePath = session.TargetPath,
                fileSize = session.FileSize
            }, cancellationToken);

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

    private async Task HandleDownloadStartAsync(
        ViewerCommandMessage command,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        var transferId = command.UploadId?.Trim() ?? string.Empty;
        var remotePath = command.RemotePath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(transferId) || string.IsNullOrWhiteSpace(remotePath))
        {
            await PublishFailedAsync(transferId, string.Empty, 0, AgentUiText.Bi("下載要求無效。", "The download request is invalid."), publishStatusAsync, cancellationToken, direction: "download");
            return;
        }

        try
        {
            var filePath = ResolveDownloadPath(remotePath);
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException(AgentUiText.Bi("指定的下載檔案不存在。", "The requested download file does not exist."), filePath);
            }

            using var stream = OpenDownloadReadStream(fileInfo);
            var fileSize = stream.Length;
            await publishStatusAsync(new AgentFileTransferStatusMessage
            {
                UploadId = transferId,
                Direction = "download",
                Status = "started",
                FileName = fileInfo.Name,
                StoredFileName = fileInfo.Name,
                StoredFilePath = fileInfo.FullName,
                FileSize = fileSize,
                BytesTransferred = 0,
                Message = AgentUiText.Bi($"開始傳送 {fileInfo.Name}。", $"Started sending {fileInfo.Name}.")
            }, cancellationToken);

            var buffer = new byte[MaxDownloadChunkBytes];
            var sequenceNumber = 0;
            long bytesTransferred = 0;
            long lastPublishedBytesTransferred = 0;

            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, MaxDownloadChunkBytes, cancellationToken);
                if (bytesRead <= 0)
                {
                    break;
                }

                bytesTransferred += bytesRead;
                await publishStatusAsync(new AgentFileTransferStatusMessage
                {
                    UploadId = transferId,
                    Direction = "download",
                    Status = "chunk",
                    FileName = fileInfo.Name,
                    StoredFileName = fileInfo.Name,
                    StoredFilePath = fileInfo.FullName,
                    FileSize = fileSize,
                    BytesTransferred = bytesTransferred,
                    SequenceNumber = sequenceNumber++,
                    ChunkBase64 = Convert.ToBase64String(buffer, 0, bytesRead),
                    Message = AgentUiText.Bi($"正在傳送 {fileInfo.Name}。", $"Sending {fileInfo.Name}.")
                }, cancellationToken);

                if (bytesTransferred - lastPublishedBytesTransferred >= ProgressPublishThresholdBytes && bytesTransferred < fileSize)
                {
                    lastPublishedBytesTransferred = bytesTransferred;
                    await publishStatusAsync(new AgentFileTransferStatusMessage
                    {
                        UploadId = transferId,
                        Direction = "download",
                        Status = "progress",
                        FileName = fileInfo.Name,
                        StoredFileName = fileInfo.Name,
                        StoredFilePath = fileInfo.FullName,
                        FileSize = fileSize,
                        BytesTransferred = bytesTransferred,
                        Message = AgentUiText.Bi($"已傳送 {bytesTransferred} / {fileSize} 位元組。", $"Sent {bytesTransferred} of {fileSize} bytes.")
                    }, cancellationToken);
                }
            }

            await publishStatusAsync(new AgentFileTransferStatusMessage
            {
                UploadId = transferId,
                Direction = "download",
                Status = "completed",
                FileName = fileInfo.Name,
                StoredFileName = fileInfo.Name,
                StoredFilePath = fileInfo.FullName,
                FileSize = fileSize,
                BytesTransferred = fileSize,
                Message = AgentUiText.Bi($"檔案 {fileInfo.Name} 已完成傳送。", $"File {fileInfo.Name} was sent successfully.")
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to process download request {TransferId}.", transferId);
            await PublishFailedAsync(
                transferId,
                Path.GetFileName(remotePath),
                0,
                AgentUiText.Bi($"檔案下載失敗：{exception.Message}", $"File download failed: {exception.Message}"),
                publishStatusAsync,
                cancellationToken,
                direction: "download");
        }
    }

    private static FileStream OpenDownloadReadStream(FileInfo fileInfo)
    {
        try
        {
            return new FileStream(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                DownloadReadSharing,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (IOException exception)
        {
            throw new IOException(
                AgentUiText.Bi(
                    "檔案目前被其他程式以獨占方式使用，無法下載。若要下載，請先關閉對方程式或解除檔案鎖定。",
                    "The file is currently locked exclusively by another process and cannot be downloaded. Close the application holding the file or release the lock first."),
                exception);
        }
    }

    private async Task HandleBrowseDirectoryAsync(
        ViewerCommandMessage command,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        var requestId = command.UploadId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requestId))
        {
            await PublishFailedAsync(
                requestId,
                string.Empty,
                0,
                AgentUiText.Bi("遠端檔案總管要求缺少識別碼。", "The remote file browser request is missing an identifier."),
                publishStatusAsync,
                cancellationToken,
                direction: "browse");
            return;
        }

        try
        {
            var directoryPath = ResolveBrowserDirectoryPath(command.DirectoryPath);
            var directoryInfo = new DirectoryInfo(directoryPath);
            if (!directoryInfo.Exists)
            {
                throw new DirectoryNotFoundException(AgentUiText.Bi("指定的遠端資料夾不存在。", "The requested remote directory does not exist."));
            }

            var rootPaths = GetBrowsableRootPaths();

            var directories = directoryInfo.EnumerateDirectories()
                .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxBrowserEntries + 1)
                .ToList();

            var remainingSlots = Math.Max(0, MaxBrowserEntries - Math.Min(directories.Count, MaxBrowserEntries));
            var files = directoryInfo.EnumerateFiles()
                .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(remainingSlots + 1)
                .ToList();

            var entries = new List<RemoteFileBrowserEntry>(Math.Min(MaxBrowserEntries, directories.Count + files.Count));
            foreach (var directory in directories.Take(MaxBrowserEntries))
            {
                entries.Add(new RemoteFileBrowserEntry
                {
                    Name = directory.Name,
                    FullPath = directory.FullName,
                    IsDirectory = true,
                    Size = 0,
                    LastModifiedAt = directory.LastWriteTimeUtc
                });
            }

            foreach (var file in files.Take(Math.Max(0, MaxBrowserEntries - entries.Count)))
            {
                entries.Add(new RemoteFileBrowserEntry
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    Size = file.Length,
                    LastModifiedAt = file.LastWriteTimeUtc
                });
            }

            var parentDirectoryPath = directoryInfo.Parent?.FullName ?? string.Empty;
            var entriesTruncated = directories.Count > MaxBrowserEntries || files.Count > remainingSlots;
            await publishStatusAsync(new AgentFileTransferStatusMessage
            {
                UploadId = requestId,
                Direction = "browse",
                Status = "listed",
                FileName = string.Empty,
                StoredFileName = string.Empty,
                StoredFilePath = directoryInfo.FullName,
                FileSize = 0,
                BytesTransferred = 0,
                DirectoryPath = directoryInfo.FullName,
                ParentDirectoryPath = parentDirectoryPath,
                CanNavigateUp = !string.IsNullOrWhiteSpace(parentDirectoryPath),
                EntriesTruncated = entriesTruncated,
                RootPaths = rootPaths,
                Entries = entries,
                Message = entriesTruncated
                    ? AgentUiText.Bi($"已列出 {entries.Count} 個項目（已截斷）。", $"Listed {entries.Count} items (truncated).")
                    : AgentUiText.Bi($"已列出 {entries.Count} 個項目。", $"Listed {entries.Count} items.")
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to list remote directory for request {RequestId}.", requestId);
            await PublishFailedAsync(
                requestId,
                string.Empty,
                0,
                AgentUiText.Bi($"遠端檔案總管載入失敗：{exception.Message}", $"Remote file browser load failed: {exception.Message}"),
                publishStatusAsync,
                cancellationToken,
                direction: "browse");
        }
    }

    private async Task HandleMoveAsync(
        ViewerCommandMessage command,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        var requestId = command.UploadId?.Trim() ?? string.Empty;
        var sourcePathInput = command.RemotePath?.Trim() ?? string.Empty;
        var destinationDirectoryInput = command.DestinationPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(sourcePathInput) || string.IsNullOrWhiteSpace(destinationDirectoryInput))
        {
            await PublishFailedAsync(
                requestId,
                Path.GetFileName(sourcePathInput),
                0,
                AgentUiText.Bi("移動檔案或資料夾的要求無效。", "The file or folder move request is invalid."),
                publishStatusAsync,
                cancellationToken,
                direction: "move");
            return;
        }

        try
        {
            var sourcePath = ResolveRemoteContentPath(sourcePathInput);
            var destinationDirectoryPath = ResolveBrowserDirectoryPath(destinationDirectoryInput);
            if (!Directory.Exists(destinationDirectoryPath))
            {
                throw new DirectoryNotFoundException(AgentUiText.Bi("目的資料夾不存在。", "The destination folder does not exist."));
            }

            var sourceIsFile = File.Exists(sourcePath);
            var sourceIsDirectory = Directory.Exists(sourcePath);
            if (!sourceIsFile && !sourceIsDirectory)
            {
                throw new FileNotFoundException(AgentUiText.Bi("找不到要移動的檔案或資料夾。", "The file or folder to move was not found."), sourcePath);
            }

            var sourceName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var currentParentPath = Path.GetDirectoryName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty;
            if (string.Equals(currentParentPath, destinationDirectoryPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(AgentUiText.Bi("所選項目已經在目的資料夾中。", "The selected item is already in the destination folder."));
            }

            if (sourceIsDirectory && IsPathWithin(destinationDirectoryPath, sourcePath))
            {
                throw new InvalidOperationException(AgentUiText.Bi("不能將資料夾移動到自己的子資料夾內。", "A folder cannot be moved into one of its own subfolders."));
            }

            var destinationPath = sourceIsDirectory
                ? ResolveUniqueDirectoryPath(destinationDirectoryPath, sourceName)
                : ResolveUniqueTargetPath(destinationDirectoryPath, sourceName);

            if (sourceIsFile)
            {
                File.Move(sourcePath, destinationPath);
            }
            else
            {
                Directory.Move(sourcePath, destinationPath);
            }

            await publishStatusAsync(new AgentFileTransferStatusMessage
            {
                UploadId = requestId,
                Direction = "move",
                Status = "completed",
                FileName = sourceName,
                StoredFileName = Path.GetFileName(destinationPath),
                StoredFilePath = destinationPath,
                FileSize = 0,
                BytesTransferred = 0,
                Message = sourceIsDirectory
                    ? AgentUiText.Bi($"已將資料夾「{sourceName}」移動到 {destinationDirectoryPath}。", $"Moved folder '{sourceName}' to {destinationDirectoryPath}.")
                    : AgentUiText.Bi($"已將檔案「{sourceName}」移動到 {destinationDirectoryPath}。", $"Moved file '{sourceName}' to {destinationDirectoryPath}.")
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to move remote content for request {RequestId}.", requestId);
            await PublishFailedAsync(
                requestId,
                Path.GetFileName(sourcePathInput),
                0,
                AgentUiText.Bi($"移動檔案或資料夾失敗：{exception.Message}", $"Failed to move the file or folder: {exception.Message}"),
                publishStatusAsync,
                cancellationToken,
                direction: "move");
        }
    }

    private async Task FailAndCleanupAsync(
        UploadSession session,
        string message,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        CancellationToken cancellationToken)
    {
        _uploads.TryRemove(session.UploadId, out _);
        await LogAsync("agent-upload-failed", message, new
        {
            uploadId = session.UploadId,
            fileName = session.FileName,
            storedFileName = session.StoredFileName,
            storedFilePath = session.TargetPath,
            bytesTransferred = session.BytesTransferred,
            fileSize = session.FileSize
        }, cancellationToken);
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
            await Net48Compat.DisposeAsyncCompat(session.Stream);
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
        CancellationToken cancellationToken,
        string direction = "upload")
    {
        await publishStatusAsync(new AgentFileTransferStatusMessage
        {
            UploadId = uploadId,
            Direction = direction,
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

    private string ResolveDownloadPath(string remotePath)
    {
        return ResolveRemoteContentPath(remotePath);
    }

    private string ResolveBrowserDirectoryPath(string? directoryPath)
    {
        var trimmed = directoryPath?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            var transferDirectory = Path.GetFullPath(ResolveTransferDirectory());
            Directory.CreateDirectory(transferDirectory);
            return transferDirectory;
        }

        var candidatePath = Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(ResolveTransferDirectory(), trimmed));

        if (File.Exists(candidatePath))
        {
            return Path.GetDirectoryName(candidatePath)
                ?? throw new DirectoryNotFoundException(AgentUiText.Bi("指定的遠端檔案沒有父資料夾。", "The requested remote file does not have a parent directory."));
        }

        if (Directory.Exists(candidatePath))
        {
            return candidatePath;
        }

        var parentDirectoryPath = Path.GetDirectoryName(candidatePath);
        if (!string.IsNullOrWhiteSpace(parentDirectoryPath)
            && Directory.Exists(parentDirectoryPath)
            && LooksLikeFilePath(trimmed))
        {
            return parentDirectoryPath;
        }

        return candidatePath;
    }

    private string ResolveRemoteContentPath(string remotePath)
    {
        var trimmed = remotePath.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        return Path.GetFullPath(Path.Combine(ResolveTransferDirectory(), trimmed));
    }

    private static bool LooksLikeFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fileName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return !string.IsNullOrWhiteSpace(fileName) && fileName.IndexOf('.') >= 0;
    }

    private static bool IsPathWithin(string candidatePath, string parentPath)
    {
        var normalizedCandidatePath = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedParentPath = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return normalizedCandidatePath.StartsWith(normalizedParentPath, StringComparison.OrdinalIgnoreCase);
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

    private static IReadOnlyList<string> GetBrowsableRootPaths()
    {
        return DriveInfo.GetDrives()
            .Where(static drive => drive.DriveType != DriveType.NoRootDirectory)
            .Select(static drive => drive.RootDirectory.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static string ResolveUniqueDirectoryPath(string directory, string folderName)
    {
        var candidate = Path.Combine(directory, folderName);
        var counter = 1;
        while (Directory.Exists(candidate) || File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{folderName} ({counter++})");
        }

        return candidate;
    }

    private Task LogAsync(string eventName, string message, object? data, CancellationToken cancellationToken)
    {
        return _fileTransferTraceService.WriteAsync(eventName, message, data, cancellationToken);
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

