using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Patients;

public sealed class EditModel(IPatientRepository patients) : PageModel
{
	[BindProperty]
	public PatientForm Input { get; set; } = new();

	public int PatientId { get; private set; }

	public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
	{
		PatientRecord? patient = await patients.GetByIdAsync(id, cancellationToken);
		if (patient is null)
			return NotFound();

		PatientId = patient.PatientId;
		Input = new PatientForm
		{
			FirstName = patient.FirstName,
			LastName = patient.LastName,
			Gender = patient.Gender,
			DateOfBirth = patient.DateOfBirth,
			Address = patient.Address,
			Phone = patient.Phone,
			AdmissionDate = patient.AdmissionDate,
			DischargeDate = patient.DischargeDate,
			Status = patient.Status
		};
		return Page();
	}

	public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
	{
		PatientId = id;
		if (!ModelState.IsValid)
			return Page();

		bool updated = await patients.UpdateAsync(id, Input, cancellationToken);
		if (!updated)
			return NotFound();

		TempData["Message"] = "Patient updated.";
		return RedirectToPage("Index");
	}
}
