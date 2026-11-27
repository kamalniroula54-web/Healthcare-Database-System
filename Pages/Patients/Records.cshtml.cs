using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Patients;

[Authorize(Roles = UserRoles.Staff)]
public sealed class RecordsModel(
	IPatientRepository patients,
	IPatientPortalService patientPortal,
	CurrentHospitalContext currentHospital) : PageModel
{
	[BindProperty]
	public PatientCheckupForm CheckupInput { get; set; } = new();

	[BindProperty]
	public PatientTestResultForm TestResultInput { get; set; } = new();

	public PatientSummary? Patient { get; private set; }
	public PatientRecord? PatientDetails { get; private set; }
	public IReadOnlyList<PatientCheckup> Checkups { get; private set; } = [];
	public IReadOnlyList<PatientTestResult> TestResults { get; private set; } = [];
	public bool IsReadOnly { get; private set; }
	public bool CanAddVisits { get; private set; }
	public bool CanCreatePortalCode { get; private set; }
	public string? PortalRegistrationCode { get; private set; }
	public string? PortalCodeMessage { get; private set; }
	public int CurrentHospitalId => currentHospital.HospitalId;

	public async Task<IActionResult> OnGetAsync(
		int patientId,
		int? editCheckupId,
		int? editTestResultId,
		CancellationToken cancellationToken)
	{
		CheckupInput.CheckupDate = DateTime.Today;
		TestResultInput.TestDate = DateTime.Today;
		if (!await LoadRecordsAsync(patientId, cancellationToken))
			return NotFound();

		if (editTestResultId.HasValue)
		{
			PatientTestResult? result = TestResults.FirstOrDefault(item => item.TestResultId == editTestResultId);
			if (result is null || result.HospitalId != currentHospital.HospitalId)
				return NotFound();

			if (result.CheckupId.HasValue)
				editCheckupId = result.CheckupId;
			else
			{
				TestResultInput = new PatientTestResultForm
				{
					Id = result.TestResultId,
					TestDate = result.TestDate,
					TestName = result.TestName,
					Result = result.Result,
					Unit = result.Unit,
					ReferenceRange = result.ReferenceRange,
					Notes = result.Notes
				};
			}
		}

		if (editCheckupId.HasValue)
		{
			PatientCheckup? checkup = Checkups.FirstOrDefault(item => item.CheckupId == editCheckupId);
			if (checkup is null || checkup.HospitalId != currentHospital.HospitalId)
				return NotFound();

			CheckupInput = new PatientCheckupForm
			{
				Id = checkup.CheckupId,
				CheckupDate = checkup.CheckupDate,
				History = checkup.History,
				FollowUpDate = checkup.FollowUpDate,
				Tests = TestResults
					.Where(result => result.CheckupId == checkup.CheckupId)
					.Select(result => new PatientTestResultForm
					{
						Id = result.TestResultId,
						TestDate = result.TestDate,
						TestName = result.TestName,
						Result = result.Result,
						Unit = result.Unit,
						ReferenceRange = result.ReferenceRange,
						Notes = result.Notes
					})
					.ToList()
			};
		}

		return Page();
	}

	public async Task<IActionResult> OnPostSaveCheckupAsync(
		int patientId,
		CancellationToken cancellationToken)
	{
		if (!await CanAccessPatientAsync(patientId, cancellationToken))
			return Forbid();

		RemoveModelStateFor(nameof(TestResultInput));
		CheckupInput.History ??= new PatientMedicalHistory();
		CheckupInput.Tests ??= [];
		if (CheckupInput.Tests.Count > PatientCheckupForm.MaxTestsPerVisit)
		{
			ModelState.AddModelError(
				$"{nameof(CheckupInput)}.{nameof(CheckupInput.Tests)}",
				$"A visit can include at most {PatientCheckupForm.MaxTestsPerVisit} tests.");
		}

		if (!ModelState.IsValid)
		{
			if (!await LoadRecordsAsync(patientId, cancellationToken))
				return NotFound();
			return Page();
		}

		string? username = User.Identity?.Name;
		if (string.IsNullOrWhiteSpace(username))
			return Forbid();

		if (!await patients.SaveCheckupAsync(patientId, CheckupInput, username, cancellationToken))
			return NotFound();

		TempData["Message"] = CheckupInput.Id == 0
			? "Patient medical history saved."
			: "Patient medical history updated.";
		return RedirectToPage(new { patientId });
	}

	public async Task<IActionResult> OnPostSaveTestResultAsync(
		int patientId,
		CancellationToken cancellationToken)
	{
		if (!await CanAccessPatientAsync(patientId, cancellationToken))
			return Forbid();

		RemoveModelStateFor(nameof(CheckupInput));
		if (TestResultInput.Id == 0)
			return BadRequest();

		if (!ModelState.IsValid)
		{
			if (!await LoadRecordsAsync(patientId, cancellationToken))
				return NotFound();
			return Page();
		}

		string? username = User.Identity?.Name;
		if (string.IsNullOrWhiteSpace(username))
			return Forbid();

		if (!await patients.SaveTestResultAsync(patientId, TestResultInput, username, cancellationToken))
			return NotFound();

		TempData["Message"] = "Legacy test result updated.";
		return RedirectToPage(new { patientId });
	}

	public async Task<IActionResult> OnPostDeleteCheckupAsync(
		int patientId,
		int id,
		CancellationToken cancellationToken)
	{
		if (!await CanAccessPatientAsync(patientId, cancellationToken))
			return Forbid();

		if (!await patients.DeleteCheckupAsync(patientId, id, cancellationToken))
			return NotFound();

		TempData["Message"] = "Visit deleted.";
		return RedirectToPage(new { patientId });
	}

	public async Task<IActionResult> OnPostDeleteTestResultAsync(
		int patientId,
		int id,
		CancellationToken cancellationToken)
	{
		if (!await CanAccessPatientAsync(patientId, cancellationToken))
			return Forbid();

		if (!await patients.DeleteTestResultAsync(patientId, id, cancellationToken))
			return NotFound();

		TempData["Message"] = "Test result deleted.";
		return RedirectToPage(new { patientId });
	}

	public async Task<IActionResult> OnPostCreatePortalRegistrationCodeAsync(
		int patientId,
		CancellationToken cancellationToken)
	{
		if (!await LoadRecordsAsync(patientId, cancellationToken))
			return NotFound();

		CheckupInput.CheckupDate = DateTime.Today;
		TestResultInput.TestDate = DateTime.Today;
		if (!CanCreatePortalCode)
			return Forbid();

		string? username = User.Identity?.Name;
		if (string.IsNullOrWhiteSpace(username))
			return Forbid();

		PortalRegistrationCode =
			await patientPortal.CreateRegistrationCodeAsync(patientId, username, cancellationToken);
		PortalCodeMessage = PortalRegistrationCode is null
			? "A code could not be issued. This patient record may already be linked to a portal account."
			: "Give this one-time code to the verified patient through a secure channel. It expires in seven days.";
		return Page();
	}

	private async Task<bool> LoadRecordsAsync(int patientId, CancellationToken cancellationToken)
	{
		Patient = await patients.GetSummaryByIdAsync(patientId, cancellationToken);
		if (Patient is null)
			return false;

		PatientDetails = await patients.GetByIdAsync(patientId, cancellationToken);
		if (PatientDetails is null)
			return false;

		Checkups = await patients.GetCheckupsAsync(patientId, cancellationToken);
		TestResults = await patients.GetTestResultsAsync(patientId, cancellationToken);
		IsReadOnly = Patient.HospitalId != currentHospital.HospitalId;
		CanAddVisits = true;
		CanCreatePortalCode =
			(currentHospital.IsAdministrator || currentHospital.IsEmployee) &&
			Patient.HospitalId == currentHospital.HospitalId;
		return true;
	}

	private async Task<bool> CanAccessPatientAsync(int patientId, CancellationToken cancellationToken)
	{
		return await patients.GetSummaryByIdAsync(patientId, cancellationToken) is not null;
	}

	private void RemoveModelStateFor(string prefix)
	{
		string propertyPrefix = $"{prefix}.";
		foreach (string key in ModelState.Keys
			.Where(key => key.StartsWith(propertyPrefix, StringComparison.Ordinal))
			.ToArray())
		{
			ModelState.Remove(key);
		}
	}
}
