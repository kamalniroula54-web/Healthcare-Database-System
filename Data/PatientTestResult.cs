namespace MK_Hospital.Data;

public sealed record PatientTestResult(
	int TestResultId,
	int PatientId,
	int HospitalId,
	int? CheckupId,
	DateTime TestDate,
	string TestName,
	string Result,
	string Unit,
	string ReferenceRange,
	string Notes,
	string CreatedBy,
	DateTime CreatedAtUtc,
	string UpdatedBy,
	DateTime UpdatedAtUtc);
