namespace LogAnalyzer.Web.Reporting;

public static class ReportFileName
{
    private const string DefaultBaseName = "incident-report";
    private const string PdfExtension = ".pdf";

    public static string NormalizePdf(string? value)
    {
        var normalizedValue = value?.Replace('\\', '/');
        var fileName = Path.GetFileName(normalizedValue);
        var baseName = Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = DefaultBaseName;
        }

        var invalidCharacters = Path.GetInvalidFileNameChars()
            .Append(Path.DirectorySeparatorChar)
            .Append(Path.AltDirectorySeparatorChar)
            .Append('\\')
            .Append('/')
            .ToHashSet();
        var safeBaseName = new string(baseName
            .Select(character => invalidCharacters.Contains(character) || char.IsControl(character) ? '-' : character)
            .ToArray())
            .Trim(' ', '.', '-');

        if (string.IsNullOrWhiteSpace(safeBaseName))
        {
            safeBaseName = DefaultBaseName;
        }

        return safeBaseName + PdfExtension;
    }
}
