using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Admin)]
public sealed class AdministratorsModel(
	IUserAccountService accounts,
	IOutlookMailSender mailSender) : PageModel
{
	[BindProperty]
	public AdministratorForm Input { get; set; } = new();

	public IReadOnlyList<EmployeeAccountSummary> Administrators { get; private set; } = [];

	[TempData]
	public string? PasswordUsername { get; set; }

	[TempData]
	public string? TemporaryPassword { get; set; }

	[TempData]
	public string? PasswordAction { get; set; }

	[TempData]
	public string? PasswordDeliveryError { get; set; }

	public async Task OnGetAsync(CancellationToken cancellationToken)
	{
		Administrators = await accounts.GetAdministratorsAsync(cancellationToken);
	}

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
		{
			await LoadAdministratorsAsync(cancellationToken);
			return Page();
		}

		string? temporaryPassword = await accounts.CreateAdministratorAsync(Input.Username, cancellationToken);
		if (temporaryPassword is null)
		{
			ModelState.AddModelError(nameof(Input.Username), "That username is already in use.");
			await LoadAdministratorsAsync(cancellationToken);
			return Page();
		}

		PasswordUsername = Input.Username.Trim();
		TemporaryPassword = temporaryPassword;
		PasswordAction = "created";
		return RedirectToPage();
	}

	public async Task<IActionResult> OnPostResetPasswordAsync(string username, CancellationToken cancellationToken)
	{
		string adminUsername = User.Identity?.Name
			?? throw new InvalidOperationException("An authenticated administrator username is required.");
		string? temporaryPassword = await accounts.ResetAdministratorPasswordAsync(
			username,
			adminUsername,
			cancellationToken);
		if (temporaryPassword is null)
		{
			TempData["Message"] = "Ask another administrator to reset your account, or confirm the target exists and has no pending reset request.";
			return RedirectToPage();
		}

		EmployeeAccountSummary? target = (await accounts.GetAdministratorsAsync(cancellationToken))
			.FirstOrDefault(account => string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase));
		if (target is not null && !string.IsNullOrWhiteSpace(target.Profile.Email))
		{
			try
			{
				await mailSender.SendPasswordResetAsync(
					target.Profile.Email,
					target.Username,
					target.Profile.FirstName,
					temporaryPassword,
					cancellationToken);
				TempData["Message"] = $"A one-time password was sent to {target.Profile.Email}.";
				return RedirectToPage();
			}
			catch (OutlookMailException exception)
			{
				PasswordDeliveryError = exception.ErrorCode;
			}
		}
		else
		{
			PasswordDeliveryError = "EmailAddressMissing";
		}

		PasswordUsername = username;
		TemporaryPassword = temporaryPassword;
		PasswordAction = "reset";
		return RedirectToPage();
	}

	private async Task LoadAdministratorsAsync(CancellationToken cancellationToken)
	{
		Administrators = await accounts.GetAdministratorsAsync(cancellationToken);
	}
}

public sealed class AdministratorForm
{
	[Required, StringLength(64, MinimumLength = 3)]
	public string Username { get; set; } = string.Empty;
}
