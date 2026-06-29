namespace Crash.Helper.Controls
{
    partial class HotkeyControl
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
            this.components = new System.ComponentModel.Container();
            this.zeroLivesLabel = new System.Windows.Forms.Label();
            this.hotkeyBox = new System.Windows.Forms.GroupBox();
            this.addMaskHotkeyTextbox = new System.Windows.Forms.TextBox();
            this.zeroLivesHotkeyTextbox = new System.Windows.Forms.TextBox();
            this.addMaskHotkeyLabel = new System.Windows.Forms.Label();
            this.giveMaskLabel = new System.Windows.Forms.Label();
            this.zeroLivesHotkeyLabel = new System.Windows.Forms.Label();
            this.enabledCheckbox = new System.Windows.Forms.CheckBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.subMaskHotkeyTextbox = new System.Windows.Forms.TextBox();
            this.subMaskHotkeyLabel = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.freezeLevelHotkeyTextbox = new System.Windows.Forms.TextBox();
            this.freezeLevelHotkeyLabel = new System.Windows.Forms.Label();
            this.hotkeyBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // zeroLivesLabel
            // 
            this.zeroLivesLabel.AutoSize = true;
            this.zeroLivesLabel.Location = new System.Drawing.Point(7, 40);
            this.zeroLivesLabel.Name = "zeroLivesLabel";
            this.zeroLivesLabel.Size = new System.Drawing.Size(88, 12);
            this.zeroLivesLabel.TabIndex = 0;
            this.zeroLivesLabel.Text = "• Set lives to 99:";
            // 
            // hotkeyBox
            // 
            this.hotkeyBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.hotkeyBox.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.hotkeyBox.Controls.Add(this.label1);
            this.hotkeyBox.Controls.Add(this.freezeLevelHotkeyTextbox);
            this.hotkeyBox.Controls.Add(this.freezeLevelHotkeyLabel);
            this.hotkeyBox.Controls.Add(this.label2);
            this.hotkeyBox.Controls.Add(this.subMaskHotkeyTextbox);
            this.hotkeyBox.Controls.Add(this.subMaskHotkeyLabel);
            this.hotkeyBox.Controls.Add(this.addMaskHotkeyTextbox);
            this.hotkeyBox.Controls.Add(this.zeroLivesHotkeyTextbox);
            this.hotkeyBox.Controls.Add(this.addMaskHotkeyLabel);
            this.hotkeyBox.Controls.Add(this.giveMaskLabel);
            this.hotkeyBox.Controls.Add(this.zeroLivesHotkeyLabel);
            this.hotkeyBox.Controls.Add(this.enabledCheckbox);
            this.hotkeyBox.Controls.Add(this.zeroLivesLabel);
            this.hotkeyBox.Location = new System.Drawing.Point(0, 0);
            this.hotkeyBox.Margin = new System.Windows.Forms.Padding(0);
            this.hotkeyBox.MinimumSize = new System.Drawing.Size(250, 0);
            this.hotkeyBox.Name = "hotkeyBox";
            this.hotkeyBox.Size = new System.Drawing.Size(285, 143);
            this.hotkeyBox.TabIndex = 0;
            this.hotkeyBox.TabStop = false;
            this.hotkeyBox.Text = "Hotkeys";
            // 
            // addMaskHotkeyTextbox
            // 
            this.addMaskHotkeyTextbox.Location = new System.Drawing.Point(114, 62);
            this.addMaskHotkeyTextbox.Name = "addMaskHotkeyTextbox";
            this.addMaskHotkeyTextbox.Size = new System.Drawing.Size(20, 19);
            this.addMaskHotkeyTextbox.TabIndex = 7;
            this.addMaskHotkeyTextbox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.addMaskHotkeyTextbox.Visible = false;
            this.addMaskHotkeyTextbox.TextChanged += new System.EventHandler(this.hotkeyTextbox_TextChanged);
            // 
            // zeroLivesHotkeyTextbox
            // 
            this.zeroLivesHotkeyTextbox.Location = new System.Drawing.Point(114, 37);
            this.zeroLivesHotkeyTextbox.MaxLength = 1;
            this.zeroLivesHotkeyTextbox.Name = "zeroLivesHotkeyTextbox";
            this.zeroLivesHotkeyTextbox.Size = new System.Drawing.Size(20, 19);
            this.zeroLivesHotkeyTextbox.TabIndex = 6;
            this.zeroLivesHotkeyTextbox.Visible = false;
            this.zeroLivesHotkeyTextbox.TextChanged += new System.EventHandler(this.hotkeyTextbox_TextChanged);
            // 
            // addMaskHotkeyLabel
            // 
            this.addMaskHotkeyLabel.AutoEllipsis = true;
            this.addMaskHotkeyLabel.AutoSize = true;
            this.addMaskHotkeyLabel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.addMaskHotkeyLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addMaskHotkeyLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.addMaskHotkeyLabel.Location = new System.Drawing.Point(114, 65);
            this.addMaskHotkeyLabel.Name = "addMaskHotkeyLabel";
            this.addMaskHotkeyLabel.Size = new System.Drawing.Size(43, 15);
            this.addMaskHotkeyLabel.TabIndex = 5;
            this.addMaskHotkeyLabel.Text = "Hotkey";
            this.addMaskHotkeyLabel.Click += new System.EventHandler(this.hotkeyLabelClicked);
            // 
            // giveMaskLabel
            // 
            this.giveMaskLabel.AutoSize = true;
            this.giveMaskLabel.Location = new System.Drawing.Point(8, 65);
            this.giveMaskLabel.Name = "giveMaskLabel";
            this.giveMaskLabel.Size = new System.Drawing.Size(64, 12);
            this.giveMaskLabel.TabIndex = 4;
            this.giveMaskLabel.Text = "• Add mask:";
            // 
            // zeroLivesHotkeyLabel
            // 
            this.zeroLivesHotkeyLabel.AutoEllipsis = true;
            this.zeroLivesHotkeyLabel.AutoSize = true;
            this.zeroLivesHotkeyLabel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.zeroLivesHotkeyLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.zeroLivesHotkeyLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.zeroLivesHotkeyLabel.Location = new System.Drawing.Point(114, 39);
            this.zeroLivesHotkeyLabel.Name = "zeroLivesHotkeyLabel";
            this.zeroLivesHotkeyLabel.Size = new System.Drawing.Size(43, 15);
            this.zeroLivesHotkeyLabel.TabIndex = 3;
            this.zeroLivesHotkeyLabel.Text = "Hotkey";
            this.zeroLivesHotkeyLabel.Click += new System.EventHandler(this.hotkeyLabelClicked);
            // 
            // enabledCheckbox
            // 
            this.enabledCheckbox.AutoSize = true;
            this.enabledCheckbox.Checked = true;
            this.enabledCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.enabledCheckbox.Location = new System.Drawing.Point(10, 15);
            this.enabledCheckbox.Margin = new System.Windows.Forms.Padding(0);
            this.enabledCheckbox.Name = "enabledCheckbox";
            this.enabledCheckbox.Size = new System.Drawing.Size(109, 16);
            this.enabledCheckbox.TabIndex = 2;
            this.enabledCheckbox.Text = "Hotkeys enabled";
            this.enabledCheckbox.UseVisualStyleBackColor = true;
            this.enabledCheckbox.CheckedChanged += new System.EventHandler(this.enabledCheckbox_CheckedChanged);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(61, 4);
            // 
            // subMaskHotkeyTextbox
            // 
            this.subMaskHotkeyTextbox.Location = new System.Drawing.Point(114, 87);
            this.subMaskHotkeyTextbox.Name = "subMaskHotkeyTextbox";
            this.subMaskHotkeyTextbox.Size = new System.Drawing.Size(20, 19);
            this.subMaskHotkeyTextbox.TabIndex = 9;
            this.subMaskHotkeyTextbox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.subMaskHotkeyTextbox.Visible = false;
            this.subMaskHotkeyTextbox.TextChanged += new System.EventHandler(this.hotkeyTextbox_TextChanged);
            // 
            // subMaskHotkeyLabel
            // 
            this.subMaskHotkeyLabel.AutoEllipsis = true;
            this.subMaskHotkeyLabel.AutoSize = true;
            this.subMaskHotkeyLabel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.subMaskHotkeyLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.subMaskHotkeyLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.subMaskHotkeyLabel.Location = new System.Drawing.Point(114, 90);
            this.subMaskHotkeyLabel.Name = "subMaskHotkeyLabel";
            this.subMaskHotkeyLabel.Size = new System.Drawing.Size(43, 15);
            this.subMaskHotkeyLabel.TabIndex = 8;
            this.subMaskHotkeyLabel.Text = "Hotkey";
            this.subMaskHotkeyLabel.Click += new System.EventHandler(this.hotkeyLabelClicked);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 91);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(63, 12);
            this.label2.TabIndex = 10;
            this.label2.Text = "• Sub mask:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 115);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(78, 12);
            this.label1.TabIndex = 13;
            this.label1.Text = "• Toggle Level Lock:";
            // 
            // freezeLevelHotkeyTextbox
            // 
            this.freezeLevelHotkeyTextbox.Location = new System.Drawing.Point(114, 111);
            this.freezeLevelHotkeyTextbox.Name = "freezeLevelHotkeyTextbox";
            this.freezeLevelHotkeyTextbox.Size = new System.Drawing.Size(20, 19);
            this.freezeLevelHotkeyTextbox.TabIndex = 12;
            this.freezeLevelHotkeyTextbox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.freezeLevelHotkeyTextbox.Visible = false;
            this.freezeLevelHotkeyTextbox.TextChanged += new System.EventHandler(this.hotkeyTextbox_TextChanged);
            // 
            // freezeLevelHotkeyLabel
            // 
            this.freezeLevelHotkeyLabel.AutoEllipsis = true;
            this.freezeLevelHotkeyLabel.AutoSize = true;
            this.freezeLevelHotkeyLabel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.freezeLevelHotkeyLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.freezeLevelHotkeyLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.freezeLevelHotkeyLabel.Location = new System.Drawing.Point(114, 114);
            this.freezeLevelHotkeyLabel.Name = "freezeLevelHotkeyLabel";
            this.freezeLevelHotkeyLabel.Size = new System.Drawing.Size(43, 15);
            this.freezeLevelHotkeyLabel.TabIndex = 11;
            this.freezeLevelHotkeyLabel.Text = "Hotkey";
            this.freezeLevelHotkeyLabel.Click += new System.EventHandler(this.hotkeyLabelClicked);
            // 
            // HotkeyControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.hotkeyBox);
            this.Enabled = false;
            this.Margin = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.Name = "HotkeyControl";
            this.Size = new System.Drawing.Size(285, 143);
            this.hotkeyBox.ResumeLayout(false);
            this.hotkeyBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox hotkeyBox;
        private System.Windows.Forms.CheckBox enabledCheckbox;
        private System.Windows.Forms.Label zeroLivesLabel;
		private System.Windows.Forms.Label zeroLivesHotkeyLabel;
        private System.Windows.Forms.Label addMaskHotkeyLabel;
        private System.Windows.Forms.Label giveMaskLabel;
        private System.Windows.Forms.TextBox addMaskHotkeyTextbox;
        private System.Windows.Forms.TextBox zeroLivesHotkeyTextbox;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox subMaskHotkeyTextbox;
        private System.Windows.Forms.Label subMaskHotkeyLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox freezeLevelHotkeyTextbox;
        private System.Windows.Forms.Label freezeLevelHotkeyLabel;
    }
}
