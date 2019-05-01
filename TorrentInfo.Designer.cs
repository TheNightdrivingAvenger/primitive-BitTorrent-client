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
            this.ContentsBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // AddTorrentOK
            // 
            this.AddTorrentOK.Location = new System.Drawing.Point(320, 410);
            this.AddTorrentOK.Name = "AddTorrentOK";
            this.AddTorrentOK.Size = new System.Drawing.Size(105, 30);
            this.AddTorrentOK.TabIndex = 0;
            this.AddTorrentOK.Text = "OK";
            this.AddTorrentOK.UseVisualStyleBackColor = true;
            this.AddTorrentOK.Click += new System.EventHandler(this.AddTorrentOK_Click);
            // 
            // AddTorrentCancel
            // 
            this.AddTorrentCancel.Location = new System.Drawing.Point(442, 410);
            this.AddTorrentCancel.Name = "AddTorrentCancel";
            this.AddTorrentCancel.Size = new System.Drawing.Size(105, 30);
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
            this.ContentsBox.Location = new System.Drawing.Point(12, 12);
            this.ContentsBox.Name = "ContentsBox";
            this.ContentsBox.Size = new System.Drawing.Size(640, 129);
            this.ContentsBox.TabIndex = 2;
            this.ContentsBox.TabStop = false;
            this.ContentsBox.Text = "Сведения о содержании";
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
            this.DateLbl.Size = new System.Drawing.Size(46, 17);
            this.DateLbl.TabIndex = 6;
            this.DateLbl.Text = "Дата:";
            // 
            // DescriptionLbl
            // 
            this.DescriptionLbl.AutoSize = true;
            this.DescriptionLbl.Location = new System.Drawing.Point(6, 71);
            this.DescriptionLbl.Name = "DescriptionLbl";
            this.DescriptionLbl.Size = new System.Drawing.Size(78, 17);
            this.DescriptionLbl.TabIndex = 4;
            this.DescriptionLbl.Text = "Описание:";
            // 
            // SizeLbl
            // 
            this.SizeLbl.AutoSize = true;
            this.SizeLbl.Location = new System.Drawing.Point(6, 44);
            this.SizeLbl.Name = "SizeLbl";
            this.SizeLbl.Size = new System.Drawing.Size(61, 17);
            this.SizeLbl.TabIndex = 2;
            this.SizeLbl.Text = "Размер:";
            // 
            // NameLbl
            // 
            this.NameLbl.AutoSize = true;
            this.NameLbl.Location = new System.Drawing.Point(6, 18);
            this.NameLbl.Name = "NameLbl";
            this.NameLbl.Size = new System.Drawing.Size(39, 17);
            this.NameLbl.TabIndex = 0;
            this.NameLbl.Text = "Имя:";
            // 
            // TorrentInfo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(664, 452);
            this.Controls.Add(this.ContentsBox);
            this.Controls.Add(this.AddTorrentCancel);
            this.Controls.Add(this.AddTorrentOK);
            this.Name = "TorrentInfo";
            this.Text = "TorrentInfo";
            this.ContentsBox.ResumeLayout(false);
            this.ContentsBox.PerformLayout();
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
    }
}