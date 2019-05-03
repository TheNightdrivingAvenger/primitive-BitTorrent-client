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
using BencodeNET.Objects;
using BencodeNET.Exceptions;
using System.Net;
using System.Net.Sockets;
using CourseWork;

namespace CourseWork
{
    /*
     * WATCH OUT FOR DAMN CHARSETS! RUTRACKER'S TRACKER RETUNRS IN CP-1251 FOR SOME REASON,
     * AND PARSER BY DEFAULT WORKS WITH UTF8. I GUESS I NEED TO EXPLICITLY CHECK THE ENCODING
     * OF RECIEVED RESPONSE AND ADJUST PARSER PROPERLY!!!
     */
    public partial class MainForm : Form
    {
        public const string CONNTOTRACKERMSG = "Connecting to tracker(-s)...";
        public const string NOTRACKER = "There was an error while trying to connect to the tracker.\r\n" +
                    "It could happen if you're not connected to the Internet, there was an internal tracker error, or this tracker doesn't exist anymore";
        public const string INVALTRACKRESPMSG = "Tracker's response is invalid! Try again later";
        public const string NOPEERSMSG = "No peers found, try again later";
        public const string SEARCHINGPEERSMSG = "Searching peers...";
        //private const string CONNTOPEERSMSG = "Connecting to peers...";
        public const string DOWNLOADINGMSG = "Downloading...";
        public const string STOPPEDMSG = "Stopped";

        private LinkedList<DownloadingFile> filesList;
        public string myPeerID { get; private set; }

        private static MessageHandler messageHandler;
        //private LinkedList<ListViewItem> filesView;

        public MainForm()
        {
            filesList = new LinkedList<DownloadingFile>();
            myPeerID = GeneratePeerID();
            DownloadingFile.messageHandler = new MessageHandler(400, filesList);
            if (!DownloadingFile.messageHandler.isStarted)
            {
                DownloadingFile.messageHandler.Start();
            }
            InitializeComponent();
        }

        private string GeneratePeerID()
        {
            var random = new Random();
            string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            string result = "-VS0001-";
            for (int i = 8; i < 20; i++)
            {
                result += chars[random.Next(0, chars.Length)];
            }
            return result;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            //F:\\CWTorrentTest
            // reading from session file to pick up all downloading torrents
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenFileDia.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var parser = new BencodeParser();
                    var torrent = parser.Parse<Torrent>(OpenFileDia.FileName);
                    var InfoWindow = new TorrentInfo(this, torrent);

                    InfoWindow.Show();
                }
                catch (BencodeException)
                {
                    MessageBox.Show(this, "There was an error while trying to process the file;\r\nMake sure it is a valid Torrent-file",
                        "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (System.IO.IOException)
                {
                    MessageBox.Show("There was an error while trying to open the file;\r\nMake sure file exists and is available for reading",
                        "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        public async void TorrentSubmitted(Torrent torrent, string chosenPath)
        {
            await AddNewTorrentAsync(torrent, chosenPath);
        }

        private async Task AddNewTorrentAsync(Torrent torrent, string chosenPath)
        {
            // add copying file to program's location (so we can keep track of opened torrents
            // AND be independent from original file)
        
            // ВСЕ столбцы строки ListView индексируются (с 0)
            ListViewItem newFile = new ListViewItem(torrent.DisplayName);
            newFile.SubItems.Add(GetAppropriateSizeForm(torrent.TotalSize));
            newFile.SubItems.Add(STOPPEDMSG);
            newFile.SubItems.Add("");
            FilesArea.Items.Add(newFile);

            var newSharedFile = new DownloadingFile(this, FilesArea.Items.IndexOf(newFile), torrent, chosenPath);

            // idk if thread safety is needed. GUI windows are in the same thread, will anyone else use this list?..
            filesList.AddLast(newSharedFile);

            // checkbox "Start downloading?" when adding
            await StartDownloading(newSharedFile).ConfigureAwait(false);
        }

        private async Task StartDownloading(DownloadingFile sharedFile)
        {
            await sharedFile.StartAsync();
        }

        public void PeerConnectedDisconnectedEvent(DownloadingFile sharedFile, int totalPeers)
        {
            string curMsg;
            if (totalPeers > 0)
            {
                curMsg = DOWNLOADINGMSG + " " + Math.Round(sharedFile.downloaded / (double)sharedFile.totalSize * 100, 2) + "%";
            }
            else if (totalPeers == 0 && sharedFile.state != DownloadState.stopped)
            {
                curMsg = SEARCHINGPEERSMSG + "; downloaded " + Math.Round(sharedFile.downloaded / (double)sharedFile.totalSize * 100, 2) + "%";
            }
            else
            {
                curMsg = "Stopped; downloaded " + Math.Round(sharedFile.downloaded / (double)sharedFile.totalSize * 100, 2) + "%";
            }
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => FilesArea.Items[sharedFile.listViewEntryID].SubItems[2].Text =
                    curMsg));
            }
            else
            {
                FilesArea.Items[sharedFile.listViewEntryID].SubItems[2].Text = curMsg;
            }
        }

        public void UpdateProgress(DownloadingFile sharedFile)
        {
            string curMsg = DOWNLOADINGMSG + " " + Math.Round(sharedFile.downloaded / (double)sharedFile.totalSize * 100, 2) + "%";

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => FilesArea.Items[sharedFile.listViewEntryID].SubItems[2].Text =
                    curMsg));
            }
            else
            {
                FilesArea.Items[sharedFile.listViewEntryID].SubItems[2].Text = curMsg;
            }
        }

        public void UpdateStatus(DownloadingFile sharedFile, string message)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => FilesArea.Items[sharedFile.listViewEntryID].SubItems[2].Text = message));
            }
            else
            {
                FilesArea.Items[sharedFile.listViewEntryID].SubItems[2].Text = message;
            }
        }

        public void ShowError(string message)
        {
            MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public void UpdateSeedersNum(DownloadingFile sharedFile, string seeders)
        {

        }

        public void UpdateLeechersNum(DownloadingFile sharedFile, string leechers)
        {

        }

        /* gets IPv4:Port from 6-bytes array
         * IPAddress' constructor takes byte array in network byte order,
         * but IPEndPoint's constructor takes port in native machine byte order
         */
        private LinkedListNode<IPEndPoint> GetPeerFromBytes(byte[] peer)
        {
            byte[] IPArr = new byte[4];
            Array.Copy(peer, IPArr, 4);
            int port = peer[4] * 256 + peer[5];
            return new LinkedListNode<IPEndPoint>(new IPEndPoint(new IPAddress(IPArr), port));
        }

        public static string GetAppropriateSizeForm(long size)
        {
            const double bytesInGiB = 1073741824;
            const double bytesInMiB = 1048576;
            const double bytesInKiB = 1024;
            if (size > bytesInGiB)
            {
                return Math.Round(size / bytesInGiB, 2).ToString() + "GiB";
            } else if ((size < bytesInGiB) && (size > bytesInMiB))
            {
                return Math.Round(size / bytesInMiB, 2).ToString() + "MiB";
            } else if ((size < bytesInMiB) && (size > bytesInKiB))
            {
                return Math.Round(size / bytesInKiB, 2).ToString() + "KiB";
            } else
            {
                return size.ToString() + "Bytes";
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            DownloadingFile.messageHandler.Stop();
            // TODO: close downloading file (file stream(-s)) and all this stuff
        }

        // TODO: some kind of race condition when file is downloaded.. sometimes progress bar doesn't update the last one

        private void StopButton_Click(object sender, EventArgs e)
        {
            // TODO: add streams flushing on stop
            // find out what file has been selected, then call Stop method
            filesList.ElementAt(0).Stop();
        }
    }
}
