using System;
using System.Data;
using System.Windows.Forms;

namespace MK_Hospital
{
	public partial class Form2 : Form
	{
		public Form2(DataTable data)
		{
			InitializeComponent();

			// Assuming you have a DataGridView named dataGridView1 on Form2
			dataGridView1.DataSource = data;
			foreach (DataGridViewColumn column in dataGridView1.Columns)
			{
				column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
			}
			dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
		}
		private void button1_Click(object sender, EventArgs e)
		{
			this.Close();
		}

	}
}






