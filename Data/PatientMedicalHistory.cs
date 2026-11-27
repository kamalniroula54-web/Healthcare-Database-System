using System.ComponentModel.DataAnnotations;

namespace MK_Hospital.Data;

public sealed class PatientMedicalHistory
{
	[Required, StringLength(2000)]
	public string ChiefComplaint { get; set; } = string.Empty;

	[StringLength(4000)]
	public string Onset { get; set; } = string.Empty;

	[StringLength(4000)]
	public string Location { get; set; } = string.Empty;

	[StringLength(4000)]
	public string Duration { get; set; } = string.Empty;

	[StringLength(4000)]
	public string Character { get; set; } = string.Empty;

	[StringLength(4000)]
	public string AggravatingFactors { get; set; } = string.Empty;

	[StringLength(4000)]
	public string RelievingFactors { get; set; } = string.Empty;

	[StringLength(4000)]
	public string Timing { get; set; } = string.Empty;

	[Range(1, 10)]
	public int? Severity { get; set; }

	[StringLength(4000)]
	public string ChronicIllnesses { get; set; } = string.Empty;

	[StringLength(4000)]
	public string PastHospitalizations { get; set; } = string.Empty;

	[StringLength(4000)]
	public string PastInjuriesTrauma { get; set; } = string.Empty;

	[StringLength(4000)]
	public string PastSurgicalHistory { get; set; } = string.Empty;

	[StringLength(4000)]
	public string CurrentMedications { get; set; } = string.Empty;

	[StringLength(4000)]
	public string DrugAllergies { get; set; } = string.Empty;

	[StringLength(4000)]
	public string FoodEnvironmentalAllergies { get; set; } = string.Empty;

	[StringLength(4000)]
	public string FamilyHistoryFather { get; set; } = string.Empty;

	[StringLength(4000)]
	public string FamilyHistoryMother { get; set; } = string.Empty;

	[StringLength(4000)]
	public string FamilyHistorySiblings { get; set; } = string.Empty;

	[StringLength(4000)]
	public string NotableHereditaryConditions { get; set; } = string.Empty;

	[StringLength(4000)]
	public string OccupationPhysicalDemands { get; set; } = string.Empty;

	[StringLength(4000)]
	public string TobaccoUse { get; set; } = string.Empty;

	[StringLength(4000)]
	public string AlcoholUse { get; set; } = string.Empty;

	[StringLength(4000)]
	public string SubstanceUse { get; set; } = string.Empty;

	[StringLength(4000)]
	public string ExerciseAndDiet { get; set; } = string.Empty;

	[StringLength(4000)]
	public string LivingSituationSupportSystem { get; set; } = string.Empty;

	[StringLength(4000)]
	public string Immunizations { get; set; } = string.Empty;

	[StringLength(4000)]
	public string RosGeneralConstitutional { get; set; } = string.Empty;

	[StringLength(4000)]
	public string RosCardiovascular { get; set; } = string.Empty;

	[StringLength(4000)]
	public string RosRespiratory { get; set; } = string.Empty;

	[StringLength(4000)]
	public string RosGastrointestinal { get; set; } = string.Empty;

	[StringLength(4000)]
	public string RosGenitourinary { get; set; } = string.Empty;

	[StringLength(4000)]
	public string RosMusculoskeletal { get; set; } = string.Empty;

	[StringLength(4000)]
	public string RosNeurological { get; set; } = string.Empty;

	[StringLength(4000)]
	public string AdditionalNotes { get; set; } = string.Empty;
}
