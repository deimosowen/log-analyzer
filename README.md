# Log Analyzer

Blazor Server application for uploading, indexing, and correlating application logs and IIS W3C logs by time.

Repository: https://github.com/deimosowen/log-analyzer

License: MIT

Roadmap: [ROADMAP.md](ROADMAP.md)

## Purpose

Log Analyzer helps investigate incidents when the useful context is spread across several logs. Create an incident project, upload logs, pick a problematic event, and inspect related events in the same time window with timezone offsets applied.

The main dashboard shows the current user's incident count, uploads, analyzed logs, indexed events, and problem events.

## Architecture

- `LogAnalyzer.Domain`
  - `Constants` contains stable domain names such as log levels, formats, and statuses.
  - `Entities` contains persisted domain records.
  - `ReadModels` contains query/read-side DTOs.
- `LogAnalyzer.Application`
  - `Analysis` contains correlation grouping rules.
  - `Events`, `Import`, `Metadata`, and `Storage` contain application contracts and orchestration.
  - `Parsing` contains parser implementations and parser contracts.
  - `Time` contains time-zone conversion helpers and defaults.
- `LogAnalyzer.Infrastructure`
  - `Migrations` contains shared migration primitives.
  - `Postgres` contains PostgreSQL metadata storage, repository, and migrations.
  - `Sqlite` contains metadata/event repositories and SQLite migrations.
  - `ClickHouse` contains ClickHouse event storage, SQL client, and migrations.
  - `Storage` contains filesystem upload/archive storage.
- `LogAnalyzer.Web`
  - `Api` contains endpoint groups, routes, query parsing, and request contracts.
  - `Components` contains Blazor UI.
  - `Services` contains hosted/background web services.
- `LogAnalyzer.Tests` contains parser and analysis behavior tests.

## Storage

Metadata storage is selected through `Metadata:Provider`:

- Docker/production uses PostgreSQL metadata storage.
- Local development can keep SQLite metadata storage with `Metadata:Provider=SQLite`.

Events use a configurable `ILogEventStore`:

- Local development defaults to SQLite event storage.
- Set `ClickHouse:Enabled=true` in `appsettings.json` to use ClickHouse.

Uploaded files, local SQLite data, build output, and screenshots are ignored by git.

## Migrations

Database schema is updated through `IDatabaseMigrator` implementations at application startup.

- SQLite metadata schema: `SqliteMetadataMigrator`.
- PostgreSQL metadata schema: `PostgresMetadataMigrator`.
- SQLite event schema: `SqliteEventStoreMigrator`.
- ClickHouse event schema: `ClickHouseEventStoreMigrator`.

Each migrator writes applied versions into `schema_migrations`. Migration SQL lives in one file per version and is grouped by database and schema:

- SQLite metadata: `src/LogAnalyzer.Infrastructure/Sqlite/Migrations/Metadata/V###_*.cs`.
- PostgreSQL metadata: `src/LogAnalyzer.Infrastructure/Postgres/Migrations/Metadata/V###_*.cs`.
- SQLite events: `src/LogAnalyzer.Infrastructure/Sqlite/Migrations/Events/V###_*.cs`.
- ClickHouse events: `src/LogAnalyzer.Infrastructure/ClickHouse/Migrations/Events/V###_*.cs`.

Migration files are discovered automatically through marker base classes:

- SQLite metadata migrations inherit `SqliteMetadataMigration`.
- PostgreSQL metadata migrations inherit `PostgresMetadataMigration`.
- SQLite event migrations inherit `SqliteEventMigration`.
- ClickHouse event migrations inherit `ClickHouseEventMigration`.

To extend a schema, add the next `V###_*.cs` file in the relevant folder and inherit the matching base class. The catalog will load it automatically, sort migrations by `Version`, and fail fast on duplicate or invalid versions.

## PostgreSQL Metadata

Production deployments should use PostgreSQL for metadata:

```json
"Metadata": {
  "Provider": "PostgreSQL"
},
"Postgres": {
  "Host": "postgres",
  "Port": 5432,
  "Database": "log_analyzer",
  "Username": "log_analyzer",
  "Password": "<password>"
}
```

`Postgres:ConnectionString` can be used instead of the individual host/database/user settings. SQLite remains available for local development by setting `Metadata:Provider=SQLite`.

## Authentication

Authentication is configured in `Authentication` settings. Local development uses the configured development user while `Authentication:Enabled=false`.

For Yandex OAuth, set:

```json
"Authentication": {
  "Enabled": true,
  "PublicOrigin": "https://log-analyzer.example.com",
  "AllowedEmailDomains": [ "example.com" ],
  "Yandex": {
    "ClientId": "<client-id>",
    "ClientSecret": "<client-secret>",
    "CallbackPath": "/signin-yandex"
  }
}
```

When `AllowedEmailDomains` is empty, any verified Yandex email domain is accepted. When it contains domains, only matching email domains can sign in.
Set `PublicOrigin` on production deployments behind a reverse proxy so the OAuth redirect URI is generated with the public HTTPS host instead of the container's internal HTTP address.

## Run

```powershell
dotnet restore .\LogAnalyzer.slnx
dotnet run --project .\src\LogAnalyzer.Web\LogAnalyzer.Web.csproj --urls http://localhost:5071
```

Open `http://localhost:5071`.

## Docker

Build and push the application image:

```powershell
docker build -t deimosowen/log-analyzer:1.1.0 -t deimosowen/log-analyzer:latest .
docker push deimosowen/log-analyzer:1.1.0
docker push deimosowen/log-analyzer:latest
```

Deploy with PostgreSQL metadata storage and ClickHouse event storage:

```powershell
Copy-Item .env.example .env
# Fill YANDEX_CLIENT_ID, YANDEX_CLIENT_SECRET, and POSTGRES_PASSWORD in .env.
docker compose up -d
```

For Yandex OAuth on a server, add this redirect URI in the Yandex app:

```text
https://<your-host>/signin-yandex
```

Also set `PUBLIC_ORIGIN=https://<your-host>` in `.env`.

`docker-compose.yml` keeps PostgreSQL data, uploaded files, and ClickHouse data under `./data` next to the compose file. The image does not include local `appsettings*.json`; production settings are passed through environment variables.
The compose file uses `deimosowen/log-analyzer:latest` so routine patch releases do not require editing the image tag.

Backup PostgreSQL metadata:

```bash
docker compose exec postgres sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > metadata-backup.sql
```

Restore PostgreSQL metadata into an empty database:

```bash
docker compose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" "$POSTGRES_DB"' < metadata-backup.sql
```

Back up `./data/storage` for uploaded source logs and `./data/clickhouse` or ClickHouse-native backups for indexed events.

Existing SQLite metadata is not copied automatically when switching `Metadata:Provider` to PostgreSQL. For an existing installation, keep the old SQLite database file intact and run a dedicated one-shot transfer before cutting production traffic over to PostgreSQL.

## Verify

```powershell
dotnet format .\LogAnalyzer.slnx --verify-no-changes
dotnet build .\LogAnalyzer.slnx
dotnet test .\LogAnalyzer.slnx
```
