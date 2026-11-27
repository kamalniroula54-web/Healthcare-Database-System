namespace MK_Hospital.Data;

public sealed record PatientPortalUser(string Username, string PasswordHash);

public sealed record PatientPortalAuthenticationState(string Username);

public sealed record PatientPortalVisit(
	PatientCheckup Checkup,
	string CreatedByName,
	string UpdatedByName);

public sealed record PatientPortalTest(
	PatientTestResult Result,
	string HospitalName,
	string CreatedByName,
	string UpdatedByName);

public sealed record PatientPortalRecord(
	PatientSummary Summary,
	PatientRecord Details,
	IReadOnlyList<PatientPortalVisit> Visits,
	IReadOnlyList<PatientPortalTest> Tests);

public enum PatientPortalRegistrationStatus
{
	Succeeded,
	InvalidCode,
	UsernameUnavailable
}
