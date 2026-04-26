# Log Analyzer

Blazor Server application for uploading, indexing, and correlating application logs and IIS W3C logs by time.

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
  - `Sqlite` contains metadata/event repositories and SQLite migrations.
  - `ClickHouse` contains ClickHouse event storage, SQL client, and migrations.
  - `Storage` contains filesystem upload/archive storage.
- `LogAnalyzer.Web`
  - `Api` contains endpoint groups, routes, query parsing, and request contracts.
  - `Components` contains Blazor UI.
  - `Services` contains hosted/background web services.
- `LogAnalyzer.Tests` contains parser and analysis behavior tests.

## Storage

Metadata is stored in SQLite. Events use a configurable `ILogEventStore`:

- Local development defaults to SQLite event storage.
- Set `ClickHouse:Enabled=true` in `appsettings.json` to use ClickHouse.

Uploaded files, local SQLite data, build output, and screenshots are ignored by git.

## Migrations

Database schema is updated through `IDatabaseMigrator` implementations at application startup.

- SQLite metadata schema: `SqliteMetadataMigrator`.
- SQLite event schema: `SqliteEventStoreMigrator`.
- ClickHouse event schema: `ClickHouseEventStoreMigrator`.

Each migrator writes applied versions into `schema_migrations`. To extend a schema, add the next `DatabaseMigration` entry to the relevant migrator instead of editing repository initialization code.

## Run

```powershell
dotnet restore .\LogAnalyzer.slnx
dotnet run --project .\src\LogAnalyzer.Web\LogAnalyzer.Web.csproj --urls http://localhost:5071
```

Open `http://localhost:5071`.

## Verify

```powershell
dotnet format .\LogAnalyzer.slnx --verify-no-changes
dotnet build .\LogAnalyzer.slnx
dotnet test .\LogAnalyzer.slnx
```
