namespace apod_wallpaper
{
    partial class ConfigurationForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConfigurationForm));
            this.downloadSetCheckBox = new System.Windows.Forms.CheckBox();
            this.startWithWindowsCheckBox = new System.Windows.Forms.CheckBox();
            this.trayIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.downloadButton = new System.Windows.Forms.Button();
            this.PreviewPictureBox = new System.Windows.Forms.PictureBox();
            this.wallpaperStyleComboBox = new System.Windows.Forms.ComboBox();
            this.pictureDayDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.everyTimeCheckBox = new System.Windows.Forms.CheckBox();
            this.apiKeyLabel = new System.Windows.Forms.Label();
            this.apiKeyTextBox = new System.Windows.Forms.TextBox();
            this.apiKeyStatusLabel = new System.Windows.Forms.Label();
            this.apiKeyInfoLabel = new System.Windows.Forms.Label();
            this.getApiKeyButton = new System.Windows.Forms.Button();
            this.imagesFolderLabel = new System.Windows.Forms.Label();
            this.imagesFolderTextBox = new System.Windows.Forms.TextBox();
            this.browseImagesFolderButton = new System.Windows.Forms.Button();
            this.openImagesFolderButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.PreviewPictureBox)).BeginInit();
            this.SuspendLayout();
            this.downloadSetCheckBox.AutoSize = true;
            this.downloadSetCheckBox.Location = new System.Drawing.Point(12, 12);
            this.downloadSetCheckBox.Name = "downloadSetCheckBox";
            this.downloadSetCheckBox.Size = new System.Drawing.Size(258, 17);
            this.downloadSetCheckBox.TabIndex = 0;
            this.downloadSetCheckBox.TabStop = false;
            this.downloadSetCheckBox.Text = "Apply latest APOD on tray double-click";
            this.downloadSetCheckBox.UseVisualStyleBackColor = true;
            this.startWithWindowsCheckBox.AutoSize = true;
            this.startWithWindowsCheckBox.Location = new System.Drawing.Point(12, 35);
            this.startWithWindowsCheckBox.Name = "startWithWindowsCheckBox";
            this.startWithWindowsCheckBox.Size = new System.Drawing.Size(117, 17);
            this.startWithWindowsCheckBox.TabIndex = 1;
            this.startWithWindowsCheckBox.TabStop = false;
            this.startWithWindowsCheckBox.Text = "Start with Windows";
            this.startWithWindowsCheckBox.UseVisualStyleBackColor = true;
            this.downloadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.downloadButton.Location = new System.Drawing.Point(12, 323);
            this.downloadButton.Name = "downloadButton";
            this.downloadButton.Size = new System.Drawing.Size(157, 23);
            this.downloadButton.TabIndex = 4;
            this.downloadButton.TabStop = false;
            this.downloadButton.Text = "Download and Set";
            this.downloadButton.UseVisualStyleBackColor = true;
            this.downloadButton.Click += new System.EventHandler(this.downloadButton_Click);
            this.downloadButton.MouseHover += new System.EventHandler(this.downloadButton_MouseHover);
            this.PreviewPictureBox.InitialImage = global::apod_wallpaper.ApodResources.loading_image_progress;
            this.PreviewPictureBox.Location = new System.Drawing.Point(12, 58);
            this.PreviewPictureBox.Name = "PreviewPictureBox";
            this.PreviewPictureBox.Size = new System.Drawing.Size(291, 165);
            this.PreviewPictureBox.TabIndex = 0;
            this.PreviewPictureBox.TabStop = false;
            this.PreviewPictureBox.LoadCompleted += new System.ComponentModel.AsyncCompletedEventHandler(this.PreviewPictureBox_LoadCompleted);
            this.PreviewPictureBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PreviewPictureBox_MouseClick);
            this.PreviewPictureBox.MouseHover += new System.EventHandler(this.PreviewPictureBox_MouseHover);
            this.wallpaperStyleComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.wallpaperStyleComboBox.FormattingEnabled = true;
            this.wallpaperStyleComboBox.Location = new System.Drawing.Point(195, 325);
            this.wallpaperStyleComboBox.Name = "wallpaperStyleComboBox";
            this.wallpaperStyleComboBox.Size = new System.Drawing.Size(108, 21);
            this.wallpaperStyleComboBox.TabIndex = 7;
            this.wallpaperStyleComboBox.TabStop = false;
            this.wallpaperStyleComboBox.SelectedIndexChanged += new System.EventHandler(this.wallpaperStyleComboBox_SelectedIndexChanged);
            this.pictureDayDateTimePicker.Location = new System.Drawing.Point(12, 229);
            this.pictureDayDateTimePicker.Name = "pictureDayDateTimePicker";
            this.pictureDayDateTimePicker.Size = new System.Drawing.Size(156, 20);
            this.pictureDayDateTimePicker.TabIndex = 8;
            this.pictureDayDateTimePicker.TabStop = false;
            this.pictureDayDateTimePicker.ValueChanged += new System.EventHandler(this.PictureDayDateTimePicker_ValueChanged);
            this.everyTimeCheckBox.AutoSize = true;
            this.everyTimeCheckBox.Location = new System.Drawing.Point(181, 226);
            this.everyTimeCheckBox.Name = "everyTimeCheckBox";
            this.everyTimeCheckBox.Size = new System.Drawing.Size(121, 30);
            this.everyTimeCheckBox.TabIndex = 10;
            this.everyTimeCheckBox.TabStop = false;
            this.everyTimeCheckBox.Text = "Auto-check for\r\ntoday's wallpaper";
            this.everyTimeCheckBox.UseVisualStyleBackColor = true;
            this.everyTimeCheckBox.CheckedChanged += new System.EventHandler(this.everyTimeCheckBox_CheckedChanged);
            this.apiKeyLabel.AutoSize = true;
            this.apiKeyLabel.Location = new System.Drawing.Point(12, 381);
            this.apiKeyLabel.Name = "apiKeyLabel";
            this.apiKeyLabel.Size = new System.Drawing.Size(74, 13);
            this.apiKeyLabel.TabIndex = 11;
            this.apiKeyLabel.Text = "NASA API key";
            this.apiKeyTextBox.Location = new System.Drawing.Point(12, 398);
            this.apiKeyTextBox.Name = "apiKeyTextBox";
            this.apiKeyTextBox.Size = new System.Drawing.Size(291, 20);
            this.apiKeyTextBox.TabIndex = 12;
            this.apiKeyStatusLabel.ForeColor = System.Drawing.Color.Firebrick;
            this.apiKeyStatusLabel.Location = new System.Drawing.Point(12, 421);
            this.apiKeyStatusLabel.Name = "apiKeyStatusLabel";
            this.apiKeyStatusLabel.Size = new System.Drawing.Size(291, 17);
            this.apiKeyStatusLabel.TabIndex = 13;
            this.apiKeyInfoLabel.Location = new System.Drawing.Point(12, 442);
            this.apiKeyInfoLabel.Name = "apiKeyInfoLabel";
            this.apiKeyInfoLabel.Size = new System.Drawing.Size(291, 49);
            this.apiKeyInfoLabel.TabIndex = 14;
            this.apiKeyInfoLabel.Text = "DEMO_KEY is fine for testing, but it is limited to 30 requests per hour per IP an" +
    "d 50 per day. Your own free NASA API key raises the default limit to 1000 reques" +
    "ts per hour. In some regions the NASA site may require VPN.";
            this.getApiKeyButton.Location = new System.Drawing.Point(174, 494);
            this.getApiKeyButton.Name = "getApiKeyButton";
            this.getApiKeyButton.Size = new System.Drawing.Size(129, 23);
            this.getApiKeyButton.TabIndex = 15;
            this.getApiKeyButton.Text = "Get NASA API Key";
            this.getApiKeyButton.UseVisualStyleBackColor = true;
            this.getApiKeyButton.Click += new System.EventHandler(this.getApiKeyButton_Click);
            this.imagesFolderLabel.AutoSize = true;
            this.imagesFolderLabel.Location = new System.Drawing.Point(12, 258);
            this.imagesFolderLabel.Name = "imagesFolderLabel";
            this.imagesFolderLabel.Size = new System.Drawing.Size(104, 13);
            this.imagesFolderLabel.TabIndex = 15;
            this.imagesFolderLabel.Text = "Downloaded images";
            this.imagesFolderTextBox.Location = new System.Drawing.Point(12, 275);
            this.imagesFolderTextBox.Name = "imagesFolderTextBox";
            this.imagesFolderTextBox.Size = new System.Drawing.Size(291, 20);
            this.imagesFolderTextBox.TabIndex = 16;
            this.browseImagesFolderButton.Location = new System.Drawing.Point(12, 301);
            this.browseImagesFolderButton.Name = "browseImagesFolderButton";
            this.browseImagesFolderButton.Size = new System.Drawing.Size(75, 23);
            this.browseImagesFolderButton.TabIndex = 17;
            this.browseImagesFolderButton.Text = "Browse...";
            this.browseImagesFolderButton.UseVisualStyleBackColor = true;
            this.browseImagesFolderButton.Click += new System.EventHandler(this.browseImagesFolderButton_Click);
            this.openImagesFolderButton.Location = new System.Drawing.Point(93, 301);
            this.openImagesFolderButton.Name = "openImagesFolderButton";
            this.openImagesFolderButton.Size = new System.Drawing.Size(75, 23);
            this.openImagesFolderButton.TabIndex = 18;
            this.openImagesFolderButton.Text = "Open Folder";
            this.openImagesFolderButton.UseVisualStyleBackColor = true;
            this.openImagesFolderButton.Click += new System.EventHandler(this.openImagesFolderButton_Click);
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(315, 529);
            this.Controls.Add(this.openImagesFolderButton);
            this.Controls.Add(this.browseImagesFolderButton);
            this.Controls.Add(this.imagesFolderTextBox);
            this.Controls.Add(this.imagesFolderLabel);
            this.Controls.Add(this.getApiKeyButton);
            this.Controls.Add(this.apiKeyInfoLabel);
            this.Controls.Add(this.apiKeyStatusLabel);
            this.Controls.Add(this.apiKeyTextBox);
            this.Controls.Add(this.apiKeyLabel);
            this.Controls.Add(this.everyTimeCheckBox);
            this.Controls.Add(this.pictureDayDateTimePicker);
            this.Controls.Add(this.wallpaperStyleComboBox);
            this.Controls.Add(this.PreviewPictureBox);
            this.Controls.Add(this.downloadButton);
            this.Controls.Add(this.startWithWindowsCheckBox);
            this.Controls.Add(this.downloadSetCheckBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "ConfigurationForm";
            this.Text = "Configuration";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SaveSettings);
            this.Shown += new System.EventHandler(this.LoadSettings);
            ((System.ComponentModel.ISupportInitialize)(this.PreviewPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.CheckBox downloadSetCheckBox;
        private System.Windows.Forms.CheckBox startWithWindowsCheckBox;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private System.Windows.Forms.Button downloadButton;
        private System.Windows.Forms.PictureBox PreviewPictureBox;
        private System.Windows.Forms.ComboBox wallpaperStyleComboBox;
        private System.Windows.Forms.DateTimePicker pictureDayDateTimePicker;
        private System.Windows.Forms.CheckBox everyTimeCheckBox;
        private System.Windows.Forms.Label apiKeyLabel;
        private System.Windows.Forms.TextBox apiKeyTextBox;
        private System.Windows.Forms.Label apiKeyStatusLabel;
        private System.Windows.Forms.Label apiKeyInfoLabel;
        private System.Windows.Forms.Button getApiKeyButton;
        private System.Windows.Forms.Label imagesFolderLabel;
        private System.Windows.Forms.TextBox imagesFolderTextBox;
        private System.Windows.Forms.Button browseImagesFolderButton;
        private System.Windows.Forms.Button openImagesFolderButton;
    }
}
