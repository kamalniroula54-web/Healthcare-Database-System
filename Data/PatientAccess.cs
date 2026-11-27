namespace MK_Hospital.Data;

public sealed record PatientAccessRequestSummary(
	int Id,
	string EmployeeUsername,
	string EmployeeName,
	string EmployeeHospitalName,
	int TargetHospitalId,
	string TargetHospitalName,
	string PatientReference,
	string Reason,
	DateTime RequestedAtUtc,
	string Status,
	string? ResolvedBy,
	DateTime? ResolvedAtUtc,
	int? GrantedPatientId,
	string? GrantedPatientName);

public sealed record PatientAccessGrantSummary(
	int Id,
	string EmployeeUsername,
	string EmployeeName,
	int HospitalId,
	string HospitalName,
	int GrantedHospitalId,
	string GrantedHospitalName,
	int PatientId,
	string PatientName,
	string GrantedBy,
	DateTime GrantedAtUtc,
	string? RevokedBy,
	DateTime? RevokedAtUtc);

public sealed record PatientCandidate(int PatientId, string Name);
