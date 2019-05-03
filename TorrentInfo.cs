using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using BencodeNET.Parsing;
using BencodeNET.Torrents;

namespace CourseWork
{
    public partial class TorrentInfo : Form
    {
        private Torrent pendingTorrent;
        private string chosenPath;
        //public bool createSubFolder { get; private set; }

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
            //FileWorker.AddNewTorrentAsync();
            // add it to GUI, then wait
            ((MainForm)Owner).TorrentSubmitted(pendingTorrent, chosenPath);
            this.Close();
        }

        private void AddTorrentCancel_Click(object sender, EventArgs e)
        {
            pendingTorrent = null;
            this.Close();
        }

        private void TorrentInfo_Load(object sender, EventArgs e)
        {

        }

        private void BrowseFolderButton_Click(object sender, EventArgs e)
        {
            if (DownloadPathDialog.ShowDialog() == DialogResult.OK)
            {
                string tail = "";
                if (CreateSubFolder.Checked)
                {
                    tail = pendingTorrent.DisplayName;
                }
                chosenPath = DownloadPathDialog.SelectedPath + Path.DirectorySeparatorChar + FileWorker.ClearPath(tail);
                DownloadPath.Text = chosenPath;
                AddTorrentOK.Enabled = true;
            }
        }
    }
}
