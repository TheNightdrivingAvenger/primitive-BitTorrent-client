namespace CourseWork
{
    partial class TorrentInfo
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
            this.AddTorrentOK = new System.Windows.Forms.Button();
            this.AddTorrentCancel = new System.Windows.Forms.Button();
            this.ContentsBox = new System.Windows.Forms.GroupBox();
            this.DateLblContents = new System.Windows.Forms.Label();
            this.DescriptionLblContents = new System.Windows.Forms.Label();
            this.SizeLblContents = new System.Windows.Forms.Label();
            this.NameLblContents = new System.Windows.Forms.Label();
            this.DateLbl = new System.Windows.Forms.Label();
            this.DescriptionLbl = new System.Windows.Forms.Label();
            this.SizeLbl = new System.Windows.Forms.Label();
            this.NameLbl = new System.Windows.Forms.Label();
            this.PathBox = new System.Windows.Forms.GroupBox();
            this.DownloadPath = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.DownloadPathDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.BrowseFolderButton = new System.Windows.Forms.Button();
            this.CreateSubFolder = new System.Windows.Forms.CheckBox();
            this.ContentsBox.SuspendLayout();
            this.PathBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // AddTorrentOK
            // 
            this.AddTorrentOK.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.AddTorrentOK.Enabled = false;
            this.AddTorrentOK.Location = new System.Drawing.Point(0, 411);
            this.AddTorrentOK.Name = "AddTorrentOK";
            this.AddTorrentOK.Size = new System.Drawing.Size(664, 41);
            this.AddTorrentOK.TabIndex = 0;
            this.AddTorrentOK.Text = "OK";
            this.AddTorrentOK.UseVisualStyleBackColor = true;
            this.AddTorrentOK.Click += new System.EventHandler(this.AddTorrentOK_Click);
            // 
            // AddTorrentCancel
            // 
            this.AddTorrentCancel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.AddTorrentCancel.Location = new System.Drawing.Point(0, 369);
            this.AddTorrentCancel.Name = "AddTorrentCancel";
            this.AddTorrentCancel.Size = new System.Drawing.Size(664, 42);
            this.AddTorrentCancel.TabIndex = 1;
            this.AddTorrentCancel.Text = "Cancel";
            this.AddTorrentCancel.UseVisualStyleBackColor = true;
            this.AddTorrentCancel.Click += new System.EventHandler(this.AddTorrentCancel_Click);
            // 
            // ContentsBox
            // 
            this.ContentsBox.Controls.Add(this.DateLblContents);
            this.ContentsBox.Controls.Add(this.DescriptionLblContents);
            this.ContentsBox.Controls.Add(this.SizeLblContents);
            this.ContentsBox.Controls.Add(this.NameLblContents);
            this.ContentsBox.Controls.Add(this.DateLbl);
            this.ContentsBox.Controls.Add(this.DescriptionLbl);
            this.ContentsBox.Controls.Add(this.SizeLbl);
            this.ContentsBox.Controls.Add(this.NameLbl);
            this.ContentsBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.ContentsBox.Location = new System.Drawing.Point(0, 0);
            this.ContentsBox.Name = "ContentsBox";
            this.ContentsBox.Size = new System.Drawing.Size(664, 129);
            this.ContentsBox.TabIndex = 0;
            this.ContentsBox.TabStop = false;
            this.ContentsBox.Text = "Contents info";
            // 
            // DateLblContents
            // 
            this.DateLblContents.AutoSize = true;
            this.DateLblContents.Location = new System.Drawing.Point(96, 99);
            this.DateLblContents.Name = "DateLblContents";
            this.DateLblContents.Size = new System.Drawing.Size(12, 17);
            this.DateLblContents.TabIndex = 10;
            this.DateLblContents.Text = " ";
            // 
            // DescriptionLblContents
            // 
            this.DescriptionLblContents.AutoSize = true;
            this.DescriptionLblContents.Location = new System.Drawing.Point(96, 71);
            this.DescriptionLblContents.Name = "DescriptionLblContents";
            this.DescriptionLblContents.Size = new System.Drawing.Size(12, 17);
            this.DescriptionLblContents.TabIndex = 9;
            this.DescriptionLblContents.Text = " ";
            // 
            // SizeLblContents
            // 
            this.SizeLblContents.AutoSize = true;
            this.SizeLblContents.Location = new System.Drawing.Point(96, 44);
            this.SizeLblContents.Name = "SizeLblContents";
            this.SizeLblContents.Size = new System.Drawing.Size(12, 17);
            this.SizeLblContents.TabIndex = 8;
            this.SizeLblContents.Text = " ";
            // 
            // NameLblContents
            // 
            this.NameLblContents.AutoSize = true;
            this.NameLblContents.Location = new System.Drawing.Point(96, 18);
            this.NameLblContents.Name = "NameLblContents";
            this.NameLblContents.Size = new System.Drawing.Size(12, 17);
            this.NameLblContents.TabIndex = 7;
            this.NameLblContents.Text = " ";
            // 
            // DateLbl
            // 
            this.DateLbl.AutoSize = true;
            this.DateLbl.Location = new System.Drawing.Point(6, 99);
            this.DateLbl.Name = "DateLbl";
            this.DateLbl.Size = new System.Drawing.Size(42, 17);
            this.DateLbl.TabIndex = 0;
            this.DateLbl.Text = "Date:";
            // 
            // DescriptionLbl
            // 
            this.DescriptionLbl.AutoSize = true;
            this.DescriptionLbl.Location = new System.Drawing.Point(6, 71);
            this.DescriptionLbl.Name = "DescriptionLbl";
            this.DescriptionLbl.Size = new System.Drawing.Size(83, 17);
            this.DescriptionLbl.TabIndex = 0;
            this.DescriptionLbl.Text = "Description:";
            // 
            // SizeLbl
            // 
            this.SizeLbl.AutoSize = true;
            this.SizeLbl.Location = new System.Drawing.Point(6, 44);
            this.SizeLbl.Name = "SizeLbl";
            this.SizeLbl.Size = new System.Drawing.Size(39, 17);
            this.SizeLbl.TabIndex = 0;
            this.SizeLbl.Text = "Size:";
            // 
            // NameLbl
            // 
            this.NameLbl.AutoSize = true;
            this.NameLbl.Location = new System.Drawing.Point(6, 18);
            this.NameLbl.Name = "NameLbl";
            this.NameLbl.Size = new System.Drawing.Size(69, 17);
            this.NameLbl.TabIndex = 0;
            this.NameLbl.Text = "Filename:";
            // 
            // PathBox
            // 
            this.PathBox.Controls.Add(this.CreateSubFolder);
            this.PathBox.Controls.Add(this.BrowseFolderButton);
            this.PathBox.Controls.Add(this.label1);
            this.PathBox.Controls.Add(this.DownloadPath);
            this.PathBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PathBox.Location = new System.Drawing.Point(0, 129);
            this.PathBox.Name = "PathBox";
            this.PathBox.Size = new System.Drawing.Size(664, 240);
            this.PathBox.TabIndex = 0;
            this.PathBox.TabStop = false;
            this.PathBox.Text = "Download settings";
            // 
            // DownloadPath
            // 
            this.DownloadPath.Location = new System.Drawing.Point(12, 42);
            this.DownloadPath.Name = "DownloadPath";
            this.DownloadPath.ReadOnly = true;
            this.DownloadPath.Size = new System.Drawing.Size(520, 22);
            this.DownloadPath.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(180, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "Choose the download path:";
            // 
            // BrowseFolderButton
            // 
            this.BrowseFolderButton.Location = new System.Drawing.Point(539, 40);
            this.BrowseFolderButton.Name = "BrowseFolderButton";
            this.BrowseFolderButton.Size = new System.Drawing.Size(119, 24);
            this.BrowseFolderButton.TabIndex = 1;
            this.BrowseFolderButton.Text = "Browse...";
            this.BrowseFolderButton.UseVisualStyleBackColor = true;
            this.BrowseFolderButton.Click += new System.EventHandler(this.BrowseFolderButton_Click);
            // 
            // CreateSubFolder
            // 
            this.CreateSubFolder.AutoSize = true;
            this.CreateSubFolder.Location = new System.Drawing.Point(12, 71);
            this.CreateSubFolder.Name = "CreateSubFolder";
            this.CreateSubFolder.Size = new System.Drawing.Size(135, 21);
            this.CreateSubFolder.TabIndex = 2;
            this.CreateSubFolder.Text = "Create subfolder";
            this.CreateSubFolder.UseVisualStyleBackColor = true;
            // 
            // TorrentInfo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(664, 452);
            this.Controls.Add(this.PathBox);
            this.Controls.Add(this.ContentsBox);
            this.Controls.Add(this.AddTorrentCancel);
            this.Controls.Add(this.AddTorrentOK);
            this.Name = "TorrentInfo";
            this.Text = "TorrentInfo";
            this.Load += new System.EventHandler(this.TorrentInfo_Load);
            this.ContentsBox.ResumeLayout(false);
            this.ContentsBox.PerformLayout();
            this.PathBox.ResumeLayout(false);
            this.PathBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button AddTorrentOK;
        private System.Windows.Forms.Button AddTorrentCancel;
        private System.Windows.Forms.GroupBox ContentsBox;
        private System.Windows.Forms.Label DateLbl;
        private System.Windows.Forms.Label DescriptionLbl;
        private System.Windows.Forms.Label SizeLbl;
        private System.Windows.Forms.Label NameLbl;
        private System.Windows.Forms.Label NameLblContents;
        private System.Windows.Forms.Label SizeLblContents;
        private System.Windows.Forms.Label DateLblContents;
        private System.Windows.Forms.Label DescriptionLblContents;
        private System.Windows.Forms.GroupBox PathBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox DownloadPath;
        private System.Windows.Forms.Button BrowseFolderButton;
        private System.Windows.Forms.FolderBrowserDialog DownloadPathDialog;
        private System.Windows.Forms.CheckBox CreateSubFolder;
    }
}