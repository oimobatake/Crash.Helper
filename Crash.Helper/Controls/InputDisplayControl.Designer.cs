namespace Crash.Helper.Controls
{
    partial class InputDisplayControl
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
            this.inputBox = new System.Windows.Forms.GroupBox();
            this.camInputYLabel = new System.Windows.Forms.Label();
            this.camInputXLabel = new System.Windows.Forms.Label();
            this.inputYLabel = new System.Windows.Forms.Label();
            this.inputXLabel = new System.Windows.Forms.Label();
            this.inputBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // inputBox
            // 
            this.inputBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.inputBox.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.inputBox.Controls.Add(this.camInputYLabel);
            this.inputBox.Controls.Add(this.camInputXLabel);
            this.inputBox.Controls.Add(this.inputYLabel);
            this.inputBox.Controls.Add(this.inputXLabel);
            this.inputBox.Location = new System.Drawing.Point(0, 0);
            this.inputBox.Margin = new System.Windows.Forms.Padding(0);
            this.inputBox.MinimumSize = new System.Drawing.Size(250, 0);
            this.inputBox.Name = "inputBox";
            this.inputBox.Size = new System.Drawing.Size(285, 110);
            this.inputBox.TabIndex = 0;
            this.inputBox.TabStop = false;
            this.inputBox.Text = "Inputs";
            // 
            // camInputYLabel
            // 
            this.camInputYLabel.AutoSize = true;
            this.camInputYLabel.Location = new System.Drawing.Point(8, 87);
            this.camInputYLabel.Name = "camInputYLabel";
            this.camInputYLabel.Size = new System.Drawing.Size(73, 12);
            this.camInputYLabel.TabIndex = 3;
            this.camInputYLabel.Text = "CamInputY: -";
            // 
            // camInputXLabel
            // 
            this.camInputXLabel.AutoSize = true;
            this.camInputXLabel.Location = new System.Drawing.Point(8, 65);
            this.camInputXLabel.Name = "camInputXLabel";
            this.camInputXLabel.Size = new System.Drawing.Size(73, 12);
            this.camInputXLabel.TabIndex = 2;
            this.camInputXLabel.Text = "CamInputX: -";
            // 
            // inputYLabel
            // 
            this.inputYLabel.AutoSize = true;
            this.inputYLabel.Location = new System.Drawing.Point(8, 43);
            this.inputYLabel.Name = "inputYLabel";
            this.inputYLabel.Size = new System.Drawing.Size(49, 12);
            this.inputYLabel.TabIndex = 1;
            this.inputYLabel.Text = "InputY: -";
            // 
            // inputXLabel
            // 
            this.inputXLabel.AutoSize = true;
            this.inputXLabel.Location = new System.Drawing.Point(8, 21);
            this.inputXLabel.Name = "inputXLabel";
            this.inputXLabel.Size = new System.Drawing.Size(49, 12);
            this.inputXLabel.TabIndex = 0;
            this.inputXLabel.Text = "InputX: -";
            // 
            // InputDisplayControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.inputBox);
            this.Enabled = false;
            this.Margin = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.Name = "InputDisplayControl";
            this.Size = new System.Drawing.Size(285, 110);
            this.inputBox.ResumeLayout(false);
            this.inputBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox inputBox;
        private System.Windows.Forms.Label inputXLabel;
        private System.Windows.Forms.Label inputYLabel;
        private System.Windows.Forms.Label camInputXLabel;
        private System.Windows.Forms.Label camInputYLabel;
    }
}