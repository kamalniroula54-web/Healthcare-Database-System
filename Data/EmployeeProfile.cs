namespace MK_Hospital.Data;

public sealed record EmployeeProfile(
	string FirstName,
	string LastName,
	DateTime? DateOfBirth,
	string Email,
	string Phone,
	string EmergencyContactName,
	string EmergencyContactPhone,
	string EmergencyContactRelationship,
	string ClinicalRole);

public sealed record EmployeeAccountSummary(
	string Username,
	EmployeeProfile Profile,
	bool ProfileComplete,
	bool MustChangePassword,
	int HospitalId,
	string HospitalName,
	bool IsHospitalAdmin = false,
	bool CanApprovePatientAccess = false);

public sealed record PasswordResetRequest(
	int Id,
	string Username,
	string AccountRole,
	string ContactMethod,
	DateTime RequestedAtUtc,
	string Email,
	string Phone,
	string DeliveryStatus,
	string? DeliveryError);

public sealed record EmployeePasswordReset(
	string Username,
	string FirstName,
	string Email,
	string Phone,
	string ContactMethod,
	string TemporaryPassword,
	string? DeliveryError);

public sealed record EmailPasswordReset(
	int RequestId,
	string Username,
	string FirstName,
	string Email,
	string TemporaryPassword);

public sealed record OutlookConnectionStatus(
	bool IsConfigured,
	bool IsConnected,
	string? ConnectedEmail,
	string? UserCode,
	string? VerificationUrl,
	string? Message,
	string? ErrorCode);
