using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace MK_Hospital
{
	public partial class Form1 : Form
	{
		private const string ConnectionString =
			"Server=(localdb)\\ProjectModels;Database=PatientsDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True";

		public Form1()
		{
			InitializeComponent();
		}

		private void ClearForm()
		{
			textBox7.Clear();
			textBox8.Clear();
			textBox9.Clear();
			textBox10.Clear();
			textBox11.Clear();
			textBox12.Clear();
			textBox13.Clear();
			textBox14.Clear();
			textBox15.Clear();

			admitted.Checked = false;
			discharged.Checked = false;
		}

		private bool TryGetPatient(out DateTime dob, out DateTime admissionDate, out DateTime? dischargeDate)
		{
			dob = default;
			admissionDate = default;
			dischargeDate = null;

			if (string.IsNullOrWhiteSpace(textBox7.Text) ||
				string.IsNullOrWhiteSpace(textBox8.Text) ||
				string.IsNullOrWhiteSpace(textBox9.Text) ||
				string.IsNullOrWhiteSpace(textBox10.Text) ||
				string.IsNullOrWhiteSpace(textBox11.Text) ||
				string.IsNullOrWhiteSpace(textBox12.Text) ||
				string.IsNullOrWhiteSpace(textBox13.Text))
			{
				MessageBox.Show("Please complete all required patient fields.");
				return false;
			}

			if (!DateTime.TryParse(textBox10.Text.Trim(), out dob))
			{
				MessageBox.Show("Enter a valid Date of Birth.");
				return false;
			}

			if (!DateTime.TryParse(textBox13.Text.Trim(), out admissionDate))
			{
				MessageBox.Show("Enter a valid Admission Date.");
				return false;
			}

			if (!string.IsNullOrWhiteSpace(textBox14.Text))
			{
				if (!DateTime.TryParse(textBox14.Text.Trim(), out DateTime parsedDischargeDate))
				{
					MessageBox.Show("Enter a valid Discharge Date or leave it blank.");
					return false;
				}
				dischargeDate = parsedDischargeDate;
			}

			if (!admitted.Checked && !discharged.Checked)
			{
				MessageBox.Show("Select Admitted or Discharged.");
				return false;
			}

			return true;
		}

		private int GetSelectedRadioValue()
		{
			if (admitted.Checked)
				return 1;
			if (discharged.Checked)
				return 2;
			return 0;
		}

		private void AddPatientParameters(SqlCommand command, DateTime dob, DateTime admissionDate, DateTime? dischargeDate)
		{
			command.Parameters.Add("@Param1", SqlDbType.NVarChar, 10).Value = textBox7.Text.Trim();
			command.Parameters.Add("@Param2", SqlDbType.NVarChar, 10).Value = textBox8.Text.Trim();
			command.Parameters.Add("@Param3", SqlDbType.NVarChar, 10).Value = textBox9.Text.Trim();
			command.Parameters.Add("@Param4", SqlDbType.Date).Value = dob.Date;
			command.Parameters.Add("@Param5", SqlDbType.NVarChar).Value = textBox11.Text.Trim();
			command.Parameters.Add("@Param6", SqlDbType.NVarChar, 10).Value = textBox12.Text.Trim();
			command.Parameters.Add("@Param7", SqlDbType.Date).Value = admissionDate.Date;
			command.Parameters.Add("@Param8", SqlDbType.Date).Value =
				dischargeDate.HasValue ? dischargeDate.Value.Date : DBNull.Value;
			command.Parameters.Add("@Param9", SqlDbType.Int).Value = GetSelectedRadioValue();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!TryGetPatient(out DateTime dob, out DateTime admissionDate, out DateTime? dischargeDate))
				return;

			try
			{
				using SqlConnection connection = new(ConnectionString);
				using SqlCommand command = connection.CreateCommand();

				command.CommandText =
					"INSERT INTO dbo.Patients ([FirstName], [LastName], [Gender], [DOB], [Address], [Phone], [AdmissionDate], [DischargeDate], [Status]) " +
					"VALUES (@Param1, @Param2, @Param3, @Param4, @Param5, @Param6, @Param7, @Param8, @Param9)";
				AddPatientParameters(command, dob, admissionDate, dischargeDate);

				connection.Open();
				command.ExecuteNonQuery();
				MessageBox.Show("Patient was added to the database.");
				ClearForm();
			}
			catch (SqlException ex)
			{
				MessageBox.Show("Database Error: " + ex.Message);
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			DataTable data = GetDataFromDatabase();
			Form2 form2 = new(data);
			form2.Show();
		}

		private DataTable GetDataFromDatabase()
		{
			DataTable dataTable = new();

			try
			{
				using SqlConnection connection = new(ConnectionString);
				using SqlCommand command = connection.CreateCommand();
				command.CommandText =
					"SELECT [Patient ID] AS PatientID, [FirstName], [LastName], [Gender], [DOB], [Address], [Phone], [AdmissionDate], [DischargeDate], [Status] " +
					"FROM dbo.Patients";
				using SqlDataAdapter adapter = new(command);

				connection.Open();
				adapter.Fill(dataTable);
			}
			catch (SqlException ex)
			{
				MessageBox.Show("Database Error: " + ex.Message);
			}

			return dataTable;
		}

		private void button3_Click_1(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox15.Text.Trim(), out int patientID))
			{
				MessageBox.Show("Invalid Patient ID. Please enter a valid integer.");
				return;
			}

			if (!TryGetPatient(out DateTime dob, out DateTime admissionDate, out DateTime? dischargeDate))
				return;

			try
			{
				using SqlConnection connection = new(ConnectionString);
				using SqlCommand command = connection.CreateCommand();
				command.CommandText =
					"UPDATE dbo.Patients SET [FirstName]=@Param1, [LastName]=@Param2, [Gender]=@Param3, [DOB]=@Param4, " +
					"[Address]=@Param5, [Phone]=@Param6, [AdmissionDate]=@Param7, [DischargeDate]=@Param8, [Status]=@Param9 " +
					"WHERE [Patient ID]=@PatientID";
				AddPatientParameters(command, dob, admissionDate, dischargeDate);
				command.Parameters.Add("@PatientID", SqlDbType.Int).Value = patientID;

				connection.Open();
				int rowsAffected = command.ExecuteNonQuery();

				MessageBox.Show(rowsAffected > 0
					? "Patient record updated successfully!"
					: "No matching patient found for the given ID.");

				ClearForm();
			}
			catch (SqlException ex)
			{
				MessageBox.Show("Database Error: " + ex.Message);
			}
		}

		private void button2_Click_1(object sender, EventArgs e)
		{
			if (!int.TryParse(textBox15.Text.Trim(), out int patientID))
			{
				MessageBox.Show("Invalid Patient ID. Please enter a valid integer.");
				return;
			}

			try
			{
				using SqlConnection connection = new(ConnectionString);
				using SqlCommand command = connection.CreateCommand();
				command.CommandText = "DELETE FROM dbo.Patients WHERE [Patient ID]=@PatientID";
				command.Parameters.Add("@PatientID", SqlDbType.Int).Value = patientID;

				connection.Open();
				int rowsAffected = command.ExecuteNonQuery();

				MessageBox.Show(rowsAffected > 0
					? "Patient record deleted successfully!"
					: "No matching patient found for the given ID.");

				ClearForm();
			}
			catch (SqlException ex)
			{
				MessageBox.Show("Database Error: " + ex.Message);
			}
		}
	}
}
