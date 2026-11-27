using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Admin)]
public sealed class PasswordResetRequestsModel(IUserAccountService accounts) : PageModel
{
	public IReadOnlyList<PasswordResetRequest> Requests { get; private set; } = [];

	[TempData]
	public string? ResetUsername { get; set; }

	[TempData]
	public string? ResetFirstName { get; set; }

	[TempData]
	public string? ResetEmail { get; set; }

	[TempData]
	public string? ResetPhone { get; set; }

	[TempData]
	public string? ResetContactMethod { get; set; }

	[TempData]
	public string? ResetDeliveryError { get; set; }

	[TempData]
	public string? TemporaryPassword { get; set; }

	public string? EmailDraftUrl
	{
		get
		{
			if (ResetContactMethod != "Email" || string.IsNullOrWhiteSpace(ResetEmail)
				|| string.IsNullOrWhiteSpace(TemporaryPassword) || string.IsNullOrWhiteSpace(ResetUsername))
				return null;

			string subject = $"MK Hospital password reset for {ResetUsername}";
			string body = $"Hello {ResetFirstName},%0D%0A%0D%0AYour one-time password (OTP) is: {TemporaryPassword}%0D%0A%0D%0ASign in with this OTP and choose a new password. If you did not request this reset, contact your administrator.%0D%0A";
			string recipient = Uri.EscapeDataString(ResetEmail).Replace("%40", "@", StringComparison.OrdinalIgnoreCase);
			return $"mailto:{recipient}?subject={Uri.EscapeDataString(subject)}&body={body}";
		}
	}

	public async Task OnGetAsync(CancellationToken cancellationToken)
	{
		Requests = await accounts.GetPendingPasswordResetRequestsAsync(cancellationToken);
	}

	public async Task<IActionResult> OnPostIssueResetAsync(int id, CancellationToken cancellationToken)
	{
		string adminUsername = User.Identity?.Name
			?? throw new InvalidOperationException("An authenticated administrator username is required.");
		EmployeePasswordReset? reset = await accounts.IssueRequestedPasswordResetAsync(id, adminUsername, cancellationToken);
		if (reset is null)
		{
			TempData["Message"] = "That reset request was already handled or no longer exists.";
			return RedirectToPage();
		}

		ResetUsername = reset.Username;
		ResetFirstName = reset.FirstName;
		ResetEmail = reset.Email;
		ResetPhone = reset.Phone;
		ResetContactMethod = reset.ContactMethod;
		ResetDeliveryError = reset.DeliveryError;
		TemporaryPassword = reset.TemporaryPassword;
		return RedirectToPage();
	}
}
