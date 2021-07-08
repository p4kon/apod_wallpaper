namespace apod_wallpaper
{
    partial class configurationForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(configurationForm));
            this.showMessageCheckBox = new System.Windows.Forms.CheckBox();
            this.saveButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.trayIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.downloadButton = new System.Windows.Forms.Button();
            this.LinkTextBox = new System.Windows.Forms.TextBox();
            this.PreviewPictureBox = new System.Windows.Forms.PictureBox();
            this.setButton = new System.Windows.Forms.Button();
            this.wallpaperStyleComboBox = new System.Windows.Forms.ComboBox();
            this.pictureDayDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.setRefreshDateTimePicker = new System.Windows.Forms.DateTimePicker();
            ((System.ComponentModel.ISupportInitialize)(this.PreviewPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // showMessageCheckBox
            // 
            this.showMessageCheckBox.AutoSize = true;
            this.showMessageCheckBox.Checked = true;
            this.showMessageCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showMessageCheckBox.Location = new System.Drawing.Point(12, 12);
            this.showMessageCheckBox.Name = "showMessageCheckBox";
            this.showMessageCheckBox.Size = new System.Drawing.Size(179, 17);
            this.showMessageCheckBox.TabIndex = 0;
            this.showMessageCheckBox.Text = "Show Message On Double-Click";
            this.showMessageCheckBox.UseVisualStyleBackColor = true;
            // 
            // saveButton
            // 
            this.saveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.saveButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.saveButton.Location = new System.Drawing.Point(15, 270);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(75, 23);
            this.saveButton.TabIndex = 1;
            this.saveButton.Text = "Save";
            this.saveButton.UseVisualStyleBackColor = true;
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(215, 270);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 2;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // downloadButton
            // 
            this.downloadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadButton.Location = new System.Drawing.Point(15, 232);
            this.downloadButton.Name = "downloadButton";
            this.downloadButton.Size = new System.Drawing.Size(75, 23);
            this.downloadButton.TabIndex = 3;
            this.downloadButton.Text = "Download";
            this.downloadButton.UseVisualStyleBackColor = true;
            this.downloadButton.Click += new System.EventHandler(this.downloadButton_Click);
            // 
            // LinkTextBox
            // 
            this.LinkTextBox.Location = new System.Drawing.Point(12, 35);
            this.LinkTextBox.Name = "LinkTextBox";
            this.LinkTextBox.Size = new System.Drawing.Size(278, 20);
            this.LinkTextBox.TabIndex = 4;
            // 
            // PreviewPictureBox
            // 
            this.PreviewPictureBox.Location = new System.Drawing.Point(16, 61);
            this.PreviewPictureBox.Name = "PreviewPictureBox";
            this.PreviewPictureBox.Size = new System.Drawing.Size(274, 139);
            this.PreviewPictureBox.TabIndex = 0;
            this.PreviewPictureBox.TabStop = false;
            // 
            // setButton
            // 
            this.setButton.Location = new System.Drawing.Point(97, 269);
            this.setButton.Name = "setButton";
            this.setButton.Size = new System.Drawing.Size(75, 23);
            this.setButton.TabIndex = 5;
            this.setButton.Text = "Set";
            this.setButton.UseVisualStyleBackColor = true;
            this.setButton.Click += new System.EventHandler(this.setButton_Click);
            // 
            // wallpaperStyleComboBox
            // 
            this.wallpaperStyleComboBox.FormattingEnabled = true;
            this.wallpaperStyleComboBox.Location = new System.Drawing.Point(97, 232);
            this.wallpaperStyleComboBox.Name = "wallpaperStyleComboBox";
            this.wallpaperStyleComboBox.Size = new System.Drawing.Size(75, 21);
            this.wallpaperStyleComboBox.TabIndex = 6;
            // 
            // pictureDayDateTimePicker
            // 
            this.pictureDayDateTimePicker.Location = new System.Drawing.Point(16, 206);
            this.pictureDayDateTimePicker.Name = "pictureDayDateTimePicker";
            this.pictureDayDateTimePicker.Size = new System.Drawing.Size(156, 20);
            this.pictureDayDateTimePicker.TabIndex = 7;
            this.pictureDayDateTimePicker.ValueChanged += new System.EventHandler(this.PictureDayDateTimePicker_ValueChanged);
            // 
            // setRefreshDateTimePicker
            // 
            this.setRefreshDateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Time;
            this.setRefreshDateTimePicker.Location = new System.Drawing.Point(215, 235);
            this.setRefreshDateTimePicker.Name = "setRefreshDateTimePicker";
            this.setRefreshDateTimePicker.ShowUpDown = true;
            this.setRefreshDateTimePicker.Size = new System.Drawing.Size(75, 20);
            this.setRefreshDateTimePicker.TabIndex = 8;
            this.setRefreshDateTimePicker.Value = new System.DateTime(2021, 7, 8, 3, 0, 0, 0);
            // 
            // configurationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(302, 305);
            this.Controls.Add(this.setRefreshDateTimePicker);
            this.Controls.Add(this.pictureDayDateTimePicker);
            this.Controls.Add(this.wallpaperStyleComboBox);
            this.Controls.Add(this.setButton);
            this.Controls.Add(this.PreviewPictureBox);
            this.Controls.Add(this.LinkTextBox);
            this.Controls.Add(this.downloadButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.saveButton);
            this.Controls.Add(this.showMessageCheckBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "configurationForm";
            this.Text = "Configuration";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SaveSettings);
            this.Shown += new System.EventHandler(this.LoadSettings);
            ((System.ComponentModel.ISupportInitialize)(this.PreviewPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox showMessageCheckBox;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private System.Windows.Forms.Button downloadButton;
        private System.Windows.Forms.TextBox LinkTextBox;
        private System.Windows.Forms.PictureBox PreviewPictureBox;
        private System.Windows.Forms.Button setButton;
        private System.Windows.Forms.ComboBox wallpaperStyleComboBox;
        private System.Windows.Forms.DateTimePicker pictureDayDateTimePicker;
        private System.Windows.Forms.DateTimePicker setRefreshDateTimePicker;
    }
}