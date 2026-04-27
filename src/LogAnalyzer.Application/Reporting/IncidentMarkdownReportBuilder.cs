using System.Text;
using LogAnalyzer.Application.Analysis;
using LogAnalyzer.Domain;

namespace LogAnalyzer.Application.Reporting;

public static class IncidentMarkdownReportBuilder
{
    public static IncidentMarkdownReport Build(IncidentMarkdownReportRequest request)
    {
        var content = BuildContent(request);
        var fileName = BuildFileName(request.ProjectName, request.SelectedEvent.TimestampUtc);
        return new IncidentMarkdownReport(fileName, content);
    }

    private static string BuildContent(IncidentMarkdownReportRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Отчет анализа инцидента");
        builder.AppendLine();
        builder.AppendLine("## Контекст");
        AppendListItem(builder, "Проект", request.ProjectName);
        AppendListItem(builder, "Сформирован", DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        AppendListItem(builder, "Часовой пояс отображения", request.DisplayTimeZoneId);
        AppendListItem(builder, "Окно", $"{request.BeforeSeconds} сек до / {request.AfterSeconds} сек после");
        AppendListItem(builder, "Логи", request.SelectedLogPreview);
        AppendListItem(builder, "Поиск", string.IsNullOrWhiteSpace(request.Query) ? "не задан" : request.Query);
        AppendListItem(builder, "Найдено", BuildFoundCount(request));
        builder.AppendLine();

        AppendSelectedEvent(builder, request);
        AppendGroups(builder, request);
        AppendEvents(builder, request);
        AppendRawDetails(builder, request.SelectedEvent);

        return builder.ToString();
    }

    private static void AppendSelectedEvent(StringBuilder builder, IncidentMarkdownReportRequest request)
    {
        var selected = request.SelectedEvent;
        builder.AppendLine("## Выбранная проблема");
        AppendListItem(builder, "Время", $"{request.FormatTime(selected.TimestampUtc)} ({selected.TimestampUtc:O})");
        AppendListItem(builder, "Уровень", selected.Level);
        AppendListItem(builder, "Лог", request.LogName(selected.LogFileId));
        AppendListItem(builder, "Источник", selected.Source);
        AppendListItem(builder, "Строка", selected.LineNumber.ToString());
        AppendListItem(builder, "Сообщение", selected.Message);

        if (!string.IsNullOrWhiteSpace(selected.HttpMethod) || selected.StatusCode > 0)
        {
            AppendListItem(builder, "HTTP", $"{selected.HttpMethod} {selected.Url} {selected.StatusCode}, {selected.TimeTaken} мс");
        }

        builder.AppendLine();
    }

    private static void AppendGroups(StringBuilder builder, IncidentMarkdownReportRequest request)
    {
        builder.AppendLine("## Сводка групп");
        if (request.CorrelationGroups.Count == 0)
        {
            builder.AppendLine("Проблем в выбранном окне времени не найдено.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| # | Кол-во | Логов | Уровень | Окно | Группа | Пример лога |");
        builder.AppendLine("|---:|---:|---:|---|---|---|---|");

        for (var i = 0; i < request.CorrelationGroups.Count; i++)
        {
            var group = request.CorrelationGroups[i];
            builder
                .Append("| ")
                .Append(i + 1)
                .Append(" | ")
                .Append(group.Count)
                .Append(" | ")
                .Append(group.LogCount)
                .Append(" | ")
                .Append(Cell(group.Level))
                .Append(" | ")
                .Append(Cell(request.FormatGroupWindow(group)))
                .Append(" | ")
                .Append(Cell(group.Title))
                .Append(" | ")
                .Append(Cell(request.LogName(group.Representative.LogFileId)))
                .AppendLine(" |");
        }

        builder.AppendLine();
    }

    private static void AppendEvents(StringBuilder builder, IncidentMarkdownReportRequest request)
    {
        builder.AppendLine("## События в окне");
        if (request.CorrelatedEvents.Count == 0)
        {
            builder.AppendLine("Нет событий для вывода.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Дельта | Время | Уровень | Лог | Строка | Источник | HTTP | Сообщение |");
        builder.AppendLine("|---:|---|---|---|---:|---|---|---|");

        foreach (var item in request.CorrelatedEvents.OrderBy(static item => item.TimestampUtc))
        {
            builder
                .Append("| ")
                .Append(Cell(request.FormatDelta(item)))
                .Append(" | ")
                .Append(Cell(request.FormatTime(item.TimestampUtc)))
                .Append(" | ")
                .Append(Cell(item.Level))
                .Append(" | ")
                .Append(Cell(request.LogName(item.LogFileId)))
                .Append(" | ")
                .Append(item.LineNumber)
                .Append(" | ")
                .Append(Cell(item.Source))
                .Append(" | ")
                .Append(Cell(BuildHttpSummary(item)))
                .Append(" | ")
                .Append(Cell(item.Message))
                .AppendLine(" |");
        }

        builder.AppendLine();
    }

    private static void AppendRawDetails(StringBuilder builder, LogEvent selectedEvent)
    {
        builder.AppendLine("## Детали выбранного события");
        AppendCodeBlock(builder, selectedEvent.RawText);

        if (!string.IsNullOrWhiteSpace(selectedEvent.Exception))
        {
            builder.AppendLine("### Exception");
            AppendCodeBlock(builder, selectedEvent.Exception);
        }
    }

    private static string BuildFoundCount(IncidentMarkdownReportRequest request)
    {
        if (request.CorrelatedTotalCount > request.CorrelatedEvents.Count)
        {
            return $"показано {request.CorrelatedEvents.Count} из {request.CorrelatedTotalCount} событий";
        }

        return $"{request.CorrelatedEvents.Count} событий, {request.CorrelationGroups.Count} групп";
    }

    private static string BuildHttpSummary(LogEvent item)
    {
        if (string.IsNullOrWhiteSpace(item.HttpMethod) && item.StatusCode == 0)
        {
            return string.Empty;
        }

        return $"{item.StatusCode} {item.HttpMethod} {item.Url}".Trim();
    }

    private static void AppendListItem(StringBuilder builder, string name, string? value)
    {
        builder.Append("- **").Append(name).Append(":** ").AppendLine(value ?? string.Empty);
    }

    private static void AppendCodeBlock(StringBuilder builder, string value)
    {
        builder.AppendLine("```text");
        builder.AppendLine(value.Replace("```", "` ` `", StringComparison.Ordinal));
        builder.AppendLine("```");
        builder.AppendLine();
    }

    private static string Cell(string? value)
    {
        return (value ?? string.Empty)
            .ReplaceLineEndings(" ")
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Trim();
    }

    private static string BuildFileName(string projectName, DateTimeOffset timestampUtc)
    {
        var slug = string.Concat(projectName
            .Select(static item => char.IsLetterOrDigit(item) ? char.ToLowerInvariant(item) : '-'))
            .Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "incident";
        }

        return $"{slug}-{timestampUtc:yyyyMMdd-HHmmss}-report.md";
    }
}
