using Microsoft.Data.SqlClient;
using System.Data;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services.Auditing;

public sealed class SqlAuditLogStore : IAuditLogStore
{
    private readonly string _connectionString;

    public SqlAuditLogStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("RemoteDesktopDb")
            ?? throw new InvalidOperationException("Missing required connection string: ConnectionStrings:RemoteDesktopDb.");
    }

    public async Task AppendAsync(AuditLogEntryDto entry, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.RemoteDesktopAuditLogs
            (
                Id,
                OccurredAt,
                ActorUserName,
                ActorDisplayName,
                Action,
                TargetType,
                TargetId,
                Succeeded,
                Details
            )
            VALUES
            (
                @id,
                @occurredAt,
                @actorUserName,
                @actorDisplayName,
                @action,
                @targetType,
                @targetId,
                @succeeded,
                @details
            );
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = entry.Id;
        command.Parameters.Add("@occurredAt", SqlDbType.DateTimeOffset).Value = entry.OccurredAt;
        command.Parameters.Add("@actorUserName", SqlDbType.NVarChar, 128).Value = entry.ActorUserName;
        command.Parameters.Add("@actorDisplayName", SqlDbType.NVarChar, 256).Value = entry.ActorDisplayName;
        command.Parameters.Add("@action", SqlDbType.NVarChar, 128).Value = entry.Action;
        command.Parameters.Add("@targetType", SqlDbType.NVarChar, 128).Value = entry.TargetType;
        command.Parameters.Add("@targetId", SqlDbType.NVarChar, 256).Value = entry.TargetId;
        command.Parameters.Add("@succeeded", SqlDbType.Bit).Value = entry.Succeeded;
        command.Parameters.Add("@details", SqlDbType.NVarChar, -1).Value = entry.Details;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntryDto>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return Array.Empty<AuditLogEntryDto>();
        }

        const string sql = """
            SELECT TOP (@take)
                Id,
                OccurredAt,
                ActorUserName,
                ActorDisplayName,
                Action,
                TargetType,
                TargetId,
                Succeeded,
                Details
            FROM dbo.RemoteDesktopAuditLogs
            ORDER BY OccurredAt DESC, Id DESC;
            """;

        var items = new List<AuditLogEntryDto>(take);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@take", SqlDbType.Int).Value = take;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AuditLogEntryDto
            {
                Id = reader.GetGuid(0),
                OccurredAt = reader.GetFieldValue<DateTimeOffset>(1),
                ActorUserName = reader.GetString(2),
                ActorDisplayName = reader.GetString(3),
                Action = reader.GetString(4),
                TargetType = reader.GetString(5),
                TargetId = reader.GetString(6),
                Succeeded = reader.GetBoolean(7),
                Details = reader.GetString(8)
            });
        }

        return items;
    }
}
