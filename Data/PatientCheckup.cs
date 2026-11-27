namespace MK_Hospital.Data;

public sealed record PatientCheckup(
	int CheckupId,
	int PatientId,
	int HospitalId,
	string HospitalName,
	DateTime CheckupDate,
	PatientMedicalHistory History,
	DateTime? FollowUpDate,
	string CreatedBy,
	DateTime CreatedAtUtc,
	string UpdatedBy,
	DateTime UpdatedAtUtc);
