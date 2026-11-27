using System.ComponentModel.DataAnnotations;

namespace MK_Hospital.Data;

public sealed class EmployeeProfileForm : IValidatableObject
{
	[Required, StringLength(64, MinimumLength = 1)]
	public string FirstName { get; set; } = string.Empty;

	[Required, StringLength(64, MinimumLength = 1)]
	public string LastName { get; set; } = string.Empty;

	[Required, DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime? DateOfBirth { get; set; }

	[Required, EmailAddress, StringLength(254)]
	public string Email { get; set; } = string.Empty;

	[Required, StringLength(32)]
	public string Phone { get; set; } = string.Empty;

	[Required, StringLength(2)]
	public string PhoneRegion { get; set; } = PhoneNumberSupport.DefaultRegion;

	[Required, StringLength(100)]
	public string EmergencyContactName { get; set; } = string.Empty;

	[Required, StringLength(32)]
	public string EmergencyContactPhone { get; set; } = string.Empty;

	[Required, StringLength(2)]
	public string EmergencyContactPhoneRegion { get; set; } = PhoneNumberSupport.DefaultRegion;

	[Range(1, int.MaxValue)]
	public int? HospitalId { get; set; }

	[Required, StringLength(80)]
	public string EmergencyContactRelationship { get; set; } = string.Empty;

	[StringLength(100)]
	public string ClinicalRole { get; set; } = string.Empty;

	public EmployeeProfile ToProfile() =>
		new(
			FirstName.Trim(),
			LastName.Trim(),
			DateOfBirth?.Date,
			Email.Trim(),
			PhoneNumberSupport.NormalizeOrThrow(Phone, PhoneRegion),
			EmergencyContactName.Trim(),
			PhoneNumberSupport.NormalizeOrThrow(EmergencyContactPhone, EmergencyContactPhoneRegion),
			EmergencyContactRelationship.Trim(),
			ClinicalRole.Trim());

	public static EmployeeProfileForm FromProfile(EmployeeProfile profile)
	{
		PhoneNumberInput phone = PhoneNumberSupport.ToInput(profile.Phone);
		PhoneNumberInput emergencyPhone = PhoneNumberSupport.ToInput(profile.EmergencyContactPhone);
		return new EmployeeProfileForm
		{
			FirstName = profile.FirstName,
			LastName = profile.LastName,
			DateOfBirth = profile.DateOfBirth,
			Email = profile.Email,
			Phone = phone.NationalNumber,
			PhoneRegion = phone.RegionCode,
			EmergencyContactName = profile.EmergencyContactName,
			EmergencyContactPhone = emergencyPhone.NationalNumber,
			EmergencyContactPhoneRegion = emergencyPhone.RegionCode,
			EmergencyContactRelationship = profile.EmergencyContactRelationship,
			ClinicalRole = profile.ClinicalRole
		};
	}

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (!PhoneNumberSupport.TryNormalize(Phone, PhoneRegion, out _))
			yield return new ValidationResult(
				"Enter a valid phone number for the selected country.",
				[nameof(Phone)]);

		if (!PhoneNumberSupport.TryNormalize(EmergencyContactPhone, EmergencyContactPhoneRegion, out _))
			yield return new ValidationResult(
				"Enter a valid phone number for the selected country.",
				[nameof(EmergencyContactPhone)]);

		if (!string.IsNullOrWhiteSpace(ClinicalRole) && !ClinicalRoleCatalog.IsValid(ClinicalRole))
			yield return new ValidationResult(
				"Select a clinical position from the list.",
				[nameof(ClinicalRole)]);
	}
}
