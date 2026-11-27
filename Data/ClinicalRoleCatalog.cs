namespace MK_Hospital.Data;

public sealed record ClinicalRoleGroup(string Name, IReadOnlyList<string> Roles);

public static class ClinicalRoleCatalog
{
	public static IReadOnlyList<ClinicalRoleGroup> Groups { get; } =
	[
		new("Medical Providers",
		[
			"Attending Physician",
			"Specialist Physician (Surgeon, Cardiologist, Oncologist, etc.)",
			"Hospitalist",
			"Resident Physician",
			"Fellow",
			"Medical Intern",
			"Physician Assistant (PA)"
		]),
		new("Nursing Staff",
		[
			"Nurse Practitioner (NP)",
			"Certified Registered Nurse Anesthetist (CRNA)",
			"Certified Nurse-Midwife (CNM)",
			"Clinical Nurse Specialist (CNS)",
			"Registered Nurse (RN)",
			"Licensed Practical Nurse (LPN) / Licensed Vocational Nurse (LVN)",
			"Certified Nursing Assistant (CNA)",
			"Patient Care Technician (PCT)"
		]),
		new("Therapy & Rehabilitation",
		[
			"Respiratory Therapist (RT)",
			"Physical Therapist (PT)",
			"Physical Therapist Assistant (PTA)",
			"Occupational Therapist (OT)",
			"Occupational Therapy Assistant (OTA)",
			"Speech-Language Pathologist (SLP)"
		]),
		new("Diagnostics, Pharmacy, & Clinical Support",
		[
			"Clinical Pharmacist",
			"Pharmacy Technician",
			"Radiologic Technologist (X-Ray)",
			"MRI Technologist",
			"CT Technologist",
			"Diagnostic Medical Sonographer (Ultrasound)",
			"Nuclear Medicine Technologist",
			"Medical Laboratory Scientist (MLS)",
			"Medical Laboratory Technician (MLT)",
			"Phlebotomist",
			"Surgical Technologist",
			"Emergency Medical Technician (EMT) / Paramedic",
			"Registered Dietitian (RD)",
			"Medical Social Worker",
			"Clinical Case Manager"
		])
	];

	public static bool IsValid(string? role) =>
		!string.IsNullOrWhiteSpace(role) &&
		Groups.SelectMany(group => group.Roles)
			.Contains(role.Trim(), StringComparer.Ordinal);
}
