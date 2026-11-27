using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Administrators)]
public sealed class EmployeesModel(
	IUserAccountService accounts,
	IOutlookMailSender mailSender,
	CurrentHospitalContext currentHospital,
	IHospitalService hospitals) : PageModel
{
	[BindProperty]
	public EmployeeForm Input { get; set; } = new();

	public IReadOnlyList<EmployeeAccountSummary> Employees { get; private set; } = [];
	public string CurrentHospitalName { get; private set; } = string.Empty;
	public string? PasswordDeliveryPhone { get; private set; }
	public bool IsGlobalAdministrator => currentHospital.IsAdministrator;

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
		await LoadEmployeesAsync(cancellationToken);
		if (PasswordAction == "reset" && PasswordUsername is not null)
		{
			PasswordDeliveryPhone = Employees
				.FirstOrDefault(employee =>
					string.Equals(employee.Username, PasswordUsername, StringComparison.OrdinalIgnoreCase))
				?.Profile.Phone;
		}
	}

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
		{
			await LoadEmployeesAsync(cancellationToken);
			return Page();
		}

		string? temporaryPassword = await accounts.CreateEmployeeAsync(
			Input.Username,
			currentHospital.HospitalId,
			cancellationToken);
		if (temporaryPassword is null)
		{
			ModelState.AddModelError(nameof(Input.Username), "That username is already in use.");
			await LoadEmployeesAsync(cancellationToken);
			return Page();
		}

		PasswordUsername = Input.Username.Trim();
		TemporaryPassword = temporaryPassword;
		PasswordAction = "created";
		return RedirectToPage();
	}

	public async Task<IActionResult> OnPostResetPasswordAsync(string username, CancellationToken cancellationToken)
	{
		int? hospitalId = currentHospital.IsHospitalAdministrator ? currentHospital.HospitalId : null;
		string? temporaryPassword = await accounts.ResetEmployeePasswordAsync(
			username,
			hospitalId,
			includeHospitalAdmins: false,
			cancellationToken);
		if (temporaryPassword is null)
		{
			TempData["Message"] = "That employee account could not be found or already has a pending reset request.";
			return RedirectToPage();
		}

		EmployeeAccountSummary? employee = (await accounts.GetEmployeesAsync(hospitalId, cancellationToken))
			.FirstOrDefault(account => string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase));
		if (employee is not null && !string.IsNullOrWhiteSpace(employee.Profile.Email))
		{
			try
			{
				await mailSender.SendPasswordResetAsync(
					employee.Profile.Email,
					employee.Username,
					employee.Profile.FirstName,
					temporaryPassword,
					cancellationToken);
				TempData["Message"] = $"A one-time password was sent to {employee.Profile.Email}.";
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

	private async Task LoadEmployeesAsync(CancellationToken cancellationToken)
	{
		int? hospitalId = currentHospital.IsHospitalAdministrator ? currentHospital.HospitalId : null;
		Employees = await accounts.GetEmployeesAsync(hospitalId, cancellationToken);
		HospitalSummary? hospital = await hospitals.GetByIdAsync(currentHospital.HospitalId, cancellationToken);
		CurrentHospitalName = hospital?.Name ?? throw new InvalidOperationException("The active hospital is unavailable.");
	}
}

public sealed class EmployeeForm
{
	[Required, StringLength(64, MinimumLength = 3)]
	public string Username { get; set; } = string.Empty;
}
