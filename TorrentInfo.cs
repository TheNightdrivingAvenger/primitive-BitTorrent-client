using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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

        public TorrentInfo(Form owner, Torrent torrent)
        {
            InitializeComponent();

            this.Owner = owner;
            this.pendingTorrent = torrent;

            NameLblContents.Text = torrent.DisplayName;
            SizeLblContents.Text = torrent.TotalSize.ToString();
            DescriptionLblContents.Text = torrent.Comment;

            if (torrent.CreationDate.HasValue)
            {
                DateLblContents.Text = torrent.CreationDate.Value.ToShortDateString() + ", "
                    + torrent.CreationDate.Value.ToShortTimeString();
            }
            else
            {
                DateLblContents.Text = "Неизвестно";
            }
        }

        private void AddTorrentOK_Click(object sender, EventArgs e)
        {
            //FileWorker.AddNewTorrentAsync();
            // add it to GUI, then wait
            ((MainForm)Owner).TorrentSubmitted(pendingTorrent);
            this.Close();
        }
    }
}
