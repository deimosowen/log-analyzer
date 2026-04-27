namespace LogAnalyzer.Web.Auth;

public interface ICurrentUserService
{
    Task<CurrentUser> GetAsync(CancellationToken cancellationToken);
}
