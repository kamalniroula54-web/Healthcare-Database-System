using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using MK_Hospital.Data;

namespace MK_Hospital.Pages.Patients;

[Authorize(Roles = UserRoles.Administrators)]
public sealed class EditModel(
	IPatientRepository patients,
	CurrentHospitalContext currentHospital) : PageModel
{
	[BindProperty]
	public PatientForm Input { get; set; } = new();

	public int PatientId { get; private set; }

	public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
	{
		PatientRecord? patient = await patients.GetByIdAsync(id, cancellationToken);
		if (patient is null || patient.HospitalId != currentHospital.HospitalId)
			return NotFound();

		PatientId = patient.PatientId;
		PhoneNumberInput phone = PhoneNumberSupport.ToInput(patient.Phone);
		PhoneNumberInput emergencyPhone = PhoneNumberSupport.ToInput(patient.EmergencyContactPhone);
		string[] addressLines = patient.Address.Split('\n');
		Input = new PatientForm
		{
			FirstName = patient.FirstName,
			LastName = patient.LastName,
			Gender = patient.Gender,
			DateOfBirth = patient.DateOfBirth,
			Street = addressLines[0].TrimEnd('\r'),
			City = addressLines.Length > 1 ? addressLines[1].TrimEnd('\r') : string.Empty,
			State = addressLines.Length > 2 ? addressLines[2].TrimEnd('\r') : string.Empty,
			Zip = addressLines.Length > 3 ? addressLines[3].TrimEnd('\r') : string.Empty,
			Country = addressLines.Length > 4
				? string.Join(Environment.NewLine, addressLines.Skip(4)).TrimEnd('\r')
				: string.Empty,
			Phone = phone.NationalNumber,
			PhoneRegion = phone.RegionCode,
			EmergencyContactName = patient.EmergencyContactName,
			EmergencyContactPhone = emergencyPhone.NationalNumber,
			EmergencyContactPhoneRegion = emergencyPhone.RegionCode,
			SharedPatientId = patient.SharedPatientId,
			AdmissionDate = patient.AdmissionDate,
			DischargeDate = patient.DischargeDate,
			Status = patient.Status
		};
		return Page();
	}

	public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
	{
		PatientId = id;
		PatientSummary? patient = await patients.GetSummaryByIdAsync(id, cancellationToken);
		if (patient is null || patient.HospitalId != currentHospital.HospitalId)
			return NotFound();

		if (!ModelState.IsValid)
			return Page();

		bool updated = await patients.UpdateAsync(id, Input, cancellationToken);
		if (!updated)
			return NotFound();

		TempData["Message"] = "Patient updated.";
		return RedirectToPage("Index");
	}
}
