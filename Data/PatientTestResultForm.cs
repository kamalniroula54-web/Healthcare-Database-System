using System.ComponentModel.DataAnnotations;

namespace MK_Hospital.Data;

public sealed class PatientTestResultForm
{
	public int Id { get; set; }

	[Required, DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime TestDate { get; set; } = DateTime.Today;

	[Required, StringLength(200)]
	public string TestName { get; set; } = string.Empty;

	[Required, StringLength(1000)]
	public string Result { get; set; } = string.Empty;

	[StringLength(64)]
	public string Unit { get; set; } = string.Empty;

	[StringLength(200)]
	public string ReferenceRange { get; set; } = string.Empty;

	[StringLength(4000)]
	public string Notes { get; set; } = string.Empty;
}
