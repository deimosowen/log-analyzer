namespace LogAnalyzer.Application.Projects;

public sealed record ProjectShareInvitePreview(
    string Token,
    string ProjectId,
    string ProjectName,
    string? ProjectDescription,
    string SharerDisplayName,
    string SharerEmail,
    DateTimeOffset CreatedAtUtc);
