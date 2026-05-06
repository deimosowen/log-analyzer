using LogAnalyzer.Application;
using LogAnalyzer.Application.Projects;
using LogAnalyzer.Web.Auth;
using Microsoft.AspNetCore.Components;

namespace LogAnalyzer.Web.Components.Pages;

public partial class Invite
{
    private ProjectShareInvitePreview? preview;
    private CurrentUser currentUser = new(string.Empty, string.Empty, string.Empty, false);
    private bool loading = true;
    private bool alreadyMember;
    private bool acceptBusy;
    private string? actionMessage;
    private bool actionError;

    [Parameter] public string Token { get; set; } = string.Empty;

    [Inject] private IMetadataRepository Metadata { get; set; } = null!;
    [Inject] private ICurrentUserService CurrentUserService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private string ProjectHref => preview is null ? "/" : $"/projects/{preview.ProjectId}";

    private string LoginHref =>
        $"/login?returnUrl={Uri.EscapeDataString($"/invite/{Uri.EscapeDataString(Token)}")}";

    protected override async Task OnParametersSetAsync()
    {
        loading = true;
        actionMessage = null;
        actionError = false;
        preview = await Metadata.GetShareInvitePreviewAsync(Token, CancellationToken.None);
        currentUser = await CurrentUserService.GetAsync(CancellationToken.None);
        alreadyMember = false;
        if (preview is not null && currentUser.IsAuthenticated)
        {
            alreadyMember = await Metadata.GetProjectAsync(currentUser.Id, preview.ProjectId, CancellationToken.None) is not null;
        }

        loading = false;
    }

    private async Task AcceptAsync()
    {
        if (preview is null || !currentUser.IsAuthenticated)
        {
            return;
        }

        acceptBusy = true;
        actionMessage = null;
        try
        {
            var result = await Metadata.AcceptShareInviteAsync(currentUser.Id, Token, CancellationToken.None);
            switch (result)
            {
                case ShareInviteAcceptResult.Accepted:
                    Navigation.NavigateTo(ProjectHref);
                    return;
                case ShareInviteAcceptResult.AlreadyHasAccess:
                    alreadyMember = true;
                    actionMessage = "У вас уже есть доступ к этому инциденту.";
                    actionError = false;
                    break;
                case ShareInviteAcceptResult.InviteNotFound:
                    actionMessage = "Приглашение больше не действует.";
                    actionError = true;
                    break;
                case ShareInviteAcceptResult.NotAuthenticated:
                    actionMessage = "Сессия истекла. Войдите снова.";
                    actionError = true;
                    break;
            }
        }
        finally
        {
            acceptBusy = false;
        }
    }

    private void DeclineAsync()
    {
        Navigation.NavigateTo("/", forceLoad: false);
    }
}
