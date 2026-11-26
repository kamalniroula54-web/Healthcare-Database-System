namespace MK_Hospital.Data;

public sealed record PatientRecord(
	int PatientId,
	string FirstName,
	string LastName,
	string Gender,
	DateTime DateOfBirth,
	string Address,
	string Phone,
	DateTime AdmissionDate,
	DateTime? DischargeDate,
	int Status);
