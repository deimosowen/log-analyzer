# Log Analyzer

Blazor Server application for uploading, indexing, and correlating application logs and IIS W3C logs by time.

## Architecture

- `LogAnalyzer.Domain` contains domain records and shared constants.
- `LogAnalyzer.Application` contains parser contracts, import orchestration, parser implementations, and analysis helpers.
- `LogAnalyzer.Infrastructure` contains SQLite repositories, filesystem storage, archive extraction, local event store, and ClickHouse HTTP event store.
- `LogAnalyzer.Web` contains Blazor UI, API endpoints, and the background import worker.
- `LogAnalyzer.Tests` contains parser and analysis behavior tests.

## Storage

Metadata is stored in SQLite. Events use a configurable `ILogEventStore`:

- Local development defaults to SQLite event storage.
- Set `ClickHouse:Enabled=true` in `appsettings.json` to use ClickHouse.

Uploaded files, local SQLite data, build output, and screenshots are ignored by git.

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
