namespace MK_Hospital.Data;

public sealed record PatientSummary(
	int PatientId,
	int HospitalId,
	string HospitalName,
	string FirstName,
	string LastName,
	DateTime AdmissionDate,
	DateTime? DischargeDate);
