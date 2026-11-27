using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Admin;

[Authorize(Roles = UserRoles.Administrators)]
public sealed class EditEmployeeModel(
	IUserAccountService accounts,
	IHospitalService hospitals,
	CurrentHospitalContext currentHospital) : PageModel
{
	[BindProperty(SupportsGet = true), Required]
	public string Username { get; set; } = string.Empty;

	[BindProperty]
	public EmployeeProfileForm Input { get; set; } = new();

	public IReadOnlyList<HospitalSummary> Hospitals { get; private set; } = [];
	public bool CanAssignHospital => currentHospital.IsAdministrator;
	public string CurrentHospitalName { get; private set; } = string.Empty;

	public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
	{
		int? hospitalScope = currentHospital.IsHospitalAdministrator ? currentHospital.HospitalId : null;
		EmployeeAccountSummary? employee =
			await accounts.GetEmployeeAsync(Username, hospitalScope, cancellationToken);
		if (employee is null)
			return NotFound();

		Input = EmployeeProfileForm.FromProfile(employee.Profile);
		Input.HospitalId = employee.HospitalId;
		HospitalSummary? hospital = await hospitals.GetByIdAsync(currentHospital.HospitalId, cancellationToken);
		CurrentHospitalName = hospital?.Name ?? throw new InvalidOperationException("The active hospital is unavailable.");
		if (CanAssignHospital)
			await LoadHospitalsAsync(cancellationToken);
		return Page();
	}

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		int? hospitalScope = currentHospital.IsHospitalAdministrator ? currentHospital.HospitalId : null;
		int hospitalId = currentHospital.IsAdministrator
			? Input.HospitalId ?? 0
			: currentHospital.HospitalId;
		if (CanAssignHospital && !await hospitals.ExistsAsync(hospitalId, cancellationToken))
		{
			ModelState.AddModelError("Input.HospitalId", "Select an active hospital for this employee.");
		}

		if (Input.DateOfBirth.HasValue && Input.DateOfBirth.Value.Date > DateTime.Today)
			ModelState.AddModelError("Input.DateOfBirth", "Date of birth cannot be in the future.");
		if (string.IsNullOrWhiteSpace(Input.ClinicalRole))
			ModelState.AddModelError("Input.ClinicalRole", "Select the employee's clinical position.");
		if (!ModelState.IsValid)
		{
			HospitalSummary? hospital = await hospitals.GetByIdAsync(currentHospital.HospitalId, cancellationToken);
			CurrentHospitalName = hospital?.Name ?? throw new InvalidOperationException("The active hospital is unavailable.");
			if (CanAssignHospital)
				await LoadHospitalsAsync(cancellationToken);
			return Page();
		}

		EmployeeAccountSummary? employee =
			await accounts.GetEmployeeAsync(Username, hospitalScope, cancellationToken);
		if (employee is null)
			return NotFound();

		bool hospitalChanged = employee.HospitalId != hospitalId;
		if (!await accounts.UpdateEmployeeProfileAsync(
			Username,
			Input.ToProfile(),
			hospitalId,
			hospitalScope,
			cancellationToken))
			return NotFound();

		TempData["Message"] = hospitalChanged
			? $"Profile for {Username} updated. Their previous session will be signed out when it is next used."
			: $"Profile for {Username} updated.";
		return RedirectToPage("/Admin/Employees");
	}

	private async Task LoadHospitalsAsync(CancellationToken cancellationToken) =>
		Hospitals = await hospitals.GetAllAsync(cancellationToken);
}
