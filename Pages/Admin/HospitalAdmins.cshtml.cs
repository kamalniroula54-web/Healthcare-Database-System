using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Admin)]
public sealed class HospitalAdminsModel(
	IUserAccountService accounts,
	IHospitalService hospitals,
	IOutlookMailSender mailSender) : PageModel
{
	[BindProperty]
	public HospitalAdministratorForm Input { get; set; } = new();

	public IReadOnlyList<HospitalSummary> Hospitals { get; private set; } = [];
	public IReadOnlyList<EmployeeAccountSummary> HospitalAdministrators { get; private set; } = [];

	[TempData]
	public string? PasswordUsername { get; set; }

	[TempData]
	public string? TemporaryPassword { get; set; }

	[TempData]
	public string? PasswordAction { get; set; }

	[TempData]
	public string? PasswordDeliveryError { get; set; }

	public async Task OnGetAsync(CancellationToken cancellationToken) =>
		await LoadAsync(cancellationToken);

	public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
	{
		if (!await hospitals.ExistsAsync(Input.HospitalId, cancellationToken))
			ModelState.AddModelError("Input.HospitalId", "Select an active hospital.");

		if (!ModelState.IsValid)
		{
			await LoadAsync(cancellationToken);
			return Page();
		}

		string? temporaryPassword = await accounts.CreateHospitalAdministratorAsync(
			Input.Username,
			Input.HospitalId,
			Input.CanApprovePatientAccess,
			cancellationToken);
		if (temporaryPassword is null)
		{
			ModelState.AddModelError("Input.Username", "That username is already in use.");
			await LoadAsync(cancellationToken);
			return Page();
		}

		PasswordUsername = Input.Username.Trim();
		TemporaryPassword = temporaryPassword;
		PasswordAction = "created";
		return RedirectToPage();
	}

	public async Task<IActionResult> OnPostUpdateAsync(
		string username,
		int hospitalId,
		bool canApprovePatientAccess,
		CancellationToken cancellationToken)
	{
		if (!await hospitals.ExistsAsync(hospitalId, cancellationToken))
		{
			TempData["Message"] = "Select an active hospital.";
			return RedirectToPage();
		}

		if (!await accounts.UpdateHospitalAdministratorAsync(
			username, hospitalId, canApprovePatientAccess, cancellationToken))
		{
			TempData["Message"] = "That hospital administrator could not be updated.";
			return RedirectToPage();
		}

		TempData["Message"] = "Hospital administrator assignment and delegation updated.";
		return RedirectToPage();
	}

	public async Task<IActionResult> OnPostRemoveAsync(
		string username,
		CancellationToken cancellationToken)
	{
		TempData["Message"] = await accounts.RemoveHospitalAdministratorAsync(username, cancellationToken)
			? "Hospital administrator privileges and delegated approval access were removed."
			: "That hospital administrator could not be found.";
		return RedirectToPage();
	}

	public async Task<IActionResult> OnPostResetPasswordAsync(
		string username,
		CancellationToken cancellationToken)
	{
		string? temporaryPassword = await accounts.ResetEmployeePasswordAsync(
			username,
			hospitalId: null,
			includeHospitalAdmins: true,
			cancellationToken);
		if (temporaryPassword is null)
		{
			TempData["Message"] = "That hospital administrator could not be found or already has a pending reset request.";
			return RedirectToPage();
		}

		EmployeeAccountSummary? account = (await accounts.GetHospitalAdministratorsAsync(cancellationToken))
			.FirstOrDefault(candidate =>
				string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase));
		if (account is not null && !string.IsNullOrWhiteSpace(account.Profile.Email))
		{
			try
			{
				await mailSender.SendPasswordResetAsync(
					account.Profile.Email,
					account.Username,
					account.Profile.FirstName,
					temporaryPassword,
					cancellationToken);
				TempData["Message"] = $"A one-time password was sent to {account.Profile.Email}.";
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

	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		Hospitals = await hospitals.GetAllAsync(cancellationToken);
		HospitalAdministrators = await accounts.GetHospitalAdministratorsAsync(cancellationToken);
	}
}

public sealed class HospitalAdministratorForm
{
	[Required, StringLength(64, MinimumLength = 3)]
	public string Username { get; set; } = string.Empty;

	[Range(1, int.MaxValue)]
	public int HospitalId { get; set; }

	public bool CanApprovePatientAccess { get; set; }
}
