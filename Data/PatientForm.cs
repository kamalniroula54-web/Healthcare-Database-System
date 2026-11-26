using System.ComponentModel.DataAnnotations;

namespace MK_Hospital.Data;

public sealed class PatientForm
{
	[Required, StringLength(10)]
	public string FirstName { get; set; } = string.Empty;

	[Required, StringLength(10)]
	public string LastName { get; set; } = string.Empty;

	[Required, StringLength(10)]
	public string Gender { get; set; } = string.Empty;

	[DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime DateOfBirth { get; set; }

	[Required]
	public string Address { get; set; } = string.Empty;

	[Required, StringLength(10)]
	public string Phone { get; set; } = string.Empty;

	[DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime AdmissionDate { get; set; }

	[DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime? DischargeDate { get; set; }

	[Range(1, 2)]
	public int Status { get; set; }
}
