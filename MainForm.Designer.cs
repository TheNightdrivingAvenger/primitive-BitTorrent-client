namespace CourseWork
{
    partial class MainForm
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
            this.FilesArea = new System.Windows.Forms.ListView();
            this.TorrentName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.FileSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.FileProgress = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.FileState = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SeedersLeechersRatio = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenFileDia = new System.Windows.Forms.OpenFileDialog();
            this.mainToolStrip = new System.Windows.Forms.ToolStrip();
            this.StartButton = new System.Windows.Forms.ToolStripButton();
            this.StopButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.RehashButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.RemoveButton = new System.Windows.Forms.ToolStripButton();
            this.DeleteButton = new System.Windows.Forms.ToolStripButton();
            this.MainContainer = new System.Windows.Forms.SplitContainer();
            this.InfoTabControl = new System.Windows.Forms.TabControl();
            this.FilesTab = new System.Windows.Forms.TabPage();
            this.FilesInfoList = new System.Windows.Forms.ListView();
            this.FileNames = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.FilesSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.InfoTab = new System.Windows.Forms.TabPage();
            this.InfoContainer = new System.Windows.Forms.Panel();
            this.StatusLbl = new System.Windows.Forms.Label();
            this.PiecesLbl = new System.Windows.Forms.Label();
            this.CommentLbl = new System.Windows.Forms.Label();
            this.InfoHashLbl = new System.Windows.Forms.Label();
            this.CreatedLbl = new System.Windows.Forms.Label();
            this.DownloadPathLbl = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.infoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.mainToolStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.MainContainer)).BeginInit();
            this.MainContainer.Panel1.SuspendLayout();
            this.MainContainer.Panel2.SuspendLayout();
            this.MainContainer.SuspendLayout();
            this.InfoTabControl.SuspendLayout();
            this.FilesTab.SuspendLayout();
            this.InfoTab.SuspendLayout();
            this.InfoContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // FilesArea
            // 
            this.FilesArea.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.TorrentName,
            this.FileSize,
            this.FileProgress,
            this.FileState,
            this.SeedersLeechersRatio});
            this.FilesArea.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FilesArea.FullRowSelect = true;
            this.FilesArea.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.FilesArea.Location = new System.Drawing.Point(0, 0);
            this.FilesArea.MultiSelect = false;
            this.FilesArea.Name = "FilesArea";
            this.FilesArea.Size = new System.Drawing.Size(1097, 239);
            this.FilesArea.TabIndex = 0;
            this.FilesArea.UseCompatibleStateImageBehavior = false;
            this.FilesArea.View = System.Windows.Forms.View.Details;
            this.FilesArea.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.FilesArea_ItemSelectionChanged);
            // 
            // TorrentName
            // 
            this.TorrentName.Text = "Torrent name";
            this.TorrentName.Width = 291;
            // 
            // FileSize
            // 
            this.FileSize.Text = "Size";
            this.FileSize.Width = 80;
            // 
            // FileProgress
            // 
            this.FileProgress.Text = "Completed";
            this.FileProgress.Width = 80;
            // 
            // FileState
            // 
            this.FileState.Text = "State";
            this.FileState.Width = 514;
            // 
            // SeedersLeechersRatio
            // 
            this.SeedersLeechersRatio.Text = "Seeders/Leechers";
            this.SeedersLeechersRatio.Width = 126;
            // 
            // menuStrip1
            // 
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.infoToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1097, 28);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(44, 24);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(216, 26);
            this.openToolStripMenuItem.Text = "&Open...";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // OpenFileDia
            // 
            this.OpenFileDia.DefaultExt = "torrent";
            this.OpenFileDia.Filter = "Torrent-files (*.torrent)|*.torrent|Session files (*.session)|*.session";
            this.OpenFileDia.ShowReadOnly = true;
            // 
            // mainToolStrip
            // 
            this.mainToolStrip.AutoSize = false;
            this.mainToolStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.mainToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.StartButton,
            this.StopButton,
            this.toolStripSeparator1,
            this.RehashButton,
            this.toolStripSeparator2,
            this.RemoveButton,
            this.DeleteButton});
            this.mainToolStrip.Location = new System.Drawing.Point(0, 28);
            this.mainToolStrip.Name = "mainToolStrip";
            this.mainToolStrip.Size = new System.Drawing.Size(1097, 35);
            this.mainToolStrip.TabIndex = 2;
            // 
            // StartButton
            // 
            this.StartButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.StartButton.Enabled = false;
            this.StartButton.Image = global::CourseWork.Properties.Resources.play_arrow;
            this.StartButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.StartButton.Name = "StartButton";
            this.StartButton.Size = new System.Drawing.Size(24, 32);
            this.StartButton.ToolTipText = "Start downloading";
            this.StartButton.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // StopButton
            // 
            this.StopButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.StopButton.Enabled = false;
            this.StopButton.Image = global::CourseWork.Properties.Resources.stop;
            this.StopButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.StopButton.Name = "StopButton";
            this.StopButton.Size = new System.Drawing.Size(24, 32);
            this.StopButton.ToolTipText = "Stop downloading";
            this.StopButton.Click += new System.EventHandler(this.StopButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 35);
            // 
            // RehashButton
            // 
            this.RehashButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.RehashButton.Enabled = false;
            this.RehashButton.Image = global::CourseWork.Properties.Resources.rehash;
            this.RehashButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.RehashButton.Name = "RehashButton";
            this.RehashButton.Size = new System.Drawing.Size(24, 32);
            this.RehashButton.ToolTipText = "Rehash downloaded files";
            this.RehashButton.Click += new System.EventHandler(this.RehashButton_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 35);
            // 
            // RemoveButton
            // 
            this.RemoveButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.RemoveButton.Enabled = false;
            this.RemoveButton.Image = global::CourseWork.Properties.Resources.cancel;
            this.RemoveButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.RemoveButton.Name = "RemoveButton";
            this.RemoveButton.Size = new System.Drawing.Size(24, 32);
            this.RemoveButton.ToolTipText = "Remove from program, keep downloaded files";
            this.RemoveButton.Click += new System.EventHandler(this.RemoveButton_Click);
            // 
            // DeleteButton
            // 
            this.DeleteButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.DeleteButton.Enabled = false;
            this.DeleteButton.Image = global::CourseWork.Properties.Resources.delete;
            this.DeleteButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.DeleteButton.Name = "DeleteButton";
            this.DeleteButton.Size = new System.Drawing.Size(24, 32);
            this.DeleteButton.ToolTipText = "Delete downloaded files and remove from program";
            this.DeleteButton.Click += new System.EventHandler(this.DeleteButton_Click);
            // 
            // MainContainer
            // 
            this.MainContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MainContainer.Location = new System.Drawing.Point(0, 63);
            this.MainContainer.Name = "MainContainer";
            this.MainContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // MainContainer.Panel1
            // 
            this.MainContainer.Panel1.Controls.Add(this.FilesArea);
            // 
            // MainContainer.Panel2
            // 
            this.MainContainer.Panel2.Controls.Add(this.InfoTabControl);
            this.MainContainer.Size = new System.Drawing.Size(1097, 478);
            this.MainContainer.SplitterDistance = 239;
            this.MainContainer.TabIndex = 3;
            this.MainContainer.TabStop = false;
            // 
            // InfoTabControl
            // 
            this.InfoTabControl.Controls.Add(this.FilesTab);
            this.InfoTabControl.Controls.Add(this.InfoTab);
            this.InfoTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.InfoTabControl.Location = new System.Drawing.Point(0, 0);
            this.InfoTabControl.Name = "InfoTabControl";
            this.InfoTabControl.SelectedIndex = 0;
            this.InfoTabControl.Size = new System.Drawing.Size(1097, 235);
            this.InfoTabControl.TabIndex = 0;
            // 
            // FilesTab
            // 
            this.FilesTab.Controls.Add(this.FilesInfoList);
            this.FilesTab.Location = new System.Drawing.Point(4, 25);
            this.FilesTab.Name = "FilesTab";
            this.FilesTab.Padding = new System.Windows.Forms.Padding(3);
            this.FilesTab.Size = new System.Drawing.Size(1089, 206);
            this.FilesTab.TabIndex = 0;
            this.FilesTab.Text = "Files";
            this.FilesTab.UseVisualStyleBackColor = true;
            // 
            // FilesInfoList
            // 
            this.FilesInfoList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.FileNames,
            this.FilesSize});
            this.FilesInfoList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FilesInfoList.FullRowSelect = true;
            this.FilesInfoList.Location = new System.Drawing.Point(3, 3);
            this.FilesInfoList.Name = "FilesInfoList";
            this.FilesInfoList.Size = new System.Drawing.Size(1083, 200);
            this.FilesInfoList.TabIndex = 0;
            this.FilesInfoList.UseCompatibleStateImageBehavior = false;
            this.FilesInfoList.View = System.Windows.Forms.View.Details;
            // 
            // FileNames
            // 
            this.FileNames.Text = "File name";
            this.FileNames.Width = 918;
            // 
            // FilesSize
            // 
            this.FilesSize.Text = "Size";
            this.FilesSize.Width = 105;
            // 
            // InfoTab
            // 
            this.InfoTab.Controls.Add(this.InfoContainer);
            this.InfoTab.Location = new System.Drawing.Point(4, 25);
            this.InfoTab.Name = "InfoTab";
            this.InfoTab.Padding = new System.Windows.Forms.Padding(3);
            this.InfoTab.Size = new System.Drawing.Size(1089, 206);
            this.InfoTab.TabIndex = 1;
            this.InfoTab.Text = "Info";
            this.InfoTab.UseVisualStyleBackColor = true;
            // 
            // InfoContainer
            // 
            this.InfoContainer.AutoScroll = true;
            this.InfoContainer.Controls.Add(this.StatusLbl);
            this.InfoContainer.Controls.Add(this.PiecesLbl);
            this.InfoContainer.Controls.Add(this.CommentLbl);
            this.InfoContainer.Controls.Add(this.InfoHashLbl);
            this.InfoContainer.Controls.Add(this.CreatedLbl);
            this.InfoContainer.Controls.Add(this.DownloadPathLbl);
            this.InfoContainer.Controls.Add(this.label6);
            this.InfoContainer.Controls.Add(this.label5);
            this.InfoContainer.Controls.Add(this.label4);
            this.InfoContainer.Controls.Add(this.label3);
            this.InfoContainer.Controls.Add(this.label2);
            this.InfoContainer.Controls.Add(this.label1);
            this.InfoContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.InfoContainer.Location = new System.Drawing.Point(3, 3);
            this.InfoContainer.Name = "InfoContainer";
            this.InfoContainer.Size = new System.Drawing.Size(1083, 200);
            this.InfoContainer.TabIndex = 0;
            // 
            // StatusLbl
            // 
            this.StatusLbl.AutoSize = true;
            this.StatusLbl.Location = new System.Drawing.Point(117, 89);
            this.StatusLbl.Name = "StatusLbl";
            this.StatusLbl.Size = new System.Drawing.Size(0, 17);
            this.StatusLbl.TabIndex = 11;
            // 
            // PiecesLbl
            // 
            this.PiecesLbl.AutoSize = true;
            this.PiecesLbl.Location = new System.Drawing.Point(117, 72);
            this.PiecesLbl.Name = "PiecesLbl";
            this.PiecesLbl.Size = new System.Drawing.Size(0, 17);
            this.PiecesLbl.TabIndex = 10;
            // 
            // CommentLbl
            // 
            this.CommentLbl.AutoSize = true;
            this.CommentLbl.Location = new System.Drawing.Point(117, 55);
            this.CommentLbl.Name = "CommentLbl";
            this.CommentLbl.Size = new System.Drawing.Size(0, 17);
            this.CommentLbl.TabIndex = 9;
            // 
            // InfoHashLbl
            // 
            this.InfoHashLbl.AutoSize = true;
            this.InfoHashLbl.Location = new System.Drawing.Point(117, 38);
            this.InfoHashLbl.Name = "InfoHashLbl";
            this.InfoHashLbl.Size = new System.Drawing.Size(0, 17);
            this.InfoHashLbl.TabIndex = 8;
            // 
            // CreatedLbl
            // 
            this.CreatedLbl.AutoSize = true;
            this.CreatedLbl.Location = new System.Drawing.Point(117, 21);
            this.CreatedLbl.Name = "CreatedLbl";
            this.CreatedLbl.Size = new System.Drawing.Size(0, 17);
            this.CreatedLbl.TabIndex = 7;
            // 
            // DownloadPathLbl
            // 
            this.DownloadPathLbl.AutoSize = true;
            this.DownloadPathLbl.Location = new System.Drawing.Point(117, 4);
            this.DownloadPathLbl.Name = "DownloadPathLbl";
            this.DownloadPathLbl.Size = new System.Drawing.Size(0, 17);
            this.DownloadPathLbl.TabIndex = 6;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label6.Location = new System.Drawing.Point(5, 89);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(59, 17);
            this.label6.TabIndex = 5;
            this.label6.Text = "Status:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(5, 72);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(54, 17);
            this.label5.TabIndex = 4;
            this.label5.Text = "Pieces:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(5, 55);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(71, 17);
            this.label4.TabIndex = 3;
            this.label4.Text = "Comment:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(5, 38);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(70, 17);
            this.label3.TabIndex = 2;
            this.label3.Text = "Info hash:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(5, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(62, 17);
            this.label2.TabIndex = 1;
            this.label2.Text = "Created:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(5, 4);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(106, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "Download path:";
            // 
            // infoToolStripMenuItem
            // 
            this.infoToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem});
            this.infoToolStripMenuItem.Name = "infoToolStripMenuItem";
            this.infoToolStripMenuItem.Size = new System.Drawing.Size(47, 24);
            this.infoToolStripMenuItem.Text = "&Info";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(216, 26);
            this.aboutToolStripMenuItem.Text = "About...";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1097, 541);
            this.Controls.Add(this.MainContainer);
            this.Controls.Add(this.mainToolStrip);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainForm";
            this.Text = "Primitive BT Client";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Shown += new System.EventHandler(this.MainForm_Shown);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.mainToolStrip.ResumeLayout(false);
            this.mainToolStrip.PerformLayout();
            this.MainContainer.Panel1.ResumeLayout(false);
            this.MainContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.MainContainer)).EndInit();
            this.MainContainer.ResumeLayout(false);
            this.InfoTabControl.ResumeLayout(false);
            this.FilesTab.ResumeLayout(false);
            this.InfoTab.ResumeLayout(false);
            this.InfoContainer.ResumeLayout(false);
            this.InfoContainer.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView FilesArea;
        private System.Windows.Forms.ColumnHeader TorrentName;
        private System.Windows.Forms.ColumnHeader FileSize;
        private System.Windows.Forms.ColumnHeader FileState;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog OpenFileDia;
        private System.Windows.Forms.ColumnHeader SeedersLeechersRatio;
        private System.Windows.Forms.ToolStrip mainToolStrip;
        private System.Windows.Forms.ToolStripButton StartButton;
        private System.Windows.Forms.ToolStripButton StopButton;
        private System.Windows.Forms.ToolStripButton RemoveButton;
        private System.Windows.Forms.ToolStripButton DeleteButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ColumnHeader FileProgress;
        private System.Windows.Forms.ToolStripButton RehashButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.SplitContainer MainContainer;
        private System.Windows.Forms.TabControl InfoTabControl;
        private System.Windows.Forms.TabPage FilesTab;
        private System.Windows.Forms.TabPage InfoTab;
        private System.Windows.Forms.ListView FilesInfoList;
        private System.Windows.Forms.ColumnHeader FileNames;
        private System.Windows.Forms.ColumnHeader FilesSize;
        private System.Windows.Forms.Panel InfoContainer;
        private System.Windows.Forms.Label StatusLbl;
        private System.Windows.Forms.Label PiecesLbl;
        private System.Windows.Forms.Label CommentLbl;
        private System.Windows.Forms.Label InfoHashLbl;
        private System.Windows.Forms.Label CreatedLbl;
        private System.Windows.Forms.Label DownloadPathLbl;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ToolStripMenuItem infoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
    }
}

