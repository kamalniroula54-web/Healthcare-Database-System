using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Patients;

public sealed class IndexModel(IPatientRepository patients) : PageModel
{
	public IReadOnlyList<PatientRecord> Patients { get; private set; } = [];

	public async Task OnGetAsync(CancellationToken cancellationToken)
	{
		Patients = await patients.GetAllAsync(cancellationToken);
	}

	public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
	{
		bool deleted = await patients.DeleteAsync(id, cancellationToken);
		TempData["Message"] = deleted ? "Patient deleted." : "That patient record was not found.";
		return RedirectToPage();
	}
}
