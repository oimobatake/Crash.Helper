namespace Crash.Helper.Controls
{
	partial class DataControl
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.masksDownButton = new System.Windows.Forms.Button();
            this.masksUpButton = new System.Windows.Forms.Button();
            this.livesUpButton = new System.Windows.Forms.Button();
            this.livesDownButton = new System.Windows.Forms.Button();
            this.freezeLivesCheckbox = new System.Windows.Forms.CheckBox();
            this.livesLabel = new System.Windows.Forms.Label();
            this.masksLabel = new System.Windows.Forms.Label();
            this.dataBox = new System.Windows.Forms.GroupBox();
            this.freezeLevelCheckbox = new System.Windows.Forms.CheckBox();
            this.levelLabel = new System.Windows.Forms.Label();
            this.nowMapLabels = new System.Windows.Forms.Label();
            this.freezeMasksCheckbox = new System.Windows.Forms.CheckBox();
            this.displayRestartCheckBox = new System.Windows.Forms.CheckBox();
            this.restrartLavel = new System.Windows.Forms.Label();
            this.dataBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // masksDownButton
            // 
            this.masksDownButton.Image = global::Crash.Helper.Properties.Resources.Subtract;
            this.masksDownButton.Location = new System.Drawing.Point(62, 44);
            this.masksDownButton.Name = "masksDownButton";
            this.masksDownButton.Size = new System.Drawing.Size(28, 26);
            this.masksDownButton.TabIndex = 10;
            this.masksDownButton.UseVisualStyleBackColor = true;
            this.masksDownButton.Click += new System.EventHandler(this.masksDownButton_Click);
            // 
            // masksUpButton
            // 
            this.masksUpButton.Image = global::Crash.Helper.Properties.Resources.Add;
            this.masksUpButton.Location = new System.Drawing.Point(91, 44);
            this.masksUpButton.Name = "masksUpButton";
            this.masksUpButton.Size = new System.Drawing.Size(28, 26);
            this.masksUpButton.TabIndex = 11;
            this.masksUpButton.UseVisualStyleBackColor = true;
            this.masksUpButton.Click += new System.EventHandler(this.masksUpButton_Click);
            // 
            // livesUpButton
            // 
            this.livesUpButton.Image = global::Crash.Helper.Properties.Resources.Add;
            this.livesUpButton.Location = new System.Drawing.Point(91, 18);
            this.livesUpButton.Name = "livesUpButton";
            this.livesUpButton.Size = new System.Drawing.Size(28, 26);
            this.livesUpButton.TabIndex = 9;
            this.livesUpButton.UseVisualStyleBackColor = true;
            this.livesUpButton.Click += new System.EventHandler(this.livesUpButton_Click);
            // 
            // livesDownButton
            // 
            this.livesDownButton.Image = global::Crash.Helper.Properties.Resources.Subtract;
            this.livesDownButton.Location = new System.Drawing.Point(62, 18);
            this.livesDownButton.Name = "livesDownButton";
            this.livesDownButton.Size = new System.Drawing.Size(28, 26);
            this.livesDownButton.TabIndex = 8;
            this.livesDownButton.UseVisualStyleBackColor = true;
            this.livesDownButton.Click += new System.EventHandler(this.livesDownButton_Click);
            // 
            // freezeLivesCheckbox
            // 
            this.freezeLivesCheckbox.AutoSize = true;
            this.freezeLivesCheckbox.Location = new System.Drawing.Point(125, 24);
            this.freezeLivesCheckbox.Name = "freezeLivesCheckbox";
            this.freezeLivesCheckbox.Size = new System.Drawing.Size(86, 16);
            this.freezeLivesCheckbox.TabIndex = 12;
            this.freezeLivesCheckbox.Text = "Freeze lives";
            this.freezeLivesCheckbox.UseVisualStyleBackColor = true;
            this.freezeLivesCheckbox.CheckedChanged += new System.EventHandler(this.freezeLivesCheckbox_CheckedChanged);
            // 
            // livesLabel
            // 
            this.livesLabel.AutoSize = true;
            this.livesLabel.Location = new System.Drawing.Point(7, 24);
            this.livesLabel.Name = "livesLabel";
            this.livesLabel.Size = new System.Drawing.Size(44, 12);
            this.livesLabel.TabIndex = 13;
            this.livesLabel.Text = "Lives: -";
            // 
            // masksLabel
            // 
            this.masksLabel.AutoSize = true;
            this.masksLabel.Location = new System.Drawing.Point(6, 51);
            this.masksLabel.Name = "masksLabel";
            this.masksLabel.Size = new System.Drawing.Size(50, 12);
            this.masksLabel.TabIndex = 14;
            this.masksLabel.Text = "Masks: -";
            // 
            // dataBox
            // 
            this.dataBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataBox.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.dataBox.Controls.Add(this.displayRestartCheckBox);
            this.dataBox.Controls.Add(this.restrartLavel);
            this.dataBox.Controls.Add(this.freezeLevelCheckbox);
            this.dataBox.Controls.Add(this.levelLabel);
            this.dataBox.Controls.Add(this.nowMapLabels);
            this.dataBox.Controls.Add(this.freezeMasksCheckbox);
            this.dataBox.Controls.Add(this.masksLabel);
            this.dataBox.Controls.Add(this.livesLabel);
            this.dataBox.Controls.Add(this.freezeLivesCheckbox);
            this.dataBox.Controls.Add(this.livesDownButton);
            this.dataBox.Controls.Add(this.livesUpButton);
            this.dataBox.Controls.Add(this.masksUpButton);
            this.dataBox.Controls.Add(this.masksDownButton);
            this.dataBox.Location = new System.Drawing.Point(0, 0);
            this.dataBox.Margin = new System.Windows.Forms.Padding(0);
            this.dataBox.MinimumSize = new System.Drawing.Size(250, 0);
            this.dataBox.Name = "dataBox";
            this.dataBox.Size = new System.Drawing.Size(285, 152);
            this.dataBox.TabIndex = 0;
            this.dataBox.TabStop = false;
            this.dataBox.Text = "Data";
            this.dataBox.EnabledChanged += new System.EventHandler(this.dataBox_EnabledChanged);
            // 
            // freezeLevelCheckbox
            // 
            this.freezeLevelCheckbox.AutoSize = true;
            this.freezeLevelCheckbox.Location = new System.Drawing.Point(125, 76);
            this.freezeLevelCheckbox.Name = "freezeLevelCheckbox";
            this.freezeLevelCheckbox.Size = new System.Drawing.Size(86, 16);
            this.freezeLevelCheckbox.TabIndex = 23;
            this.freezeLevelCheckbox.Text = "Freeze level";
            this.freezeLevelCheckbox.UseVisualStyleBackColor = true;
            // 
            // levelLabel
            // 
            this.levelLabel.AutoSize = true;
            this.levelLabel.Location = new System.Drawing.Point(6, 77);
            this.levelLabel.Name = "levelLabel";
            this.levelLabel.Size = new System.Drawing.Size(44, 12);
            this.levelLabel.TabIndex = 22;
            this.levelLabel.Text = "Level: -";
            // 
            // nowMapLabels
            // 
            this.nowMapLabels.AutoSize = true;
            this.nowMapLabels.Location = new System.Drawing.Point(7, 95);
            this.nowMapLabels.Name = "nowMapLabels";
            this.nowMapLabels.Size = new System.Drawing.Size(58, 12);
            this.nowMapLabels.TabIndex = 19;
            this.nowMapLabels.Text = "nowMap: -";
            // 
            // freezeMasksCheckbox
            // 
            this.freezeMasksCheckbox.AutoSize = true;
            this.freezeMasksCheckbox.Location = new System.Drawing.Point(125, 50);
            this.freezeMasksCheckbox.Name = "freezeMasksCheckbox";
            this.freezeMasksCheckbox.Size = new System.Drawing.Size(95, 16);
            this.freezeMasksCheckbox.TabIndex = 15;
            this.freezeMasksCheckbox.Text = "Freeze masks";
            this.freezeMasksCheckbox.UseVisualStyleBackColor = true;
            this.freezeMasksCheckbox.CheckedChanged += new System.EventHandler(this.freezeMasksCheckbox_CheckedChanged);
            // 
            // displayRestartCheckBox
            // 
            this.displayRestartCheckBox.AutoSize = true;
            this.displayRestartCheckBox.Location = new System.Drawing.Point(125, 116);
            this.displayRestartCheckBox.Name = "displayRestartCheckBox";
            this.displayRestartCheckBox.Size = new System.Drawing.Size(100, 16);
            this.displayRestartCheckBox.TabIndex = 25;
            this.displayRestartCheckBox.Text = "DisplayRestart";
            this.displayRestartCheckBox.UseVisualStyleBackColor = true;
            this.displayRestartCheckBox.CheckedChanged += new System.EventHandler(this.displayRestartCheckBox_CheckedChanged);
            // hide restart control for now (feature incomplete)
            this.displayRestartCheckBox.Visible = false;
            // 
            // restrartLavel
            // 
            this.restrartLavel.AutoSize = true;
            this.restrartLavel.Location = new System.Drawing.Point(6, 117);
            this.restrartLavel.Name = "restrartLavel";
            this.restrartLavel.Size = new System.Drawing.Size(55, 12);
            this.restrartLavel.TabIndex = 24;
            this.restrartLavel.Text = "Restart: -";
            this.restrartLavel.Visible = false;
            // 
            // DataControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.dataBox);
            this.Enabled = false;
            this.Margin = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.Name = "DataControl";
            this.Size = new System.Drawing.Size(285, 152);
            this.dataBox.ResumeLayout(false);
            this.dataBox.PerformLayout();
            this.ResumeLayout(false);

		}

        #endregion

        private System.Windows.Forms.Button masksDownButton;
        private System.Windows.Forms.Button masksUpButton;
        private System.Windows.Forms.Button livesUpButton;
        private System.Windows.Forms.Button livesDownButton;
        private System.Windows.Forms.CheckBox freezeLivesCheckbox;
        private System.Windows.Forms.Label livesLabel;
        private System.Windows.Forms.Label masksLabel;
        private System.Windows.Forms.GroupBox dataBox;
        private System.Windows.Forms.CheckBox freezeMasksCheckbox;
        private System.Windows.Forms.Label nowMapLabels;
        private System.Windows.Forms.CheckBox freezeLevelCheckbox;
        private System.Windows.Forms.Label levelLabel;
        private System.Windows.Forms.CheckBox displayRestartCheckBox;
        private System.Windows.Forms.Label restrartLavel;
    }
}
