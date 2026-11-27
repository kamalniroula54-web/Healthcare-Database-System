using System.ComponentModel.DataAnnotations;

namespace MK_Hospital.Data;

public sealed class PatientForm : IValidatableObject
{
	[Required, StringLength(10)]
	public string FirstName { get; set; } = string.Empty;

	[Required, StringLength(10)]
	public string LastName { get; set; } = string.Empty;

	[Required, StringLength(10)]
	public string Gender { get; set; } = string.Empty;

	[Required, StringLength(32, MinimumLength = 32)]
	[RegularExpression("^[A-Fa-f0-9]{32}$")]
	public string SharedPatientId { get; set; } = Guid.NewGuid().ToString("N").ToUpperInvariant();

	[DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime DateOfBirth { get; set; }

	[Required, StringLength(200)]
	public string Street { get; set; } = string.Empty;

	[Required, StringLength(100)]
	public string City { get; set; } = string.Empty;

	[Required, StringLength(100)]
	public string State { get; set; } = string.Empty;

	[Required, StringLength(20)]
	public string Zip { get; set; } = string.Empty;

	[Required, StringLength(100)]
	public string Country { get; set; } = string.Empty;

	[Required, StringLength(32)]
	public string Phone { get; set; } = string.Empty;

	[Required, StringLength(2)]
	public string PhoneRegion { get; set; } = PhoneNumberSupport.DefaultRegion;

	[StringLength(100)]
	public string? EmergencyContactName { get; set; }

	[StringLength(32)]
	public string? EmergencyContactPhone { get; set; }

	[Required, StringLength(2)]
	public string EmergencyContactPhoneRegion { get; set; } = PhoneNumberSupport.DefaultRegion;

	[DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime AdmissionDate { get; set; }

	[DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime? DischargeDate { get; set; }

	[Range(1, 2)]
	public int Status { get; set; }

	public string FormatAddress() =>
		string.Join(
			Environment.NewLine,
			Street.Trim(),
			City.Trim(),
			State.Trim(),
			Zip.Trim(),
			Country.Trim());

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (!PhoneNumberSupport.TryNormalize(Phone, PhoneRegion, out _))
			yield return new ValidationResult(
				"Enter a complete phone number that matches the selected country's numbering plan.",
				[nameof(Phone)]);

		if (!string.IsNullOrWhiteSpace(EmergencyContactPhone) &&
			!PhoneNumberSupport.TryNormalize(EmergencyContactPhone, EmergencyContactPhoneRegion, out _))
		{
			yield return new ValidationResult(
				"Enter a complete phone number that matches the selected country's numbering plan.",
				[nameof(EmergencyContactPhone)]);
		}
	}
}
