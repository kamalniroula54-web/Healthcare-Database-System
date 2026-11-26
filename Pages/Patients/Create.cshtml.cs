using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Patients;

public sealed class CreateModel(IPatientRepository patients) : PageModel
{
	[BindProperty]
	public PatientForm Input { get; set; } = new();

	public void OnGet()
	{
		Input.DateOfBirth = DateTime.Today;
		Input.AdmissionDate = DateTime.Today;
		Input.Status = 1;
	}

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
			return Page();

		await patients.CreateAsync(Input, cancellationToken);
		TempData["Message"] = "Patient added.";
		return RedirectToPage("Index");
	}
}
