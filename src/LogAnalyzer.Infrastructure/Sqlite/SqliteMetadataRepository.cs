using System.Globalization;
using LogAnalyzer.Application;
using LogAnalyzer.Domain;
using Microsoft.Data.Sqlite;

namespace LogAnalyzer.Infrastructure.Sqlite;

public sealed class SqliteMetadataRepository : IMetadataRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteMetadataRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserEntity> UpsertUserAsync(UserProfile profile, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = $"{profile.Provider}:{profile.ProviderUserId}";

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_users
                (id, provider, provider_user_id, email, display_name, created_at, last_login_at)
            VALUES
                ($id, $provider, $provider_user_id, $email, $display_name, $created_at, $last_login_at)
            ON CONFLICT(provider, provider_user_id)
            DO UPDATE SET
                email = excluded.email,
                display_name = excluded.display_name,
                last_login_at = excluded.last_login_at;
            """;
        Add(command, "$id", userId);
        Add(command, "$provider", profile.Provider);
        Add(command, "$provider_user_id", profile.ProviderUserId);
        Add(command, "$email", profile.Email);
        Add(command, "$display_name", profile.DisplayName);
        Add(command, "$created_at", ToDb(now));
        Add(command, "$last_login_at", ToDb(now));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new UserEntity(
            userId,
            profile.Provider,
            profile.ProviderUserId,
            profile.Email,
            profile.DisplayName,
            now,
            now);
    }

    public async Task<ProjectEntity> CreateProjectAsync(
        string ownerUserId,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectEntity(Guid.NewGuid().ToString("N"), ownerUserId, name.Trim(), description, now, now);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO projects (id, owner_user_id, name, description, created_at, updated_at)
            VALUES ($id, $owner_user_id, $name, $description, $created_at, $updated_at);
            """;
        Add(command, "$id", project.Id);
        Add(command, "$owner_user_id", project.OwnerUserId);
        Add(command, "$name", project.Name);
        Add(command, "$description", project.Description);
        Add(command, "$created_at", ToDb(project.CreatedAt));
        Add(command, "$updated_at", ToDb(project.UpdatedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return project;
    }

    public async Task<IReadOnlyList<ProjectEntity>> ListProjectsAsync(string ownerUserId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM projects WHERE owner_user_id = $owner_user_id ORDER BY updated_at DESC;";
        Add(command, "$owner_user_id", ownerUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ProjectEntity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadProject(reader));
        }

        return result;
    }

    public async Task<ProjectEntity?> GetProjectAsync(string ownerUserId, string projectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM projects WHERE id = $id AND owner_user_id = $owner_user_id;";
        Add(command, "$id", projectId);
        Add(command, "$owner_user_id", ownerUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProject(reader) : null;
    }

    public async Task DeleteProjectAsync(string ownerUserId, string projectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var projectCommand = connection.CreateCommand();
        projectCommand.CommandText = "SELECT 1 FROM projects WHERE id = $id AND owner_user_id = $owner_user_id;";
        Add(projectCommand, "$id", projectId);
        Add(projectCommand, "$owner_user_id", ownerUserId);
        if (await projectCommand.ExecuteScalarAsync(cancellationToken) is null)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var table in new[] { "import_errors", "log_files", "upload_sessions", "projects" })
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = table switch
            {
                "import_errors" => "DELETE FROM import_errors WHERE upload_session_id IN (SELECT id FROM upload_sessions WHERE project_id = $id);",
                "log_files" => "DELETE FROM log_files WHERE project_id = $id;",
                "upload_sessions" => "DELETE FROM upload_sessions WHERE project_id = $id;",
                _ => "DELETE FROM projects WHERE id = $id;"
            };
            Add(command, "$id", projectId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<UploadSessionEntity> CreateUploadSessionAsync(UploadSessionCreateRequest request, CancellationToken cancellationToken)
    {
        var upload = new UploadSessionEntity(
            Guid.NewGuid().ToString("N"),
            request.ProjectId,
            UploadStatuses.Created,
            request.OriginalName,
            0,
            0,
            0,
            0,
            0,
            DateTimeOffset.UtcNow,
            null,
            null);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO upload_sessions
                (id, project_id, status, original_name, total_files, processed_files, total_lines,
                 processed_lines, error_count, created_at, finished_at, current_file)
            VALUES
                ($id, $project_id, $status, $original_name, $total_files, $processed_files, $total_lines,
                 $processed_lines, $error_count, $created_at, $finished_at, $current_file);
            """;
        AddUploadParameters(command, upload);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return upload;
    }

    public async Task<UploadSessionEntity?> GetUploadSessionAsync(string uploadSessionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM upload_sessions WHERE id = $id;";
        Add(command, "$id", uploadSessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUpload(reader) : null;
    }

    public async Task<IReadOnlyList<UploadSessionEntity>> ListUploadSessionsAsync(string projectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM upload_sessions WHERE project_id = $project_id ORDER BY created_at DESC;";
        Add(command, "$project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<UploadSessionEntity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadUpload(reader));
        }

        return result;
    }

    public async Task UpdateUploadSessionAsync(string uploadSessionId, UploadProgressUpdate update, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE upload_sessions
            SET status = COALESCE($status, status),
                total_files = COALESCE($total_files, total_files),
                processed_files = COALESCE($processed_files, processed_files),
                total_lines = COALESCE($total_lines, total_lines),
                processed_lines = COALESCE($processed_lines, processed_lines),
                error_count = COALESCE($error_count, error_count),
                finished_at = COALESCE($finished_at, finished_at),
                current_file = COALESCE($current_file, current_file)
            WHERE id = $id;
            """;
        Add(command, "$id", uploadSessionId);
        Add(command, "$status", update.Status);
        Add(command, "$total_files", update.TotalFiles);
        Add(command, "$processed_files", update.ProcessedFiles);
        Add(command, "$total_lines", update.TotalLines);
        Add(command, "$processed_lines", update.ProcessedLines);
        Add(command, "$error_count", update.ErrorCount);
        Add(command, "$finished_at", update.FinishedAt is null ? null : ToDb(update.FinishedAt.Value));
        Add(command, "$current_file", update.CurrentFile);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LogFileEntity> CreateLogFileAsync(LogFileEntity logFile, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO log_files
                (id, project_id, upload_session_id, original_path, stored_path, display_name, size_bytes,
                 hash, format, timezone, first_timestamp, last_timestamp, line_count, status)
            VALUES
                ($id, $project_id, $upload_session_id, $original_path, $stored_path, $display_name, $size_bytes,
                 $hash, $format, $timezone, $first_timestamp, $last_timestamp, $line_count, $status);
            """;
        AddLogFileParameters(command, logFile);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return logFile;
    }

    public async Task UpdateLogFileAsync(string logFileId, LogFileUpdate update, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE log_files
            SET format = COALESCE($format, format),
                status = COALESCE($status, status),
                first_timestamp = COALESCE($first_timestamp, first_timestamp),
                last_timestamp = COALESCE($last_timestamp, last_timestamp),
                line_count = COALESCE($line_count, line_count)
            WHERE id = $id;
            """;
        Add(command, "$id", logFileId);
        Add(command, "$format", update.Format);
        Add(command, "$status", update.Status);
        Add(command, "$first_timestamp", update.FirstTimestamp is null ? null : ToDb(update.FirstTimestamp.Value));
        Add(command, "$last_timestamp", update.LastTimestamp is null ? null : ToDb(update.LastTimestamp.Value));
        Add(command, "$line_count", update.LineCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LogFileEntity>> ListLogFilesAsync(string projectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM log_files WHERE project_id = $project_id ORDER BY display_name;";
        Add(command, "$project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<LogFileEntity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadLogFile(reader));
        }

        return result;
    }

    public async Task<LogFileEntity?> GetLogFileAsync(string logFileId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM log_files WHERE id = $id;";
        Add(command, "$id", logFileId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadLogFile(reader) : null;
    }

    public async Task AddImportErrorAsync(ImportErrorEntity error, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO import_errors
                    (id, upload_session_id, log_file_id, line_number, error_message, raw_text, created_at)
                VALUES
                    ($id, $upload_session_id, $log_file_id, $line_number, $error_message, $raw_text, $created_at);
                """;
            Add(command, "$id", error.Id);
            Add(command, "$upload_session_id", error.UploadSessionId);
            Add(command, "$log_file_id", error.LogFileId);
            Add(command, "$line_number", error.LineNumber);
            Add(command, "$error_message", error.ErrorMessage);
            Add(command, "$raw_text", error.RawText);
            Add(command, "$created_at", ToDb(error.CreatedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "UPDATE upload_sessions SET error_count = error_count + 1 WHERE id = $id;";
            Add(command, "$id", error.UploadSessionId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImportErrorEntity>> ListImportErrorsAsync(string uploadSessionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM import_errors WHERE upload_session_id = $id ORDER BY created_at DESC LIMIT {EventSearchDefaults.RecentImportErrorsLimit};";
        Add(command, "$id", uploadSessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ImportErrorEntity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ImportErrorEntity(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("upload_session_id")),
                GetNullableString(reader, "log_file_id"),
                GetNullableInt64(reader, "line_number"),
                reader.GetString(reader.GetOrdinal("error_message")),
                GetNullableString(reader, "raw_text"),
                FromDb(reader.GetString(reader.GetOrdinal("created_at")))));
        }

        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        return await _connectionFactory.OpenAsync(cancellationToken);
    }

    private static void Add(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static void AddUploadParameters(SqliteCommand command, UploadSessionEntity upload)
    {
        Add(command, "$id", upload.Id);
        Add(command, "$project_id", upload.ProjectId);
        Add(command, "$status", upload.Status);
        Add(command, "$original_name", upload.OriginalName);
        Add(command, "$total_files", upload.TotalFiles);
        Add(command, "$processed_files", upload.ProcessedFiles);
        Add(command, "$total_lines", upload.TotalLines);
        Add(command, "$processed_lines", upload.ProcessedLines);
        Add(command, "$error_count", upload.ErrorCount);
        Add(command, "$created_at", ToDb(upload.CreatedAt));
        Add(command, "$finished_at", upload.FinishedAt is null ? null : ToDb(upload.FinishedAt.Value));
        Add(command, "$current_file", upload.CurrentFile);
    }

    private static void AddLogFileParameters(SqliteCommand command, LogFileEntity logFile)
    {
        Add(command, "$id", logFile.Id);
        Add(command, "$project_id", logFile.ProjectId);
        Add(command, "$upload_session_id", logFile.UploadSessionId);
        Add(command, "$original_path", logFile.OriginalPath);
        Add(command, "$stored_path", logFile.StoredPath);
        Add(command, "$display_name", logFile.DisplayName);
        Add(command, "$size_bytes", logFile.SizeBytes);
        Add(command, "$hash", logFile.Hash);
        Add(command, "$format", logFile.Format);
        Add(command, "$timezone", logFile.TimeZone);
        Add(command, "$first_timestamp", logFile.FirstTimestamp is null ? null : ToDb(logFile.FirstTimestamp.Value));
        Add(command, "$last_timestamp", logFile.LastTimestamp is null ? null : ToDb(logFile.LastTimestamp.Value));
        Add(command, "$line_count", logFile.LineCount);
        Add(command, "$status", logFile.Status);
    }

    private static ProjectEntity ReadProject(SqliteDataReader reader)
    {
        return new ProjectEntity(
            reader.GetString(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("owner_user_id")),
            reader.GetString(reader.GetOrdinal("name")),
            GetNullableString(reader, "description"),
            FromDb(reader.GetString(reader.GetOrdinal("created_at"))),
            FromDb(reader.GetString(reader.GetOrdinal("updated_at"))));
    }

    private static UploadSessionEntity ReadUpload(SqliteDataReader reader)
    {
        return new UploadSessionEntity(
            reader.GetString(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("project_id")),
            reader.GetString(reader.GetOrdinal("status")),
            GetNullableString(reader, "original_name"),
            reader.GetInt32(reader.GetOrdinal("total_files")),
            reader.GetInt32(reader.GetOrdinal("processed_files")),
            reader.GetInt64(reader.GetOrdinal("total_lines")),
            reader.GetInt64(reader.GetOrdinal("processed_lines")),
            reader.GetInt32(reader.GetOrdinal("error_count")),
            FromDb(reader.GetString(reader.GetOrdinal("created_at"))),
            GetNullableTimestamp(reader, "finished_at"),
            GetNullableString(reader, "current_file"));
    }

    private static LogFileEntity ReadLogFile(SqliteDataReader reader)
    {
        return new LogFileEntity(
            reader.GetString(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("project_id")),
            reader.GetString(reader.GetOrdinal("upload_session_id")),
            reader.GetString(reader.GetOrdinal("original_path")),
            reader.GetString(reader.GetOrdinal("stored_path")),
            reader.GetString(reader.GetOrdinal("display_name")),
            reader.GetInt64(reader.GetOrdinal("size_bytes")),
            GetNullableString(reader, "hash"),
            reader.GetString(reader.GetOrdinal("format")),
            reader.GetString(reader.GetOrdinal("timezone")),
            GetNullableTimestamp(reader, "first_timestamp"),
            GetNullableTimestamp(reader, "last_timestamp"),
            reader.GetInt64(reader.GetOrdinal("line_count")),
            reader.GetString(reader.GetOrdinal("status")));
    }

    private static string ToDb(DateTimeOffset timestamp)
    {
        return timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset FromDb(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).ToUniversalTime();
    }

    private static string? GetNullableString(SqliteDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : reader.GetString(index);
    }

    private static long? GetNullableInt64(SqliteDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : reader.GetInt64(index);
    }

    private static DateTimeOffset? GetNullableTimestamp(SqliteDataReader reader, string name)
    {
        var value = GetNullableString(reader, name);
        return value is null ? null : FromDb(value);
    }
}
