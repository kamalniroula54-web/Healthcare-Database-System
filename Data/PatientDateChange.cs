namespace MK_Hospital.Data;

public sealed record PatientDateChange(
	int Id,
	int PatientId,
	string ChangedBy,
	DateTime PreviousAdmissionDate,
	DateTime NewAdmissionDate,
	DateTime? PreviousDischargeDate,
	DateTime? NewDischargeDate,
	DateTime ChangedAtUtc);

public sealed class PatientDateUpdate
{
	public DateTime AdmissionDate { get; set; }
	public DateTime? DischargeDate { get; set; }
}
