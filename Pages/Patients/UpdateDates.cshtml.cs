using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Patients;

[Authorize(Roles = UserRoles.Employee)]
public sealed class UpdateDatesModel(IPatientRepository patients) : PageModel
{
	[BindProperty]
	public PatientDateForm Input { get; set; } = new();

	public int PatientId { get; private set; }

	public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
	{
		PatientDateUpdate? dates = await patients.GetDatesByIdAsync(id, cancellationToken);
		if (dates is null)
			return NotFound();

		PatientId = id;
		Input.AdmissionDate = dates.AdmissionDate;
		Input.DischargeDate = dates.DischargeDate;
		return Page();
	}

	public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
	{
		PatientId = id;
		if (Input.DischargeDate.HasValue && Input.DischargeDate.Value.Date < Input.AdmissionDate.Date)
			ModelState.AddModelError("Input.DischargeDate", "Discharge date cannot be before the admission date.");

		if (!ModelState.IsValid)
			return Page();

		string? changedBy = User.Identity?.Name;
		if (string.IsNullOrWhiteSpace(changedBy))
			return Forbid();

		bool updated = await patients.UpdateDatesAsync(
			id,
			new PatientDateUpdate
			{
				AdmissionDate = Input.AdmissionDate,
				DischargeDate = Input.DischargeDate
			},
			changedBy,
			cancellationToken);
		if (!updated)
			return NotFound();

		TempData["Message"] = "Dates saved. The administrator has been notified of any changes.";
		return RedirectToPage("Index");
	}
}

public sealed class PatientDateForm
{
	[DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime AdmissionDate { get; set; }

	[DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime? DischargeDate { get; set; }
}
