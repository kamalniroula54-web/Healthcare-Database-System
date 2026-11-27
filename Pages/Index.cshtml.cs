using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages;

[Authorize(Roles = UserRoles.Staff)]
public sealed class IndexModel(IUserAccountService accounts) : PageModel
{
	public int PendingPasswordResetCount { get; private set; }

	public string WelcomeName { get; private set; } = string.Empty;

	public async Task OnGetAsync(CancellationToken cancellationToken)
	{
		string? username = User.Identity?.Name;
		if (!string.IsNullOrWhiteSpace(username))
		{
			EmployeeProfile? profile = await accounts.GetProfileAsync(username, cancellationToken);
			WelcomeName = string.IsNullOrWhiteSpace(profile?.FirstName)
				? username
				: profile.FirstName;
		}

		if (User.IsInRole(UserRoles.Admin))
			PendingPasswordResetCount = (await accounts.GetPendingPasswordResetRequestsAsync(cancellationToken)).Count;
	}
}
