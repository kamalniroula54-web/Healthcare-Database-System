namespace MK_Hospital
{
	partial class Form2
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form2));
			label1 = new Label();
			button1 = new Button();
			dataGridView1 = new DataGridView();
			((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
			SuspendLayout();
			// 
			// label1
			// 
			label1.AutoSize = true;
			label1.BackColor = Color.Transparent;
			label1.Font = new Font("Lucida Fax", 27.75F, FontStyle.Italic, GraphicsUnit.Point);
			label1.ForeColor = Color.DeepSkyBlue;
			label1.Location = new Point(421, 21);
			label1.Name = "label1";
			label1.Size = new Size(284, 42);
			label1.TabIndex = 0;
			label1.Text = "Patient Details";
			// 
			// button1
			// 
			button1.BackColor = Color.Red;
			button1.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
			button1.ForeColor = Color.White;
			button1.Location = new Point(487, 498);
			button1.Name = "button1";
			button1.Size = new Size(118, 39);
			button1.TabIndex = 2;
			button1.Text = "Exit";
			button1.UseVisualStyleBackColor = false;
			button1.Click += button1_Click;
			// 
			// dataGridView1
			// 
			dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
			dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
			dataGridView1.BackgroundColor = SystemColors.ButtonFace;
			dataGridView1.BorderStyle = BorderStyle.Fixed3D;
			dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			dataGridView1.Location = new Point(25, 84);
			dataGridView1.Name = "dataGridView1";
			dataGridView1.RowHeadersWidth = 51;
			dataGridView1.Size = new Size(1065, 392);
			dataGridView1.TabIndex = 3;
			// 
			// Form2
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			BackColor = Color.White;
			BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
			BackgroundImageLayout = ImageLayout.Stretch;
			ClientSize = new Size(1117, 575);
			Controls.Add(dataGridView1);
			Controls.Add(button1);
			Controls.Add(label1);
			ForeColor = Color.CornflowerBlue;
			Icon = (Icon)resources.GetObject("$this.Icon");
			Name = "Form2";
			Text = "Patients List";
			((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
			ResumeLayout(false);
			PerformLayout();
		}

		private Label label1;
		private Button button1;
		private DataGridView dataGridView1;
	}
}