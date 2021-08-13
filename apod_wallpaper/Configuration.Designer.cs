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
            this.downloadSetCheckBox = new System.Windows.Forms.CheckBox();
            this.saveButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.trayIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.downloadButton = new System.Windows.Forms.Button();
            this.PreviewPictureBox = new System.Windows.Forms.PictureBox();
            this.wallpaperStyleComboBox = new System.Windows.Forms.ComboBox();
            this.pictureDayDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.setRefreshDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.everyTimeCheckBox = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.PreviewPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // downloadSetCheckBox
            // 
            this.downloadSetCheckBox.AutoSize = true;
            this.downloadSetCheckBox.Checked = true;
            this.downloadSetCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.downloadSetCheckBox.Location = new System.Drawing.Point(12, 12);
            this.downloadSetCheckBox.Name = "downloadSetCheckBox";
            this.downloadSetCheckBox.Size = new System.Drawing.Size(258, 17);
            this.downloadSetCheckBox.TabIndex = 0;
            this.downloadSetCheckBox.TabStop = false;
            this.downloadSetCheckBox.Text = "Download and set today\'s picture on double-click";
            this.downloadSetCheckBox.UseVisualStyleBackColor = true;
            // 
            // saveButton
            // 
            this.saveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.saveButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.saveButton.Location = new System.Drawing.Point(12, 265);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(75, 23);
            this.saveButton.TabIndex = 1;
            this.saveButton.TabStop = false;
            this.saveButton.Text = "Save";
            this.saveButton.UseVisualStyleBackColor = true;
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(93, 265);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 2;
            this.cancelButton.TabStop = false;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // downloadButton
            // 
            this.downloadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadButton.Location = new System.Drawing.Point(12, 236);
            this.downloadButton.Name = "downloadButton";
            this.downloadButton.Size = new System.Drawing.Size(157, 23);
            this.downloadButton.TabIndex = 3;
            this.downloadButton.TabStop = false;
            this.downloadButton.Text = "Download and Set";
            this.downloadButton.UseVisualStyleBackColor = true;
            this.downloadButton.Click += new System.EventHandler(this.downloadButton_Click);
            this.downloadButton.MouseHover += new System.EventHandler(this.downloadButton_MouseHover);
            // 
            // PreviewPictureBox
            // 
            this.PreviewPictureBox.InitialImage = global::apod_wallpaper.resources_apod.loading_image_progress;
            this.PreviewPictureBox.Location = new System.Drawing.Point(12, 35);
            this.PreviewPictureBox.Name = "PreviewPictureBox";
            this.PreviewPictureBox.Size = new System.Drawing.Size(291, 165);
            this.PreviewPictureBox.TabIndex = 0;
            this.PreviewPictureBox.TabStop = false;
            this.PreviewPictureBox.LoadCompleted += new System.ComponentModel.AsyncCompletedEventHandler(this.PreviewPictureBox_LoadCompleted);
            this.PreviewPictureBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PreviewPictureBox_MouseClick);
            this.PreviewPictureBox.MouseHover += new System.EventHandler(this.PreviewPictureBox_MouseHover);
            // 
            // wallpaperStyleComboBox
            // 
            this.wallpaperStyleComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.wallpaperStyleComboBox.FormattingEnabled = true;
            this.wallpaperStyleComboBox.Location = new System.Drawing.Point(195, 267);
            this.wallpaperStyleComboBox.Name = "wallpaperStyleComboBox";
            this.wallpaperStyleComboBox.Size = new System.Drawing.Size(75, 21);
            this.wallpaperStyleComboBox.TabIndex = 6;
            this.wallpaperStyleComboBox.TabStop = false;
            this.wallpaperStyleComboBox.SelectedIndexChanged += new System.EventHandler(this.wallpaperStyleComboBox_SelectedIndexChanged);
            // 
            // pictureDayDateTimePicker
            // 
            this.pictureDayDateTimePicker.Location = new System.Drawing.Point(12, 206);
            this.pictureDayDateTimePicker.Name = "pictureDayDateTimePicker";
            this.pictureDayDateTimePicker.Size = new System.Drawing.Size(156, 20);
            this.pictureDayDateTimePicker.TabIndex = 7;
            this.pictureDayDateTimePicker.TabStop = false;
            this.pictureDayDateTimePicker.ValueChanged += new System.EventHandler(this.PictureDayDateTimePicker_ValueChanged);
            // 
            // setRefreshDateTimePicker
            // 
            this.setRefreshDateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Time;
            this.setRefreshDateTimePicker.Location = new System.Drawing.Point(195, 241);
            this.setRefreshDateTimePicker.Name = "setRefreshDateTimePicker";
            this.setRefreshDateTimePicker.ShowUpDown = true;
            this.setRefreshDateTimePicker.Size = new System.Drawing.Size(75, 20);
            this.setRefreshDateTimePicker.TabIndex = 8;
            this.setRefreshDateTimePicker.TabStop = false;
            this.setRefreshDateTimePicker.Value = new System.DateTime(2021, 7, 8, 3, 0, 0, 0);
            // 
            // everyTimeCheckBox
            // 
            this.everyTimeCheckBox.AutoSize = true;
            this.everyTimeCheckBox.Location = new System.Drawing.Point(181, 204);
            this.everyTimeCheckBox.Name = "everyTimeCheckBox";
            this.everyTimeCheckBox.Size = new System.Drawing.Size(122, 30);
            this.everyTimeCheckBox.TabIndex = 9;
            this.everyTimeCheckBox.TabStop = false;
            this.everyTimeCheckBox.Text = "Check and set \r\nwallpaper every time";
            this.everyTimeCheckBox.UseVisualStyleBackColor = true;
            // 
            // configurationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(315, 300);
            this.Controls.Add(this.everyTimeCheckBox);
            this.Controls.Add(this.setRefreshDateTimePicker);
            this.Controls.Add(this.pictureDayDateTimePicker);
            this.Controls.Add(this.wallpaperStyleComboBox);
            this.Controls.Add(this.PreviewPictureBox);
            this.Controls.Add(this.downloadButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.saveButton);
            this.Controls.Add(this.downloadSetCheckBox);
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

        private System.Windows.Forms.CheckBox downloadSetCheckBox;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private System.Windows.Forms.Button downloadButton;
        private System.Windows.Forms.PictureBox PreviewPictureBox;
        private System.Windows.Forms.ComboBox wallpaperStyleComboBox;
        private System.Windows.Forms.DateTimePicker pictureDayDateTimePicker;
        private System.Windows.Forms.DateTimePicker setRefreshDateTimePicker;
        private System.Windows.Forms.CheckBox everyTimeCheckBox;
    }
}