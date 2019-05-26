using System;
using System.IO;
using System.Windows.Forms;
using BencodeNET.Torrents;

namespace CourseWork
{
    public partial class TorrentInfo : Form
    {
        private Torrent pendingTorrent;

        public TorrentInfo(Form owner, Torrent torrent)
        {
            InitializeComponent();
            
            this.Owner = owner;
            this.pendingTorrent = torrent;

            NameLblContents.Text = torrent.DisplayName;
            SizeLblContents.Text = MainForm.GetAppropriateSizeForm(torrent.TotalSize);
            DescriptionLblContents.Text = torrent.Comment;

            if (torrent.CreationDate.HasValue)
            {
                DateLblContents.Text = torrent.CreationDate.Value.ToShortDateString() + ", "
                    + torrent.CreationDate.Value.ToShortTimeString();
            }
            else
            {
                DateLblContents.Text = "Unknown";
            }
        }

        private void AddTorrentOK_Click(object sender, EventArgs e)
        {
            if (CreateSubFolder.Checked)
            {
                string cleanPath = FileWorker.ClearPath(SubFolderName.Text);
                if (cleanPath == "")
                {
                    MessageBox.Show("Invalid subfolder name provided", "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                else
                {
                    if (cleanPath != SubFolderName.Text)
                    {
                        MessageBox.Show("Invalid path characters has been replaced with \'_\'", "Notice",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    SubFolderName.Text = cleanPath;
                }
            }
            else
            {
                SubFolderName.Text = "";
            }
            ((MainForm)Owner).TorrentSubmitted(pendingTorrent, DownloadPath.Text,
                StartDownloading.Checked, SubFolderName.Text);
            this.Close();
        }

        private void AddTorrentCancel_Click(object sender, EventArgs e)
        {
            pendingTorrent = null;
            this.Close();
        }

        private void BrowseFolderButton_Click(object sender, EventArgs e)
        {
            if (DownloadPathDialog.ShowDialog() == DialogResult.OK)
            {
                DownloadPath.Text = DownloadPathDialog.SelectedPath + Path.DirectorySeparatorChar;
                AddTorrentOK.Enabled = true;
            }
        }

        private void CreateSubFolder_CheckedChanged(object sender, EventArgs e)
        {
            SubFolderName.Enabled = CreateSubFolder.Checked;
        }

        private void TorrentInfo_Shown(object sender, EventArgs e)
        {
            SubFolderName.Text = pendingTorrent.DisplayName;
        }
    }
}
