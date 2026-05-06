using LogAnalyzer.Application;
using LogAnalyzer.Application.Projects;
using LogAnalyzer.Domain;
using Npgsql;

namespace LogAnalyzer.Infrastructure.Postgres;

public sealed class PostgresMetadataRepository : IMetadataRepository
{
    private readonly PostgresConnectionFactory _connectionFactory;

    public PostgresMetadataRepository(PostgresConnectionFactory connectionFactory)
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
                (@id, @provider, @provider_user_id, @email, @display_name, @created_at, @last_login_at)
            ON CONFLICT(provider, provider_user_id)
            DO UPDATE SET
                email = excluded.email,
                display_name = excluded.display_name,
                last_login_at = excluded.last_login_at;
            """;
        Add(command, "@id", userId);
        Add(command, "@provider", profile.Provider);
        Add(command, "@provider_user_id", profile.ProviderUserId);
        Add(command, "@email", profile.Email);
        Add(command, "@display_name", profile.DisplayName);
        Add(command, "@created_at", ToDb(now));
        Add(command, "@last_login_at", ToDb(now));
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
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (NpgsqlTransaction)transaction;
            command.CommandText = """
                INSERT INTO projects (id, owner_user_id, name, description, created_at, updated_at)
                VALUES (@id, @owner_user_id, @name, @description, @created_at, @updated_at);
                """;
            Add(command, "@id", project.Id);
            Add(command, "@owner_user_id", project.OwnerUserId);
            Add(command, "@name", project.Name);
            Add(command, "@description", project.Description);
            Add(command, "@created_at", ToDb(project.CreatedAt));
            Add(command, "@updated_at", ToDb(project.UpdatedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var memberCommand = connection.CreateCommand())
        {
            memberCommand.Transaction = (NpgsqlTransaction)transaction;
            memberCommand.CommandText = """
                INSERT INTO project_members (project_id, user_id, role, created_at)
                VALUES (@project_id, @user_id, 'owner', @created_at);
                """;
            Add(memberCommand, "@project_id", project.Id);
            Add(memberCommand, "@user_id", ownerUserId);
            Add(memberCommand, "@created_at", ToDb(project.CreatedAt));
            await memberCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return project;
    }

    public async Task<IReadOnlyList<ProjectEntity>> ListProjectsAsync(string ownerUserId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.*
            FROM projects p
            INNER JOIN project_members m ON m.project_id = p.id AND m.user_id = @user_id
            ORDER BY p.updated_at DESC;
            """;
        Add(command, "@user_id", ownerUserId);
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
        command.CommandText = """
            SELECT p.*
            FROM projects p
            INNER JOIN project_members m ON m.project_id = p.id AND m.user_id = @user_id
            WHERE p.id = @project_id;
            """;
        Add(command, "@project_id", projectId);
        Add(command, "@user_id", ownerUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProject(reader) : null;
    }

    public async Task DeleteProjectAsync(string ownerUserId, string projectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var projectCommand = connection.CreateCommand();
        projectCommand.CommandText = """
            SELECT 1 FROM project_members
            WHERE project_id = @id AND user_id = @owner_user_id AND role = 'owner';
            """;
        Add(projectCommand, "@id", projectId);
        Add(projectCommand, "@owner_user_id", ownerUserId);
        if (await projectCommand.ExecuteScalarAsync(cancellationToken) is null)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteDeleteAsync(
            connection,
            transaction,
            "DELETE FROM import_errors WHERE upload_session_id IN (SELECT id FROM upload_sessions WHERE project_id = @id);",
            projectId,
            cancellationToken);
        await ExecuteDeleteAsync(connection, transaction, "DELETE FROM log_files WHERE project_id = @id;", projectId, cancellationToken);
        await ExecuteDeleteAsync(connection, transaction, "DELETE FROM upload_sessions WHERE project_id = @id;", projectId, cancellationToken);
        await ExecuteDeleteAsync(connection, transaction, "DELETE FROM projects WHERE id = @id;", projectId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<string?> CreateShareInviteAsync(
        string creatorUserId,
        string projectId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var ownerCheck = connection.CreateCommand();
        ownerCheck.CommandText = "SELECT 1 FROM projects WHERE id = @project_id AND owner_user_id = @user_id;";
        Add(ownerCheck, "@project_id", projectId);
        Add(ownerCheck, "@user_id", creatorUserId);
        if (await ownerCheck.ExecuteScalarAsync(cancellationToken) is null)
        {
            return null;
        }

        var token = ShareInviteToken.Create();
        var inviteId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO project_share_invites (id, project_id, token, created_by_user_id, created_at)
            VALUES (@id, @project_id, @token, @created_by_user_id, @created_at);
            """;
        Add(insert, "@id", inviteId);
        Add(insert, "@project_id", projectId);
        Add(insert, "@token", token);
        Add(insert, "@created_by_user_id", creatorUserId);
        Add(insert, "@created_at", ToDb(now));
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return token;
    }

    public async Task<ProjectShareInvitePreview?> GetShareInvitePreviewAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.token, p.id, p.name, p.description, u.display_name, u.email, i.created_at
            FROM project_share_invites i
            INNER JOIN projects p ON p.id = i.project_id
            INNER JOIN app_users u ON u.id = i.created_by_user_id
            WHERE i.token = @token;
            """;
        Add(command, "@token", token);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProjectShareInvitePreview(
            reader.GetString(reader.GetOrdinal("token")),
            reader.GetString(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("name")),
            GetNullableString(reader, "description"),
            reader.GetString(reader.GetOrdinal("display_name")),
            reader.GetString(reader.GetOrdinal("email")),
            GetTimestamp(reader, "created_at"));
    }

    public async Task<ShareInviteAcceptResult> AcceptShareInviteAsync(
        string userId,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ShareInviteAcceptResult.NotAuthenticated;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return ShareInviteAcceptResult.InviteNotFound;
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string? projectId;
        await using (var loadInvite = connection.CreateCommand())
        {
            loadInvite.Transaction = (NpgsqlTransaction)transaction;
            loadInvite.CommandText = "SELECT project_id FROM project_share_invites WHERE token = @token FOR UPDATE;";
            Add(loadInvite, "@token", token);
            var scalar = await loadInvite.ExecuteScalarAsync(cancellationToken);
            projectId = scalar as string;
        }

        if (projectId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ShareInviteAcceptResult.InviteNotFound;
        }

        await using (var memberCheck = connection.CreateCommand())
        {
            memberCheck.Transaction = (NpgsqlTransaction)transaction;
            memberCheck.CommandText = """
                SELECT 1 FROM project_members WHERE project_id = @project_id AND user_id = @user_id;
                """;
            Add(memberCheck, "@project_id", projectId);
            Add(memberCheck, "@user_id", userId);
            if (await memberCheck.ExecuteScalarAsync(cancellationToken) is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ShareInviteAcceptResult.AlreadyHasAccess;
            }
        }

        var now = DateTimeOffset.UtcNow;
        await using (var insertMember = connection.CreateCommand())
        {
            insertMember.Transaction = (NpgsqlTransaction)transaction;
            insertMember.CommandText = """
                INSERT INTO project_members (project_id, user_id, role, created_at)
                VALUES (@project_id, @user_id, 'member', @created_at);
                """;
            Add(insertMember, "@project_id", projectId);
            Add(insertMember, "@user_id", userId);
            Add(insertMember, "@created_at", ToDb(now));
            await insertMember.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ShareInviteAcceptResult.Accepted;
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
                (@id, @project_id, @status, @original_name, @total_files, @processed_files, @total_lines,
                 @processed_lines, @error_count, @created_at, @finished_at, @current_file);
            """;
        AddUploadParameters(command, upload);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return upload;
    }

    public async Task<UploadSessionEntity?> GetUploadSessionAsync(string uploadSessionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM upload_sessions WHERE id = @id;";
        Add(command, "@id", uploadSessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUpload(reader) : null;
    }

    public async Task<IReadOnlyList<UploadSessionEntity>> ListUploadSessionsAsync(string projectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM upload_sessions WHERE project_id = @project_id ORDER BY created_at DESC;";
        Add(command, "@project_id", projectId);
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
            SET status = COALESCE(CAST(@status AS text), status),
                total_files = COALESCE(CAST(@total_files AS integer), total_files),
                processed_files = COALESCE(CAST(@processed_files AS integer), processed_files),
                total_lines = COALESCE(CAST(@total_lines AS bigint), total_lines),
                processed_lines = COALESCE(CAST(@processed_lines AS bigint), processed_lines),
                error_count = COALESCE(CAST(@error_count AS integer), error_count),
                finished_at = COALESCE(CAST(@finished_at AS timestamptz), finished_at),
                current_file = COALESCE(CAST(@current_file AS text), current_file)
            WHERE id = @id;
            """;
        Add(command, "@id", uploadSessionId);
        Add(command, "@status", update.Status);
        Add(command, "@total_files", update.TotalFiles);
        Add(command, "@processed_files", update.ProcessedFiles);
        Add(command, "@total_lines", update.TotalLines);
        Add(command, "@processed_lines", update.ProcessedLines);
        Add(command, "@error_count", update.ErrorCount);
        Add(command, "@finished_at", update.FinishedAt is null ? null : ToDb(update.FinishedAt.Value));
        Add(command, "@current_file", update.CurrentFile);
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
                (@id, @project_id, @upload_session_id, @original_path, @stored_path, @display_name, @size_bytes,
                 @hash, @format, @timezone, @first_timestamp, @last_timestamp, @line_count, @status);
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
            SET format = COALESCE(CAST(@format AS text), format),
                status = COALESCE(CAST(@status AS text), status),
                first_timestamp = COALESCE(CAST(@first_timestamp AS timestamptz), first_timestamp),
                last_timestamp = COALESCE(CAST(@last_timestamp AS timestamptz), last_timestamp),
                line_count = COALESCE(CAST(@line_count AS bigint), line_count)
            WHERE id = @id;
            """;
        Add(command, "@id", logFileId);
        Add(command, "@format", update.Format);
        Add(command, "@status", update.Status);
        Add(command, "@first_timestamp", update.FirstTimestamp is null ? null : ToDb(update.FirstTimestamp.Value));
        Add(command, "@last_timestamp", update.LastTimestamp is null ? null : ToDb(update.LastTimestamp.Value));
        Add(command, "@line_count", update.LineCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LogFileEntity>> ListLogFilesAsync(string projectId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM log_files WHERE project_id = @project_id ORDER BY display_name;";
        Add(command, "@project_id", projectId);
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
        command.CommandText = "SELECT * FROM log_files WHERE id = @id;";
        Add(command, "@id", logFileId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadLogFile(reader) : null;
    }

    public async Task AddImportErrorAsync(ImportErrorEntity error, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (NpgsqlTransaction)transaction;
            command.CommandText = """
                INSERT INTO import_errors
                    (id, upload_session_id, log_file_id, line_number, error_message, raw_text, created_at)
                VALUES
                    (@id, @upload_session_id, @log_file_id, @line_number, @error_message, @raw_text, @created_at);
                """;
            Add(command, "@id", error.Id);
            Add(command, "@upload_session_id", error.UploadSessionId);
            Add(command, "@log_file_id", error.LogFileId);
            Add(command, "@line_number", error.LineNumber);
            Add(command, "@error_message", error.ErrorMessage);
            Add(command, "@raw_text", error.RawText);
            Add(command, "@created_at", ToDb(error.CreatedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (NpgsqlTransaction)transaction;
            command.CommandText = "UPDATE upload_sessions SET error_count = error_count + 1 WHERE id = @id;";
            Add(command, "@id", error.UploadSessionId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImportErrorEntity>> ListImportErrorsAsync(string uploadSessionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM import_errors WHERE upload_session_id = @id ORDER BY created_at DESC LIMIT {EventSearchDefaults.RecentImportErrorsLimit};";
        Add(command, "@id", uploadSessionId);
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
                GetTimestamp(reader, "created_at")));
        }

        return result;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        return await _connectionFactory.OpenAsync(cancellationToken);
    }

    private static async Task ExecuteDeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        string projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        Add(command, "@id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Add(NpgsqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static void AddUploadParameters(NpgsqlCommand command, UploadSessionEntity upload)
    {
        Add(command, "@id", upload.Id);
        Add(command, "@project_id", upload.ProjectId);
        Add(command, "@status", upload.Status);
        Add(command, "@original_name", upload.OriginalName);
        Add(command, "@total_files", upload.TotalFiles);
        Add(command, "@processed_files", upload.ProcessedFiles);
        Add(command, "@total_lines", upload.TotalLines);
        Add(command, "@processed_lines", upload.ProcessedLines);
        Add(command, "@error_count", upload.ErrorCount);
        Add(command, "@created_at", ToDb(upload.CreatedAt));
        Add(command, "@finished_at", upload.FinishedAt is null ? null : ToDb(upload.FinishedAt.Value));
        Add(command, "@current_file", upload.CurrentFile);
    }

    private static void AddLogFileParameters(NpgsqlCommand command, LogFileEntity logFile)
    {
        Add(command, "@id", logFile.Id);
        Add(command, "@project_id", logFile.ProjectId);
        Add(command, "@upload_session_id", logFile.UploadSessionId);
        Add(command, "@original_path", logFile.OriginalPath);
        Add(command, "@stored_path", logFile.StoredPath);
        Add(command, "@display_name", logFile.DisplayName);
        Add(command, "@size_bytes", logFile.SizeBytes);
        Add(command, "@hash", logFile.Hash);
        Add(command, "@format", logFile.Format);
        Add(command, "@timezone", logFile.TimeZone);
        Add(command, "@first_timestamp", logFile.FirstTimestamp is null ? null : ToDb(logFile.FirstTimestamp.Value));
        Add(command, "@last_timestamp", logFile.LastTimestamp is null ? null : ToDb(logFile.LastTimestamp.Value));
        Add(command, "@line_count", logFile.LineCount);
        Add(command, "@status", logFile.Status);
    }

    private static ProjectEntity ReadProject(NpgsqlDataReader reader)
    {
        return new ProjectEntity(
            reader.GetString(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("owner_user_id")),
            reader.GetString(reader.GetOrdinal("name")),
            GetNullableString(reader, "description"),
            GetTimestamp(reader, "created_at"),
            GetTimestamp(reader, "updated_at"));
    }

    private static UploadSessionEntity ReadUpload(NpgsqlDataReader reader)
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
            GetTimestamp(reader, "created_at"),
            GetNullableTimestamp(reader, "finished_at"),
            GetNullableString(reader, "current_file"));
    }

    private static LogFileEntity ReadLogFile(NpgsqlDataReader reader)
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

    private static DateTimeOffset ToDb(DateTimeOffset timestamp)
    {
        return timestamp.ToUniversalTime();
    }

    private static string? GetNullableString(NpgsqlDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : reader.GetString(index);
    }

    private static long? GetNullableInt64(NpgsqlDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : reader.GetInt64(index);
    }

    private static DateTimeOffset? GetNullableTimestamp(NpgsqlDataReader reader, string name)
    {
        var index = reader.GetOrdinal(name);
        return reader.IsDBNull(index) ? null : GetTimestamp(reader, index);
    }

    private static DateTimeOffset GetTimestamp(NpgsqlDataReader reader, string name)
    {
        return GetTimestamp(reader, reader.GetOrdinal(name));
    }

    private static DateTimeOffset GetTimestamp(NpgsqlDataReader reader, int index)
    {
        var value = reader.GetValue(index);
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
            DateTime dateTime => ToUtcOffset(dateTime),
            _ => throw new InvalidOperationException($"Unexpected timestamp value type: {value.GetType().FullName}.")
        };
    }

    private static DateTimeOffset ToUtcOffset(DateTime dateTime)
    {
        var utc = dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

        return new DateTimeOffset(utc);
    }
}
