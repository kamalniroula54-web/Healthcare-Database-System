namespace MK_Hospital.Data;

public sealed record UserAccount(
	string Username,
	string PasswordHash,
	string Role,
	bool MustChangePassword,
	bool ProfileComplete,
	int HospitalId,
	bool IsHospitalAdmin,
	bool CanApprovePatientAccess);

public sealed record AccountAuthenticationState(
	string Role,
	bool MustChangePassword,
	bool ProfileComplete,
	int HospitalId,
	bool IsHospitalAdmin,
	bool CanApprovePatientAccess);

public static class UserRoles
{
	public const string Admin = "Admin";
	public const string HospitalAdmin = "HospitalAdmin";
	public const string Employee = "Employee";
	public const string Patient = "Patient";
	public const string Administrators = Admin + "," + HospitalAdmin;
	public const string Staff = Admin + "," + HospitalAdmin + "," + Employee;
}

public static class UserAccountClaims
{
	public const string MustChangePassword = "must_change_password";
	public const string MustCompleteProfile = "must_complete_profile";
	public const string CanApprovePatientAccess = "can_approve_patient_access";
}
