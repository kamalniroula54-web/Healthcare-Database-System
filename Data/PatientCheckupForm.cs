using System.ComponentModel.DataAnnotations;

namespace MK_Hospital.Data;

public sealed class PatientCheckupForm
{
	public const int MaxTestsPerVisit = 50;

	public int Id { get; set; }

	[Required, DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime CheckupDate { get; set; } = DateTime.Today;

	[Required]
	public PatientMedicalHistory History { get; set; } = new();

	[DataType(DataType.Date), Range(typeof(DateTime), "1900-01-01", "2100-12-31")]
	public DateTime? FollowUpDate { get; set; }

	public List<PatientTestResultForm> Tests { get; set; } = [];
}
