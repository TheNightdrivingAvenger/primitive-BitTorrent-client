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
using System.IO;

namespace CourseWork
{
    // Session information is stored in one main file. This main file
    // has references to other session files, each one describing it's own download.
    // In their turn, session files contain a benc. dictionary with the key containing a path to torrent-file
    // and the value containing a benc. string with values "0" or "1" representing state of each piece
    public partial class MainForm : Form
    {
        public const string FILESCORRUPTEDMSG = "Files missing or corrupted. Rehashing is needed\r\n";
        public const string CONNTOTRACKERMSG = "Connecting to tracker(-s)...";
        public const string NOTRACKER = "There was an error while trying to connect to the tracker.\r\n" +
                    "It could happen if you're not connected to the Internet, there was an internal tracker error, or this tracker doesn't exist anymore";
        public const string INVALTRACKRESPMSG = "Tracker's response is invalid! Try again later";
        public const string TRACKERERRORMSG = "Tracker responded with an error:\r\n";
        public const string NOPEERSMSG = "No peers found, try again later";
        public const string SEARCHINGPEERSMSG = "Searching peers...";
        //private const string CONNTOPEERSMSG = "Connecting to peers...";
        public const string DOWNLOADINGMSG = "Downloading...";
        public const string STOPPEDMSG = "Stopped";

        private LinkedList<DownloadingFile> filesList;
        public string myPeerID { get; private set; }

        private DownloadingFile nowSelected;
        //private static MessageHandler messageHandler;
        //private LinkedList<ListViewItem> filesView;

        private bool fileIsDownloading;

        private BencodeParser standardParser;

        public MainForm()
        {
            fileIsDownloading = false;

            standardParser = new BencodeParser();
            if (!RestoreSessionIfPresent())
            {
                filesList = new LinkedList<DownloadingFile>();
            }
            myPeerID = GeneratePeerID();
            DownloadingFile.messageHandler = new MessageHandler(400, filesList);
            if (!DownloadingFile.messageHandler.isStarted)
            {
                DownloadingFile.messageHandler.Start();
            }
            try
            {
                FileWorker.CreateMainSession();
            }
            catch
            {
                // cannot create new main session; it won't be saved
                // old file hasn't been rewritten, so restart the program
                // in new location or something
            }
            nowSelected = null;
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

        private bool RestoreSessionIfPresent()
        {
            byte[][] sessions = FileWorker.LoadMainSession(out string[] fileNames);
            if (fileNames != null)
            {
                string lostFiles = "";
                for (int i = 0; i < fileNames.Length; i++)
                {
                    if (sessions[i] == null)
                    {
                        lostFiles += fileNames[i] + Environment.NewLine;
                    }
                }
                if (lostFiles != "")
                {
                    MessageBox.Show("The following session files: " + lostFiles + "has been lost or corrupted", "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                if (sessions != null)
                {
                    filesList = new LinkedList<DownloadingFile>();
                    for (int i = 0; i < sessions.GetLength(0); i++)
                    {
                        if (sessions[i] != null)
                        {
                            try
                            {
                                var dictionary = standardParser.Parse<BDictionary>(sessions[i]);
                                string rootSessionPath = Path.GetDirectoryName(fileNames[i]) +
                                    Path.DirectorySeparatorChar;
                                Torrent newTorrent = OpenAndParse(rootSessionPath + dictionary.First().Key.ToString());
                                if (newTorrent != null)
                                {
                                    ParseSession(dictionary, newTorrent, rootSessionPath);
                                }
                            }
                            catch
                            {
                                // session file corrupted, show it! Name is in fileNames[i]
                            }
                        }
                    }
                    return true;
                }
            }
            // no session files found OR all of them were corrupted
            return false;
        }

        private Torrent OpenAndParse(string sessionFileName)
        {
            try
            {
                var torrent = standardParser.Parse<Torrent>(sessionFileName);
                return torrent;
            }
            catch (BencodeException)
            {
                MessageBox.Show(this, "There was an error while trying to process the file.\r\nMake sure it is a valid Torrent-file\r\n" +
                    sessionFileName, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            catch (IOException)
            {
                MessageBox.Show("There was an error while trying to open the file.\r\nMake sure file exists and is available for reading\r\n" +
                    sessionFileName, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        // can use BString contents, or at least dictionary's value, not the whole dict
        private void ParseSession(BDictionary contents, Torrent newTorrent, string sessionPath)
        {
            string piecesState = standardParser.Parse<BString>(contents.First().Value.EncodeAsBytes()).ToString();
            var sharedFile = new DownloadingFile(this, newTorrent, piecesState, sessionPath);
            filesList.AddLast(sharedFile);
        }

        //private void ParseSession(BDictionary contents, Torrent newTorrent, string sessionPath)
        //{
        //    var list = standardParser.Parse<BList>(contents.First().Value.EncodeAsBytes());
        //    if (list[0] is BString)
        //    {
        //        string piecesState = list[0].EncodeAsString();
        //        if (list[1] is BString)
        //        {
        //            string SHA1 = list[1].EncodeAsString();
        //            var sharedFile = new DownloadingFile(this, newTorrent, piecesState, SHA1,
        //                Path.GetDirectoryName(sessionPath));
        //            filesList.AddLast(sharedFile);
        //        }
        //        else
        //        {
        //            throw new ArgumentException("Invalid dictionary contents");
        //        }
        //    }
        //    else
        //    {
        //        throw new ArgumentException("Invalid dictionary contents");
        //    }
        //}

        private void MainForm_Shown(object sender, EventArgs e)
        {
            foreach (var downloadingFile in filesList)
            {
                AddNewViewListEntry(downloadingFile);
                if (downloadingFile.filesCorrupted)
                {
                    UpdateStatus(downloadingFile, FILESCORRUPTEDMSG);
                }
                else
                {
                    UpdateProgress(downloadingFile);
                }
                try
                {
                    downloadingFile.AddToMainSession();
                }
                catch
                {
                    // cannot add the file to the main session
                    // (disk space or something, just cannot write to the file)
                }
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenFileDia.ShowDialog() == DialogResult.OK)
            {
                Torrent newTorrent = OpenAndParse(OpenFileDia.FileName);
                if (newTorrent != null)
                {
                    var InfoWindow = new TorrentInfo(this, newTorrent);
                    InfoWindow.Show();
                }
            }
        }

        public async void TorrentSubmitted(Torrent torrent, string chosenPath, bool start)
        {
            // add copying file to program's location (so we can keep track of opened torrents
            // AND be independent from original file)
            DownloadingFile newSharedFile;
            try
            {
                newSharedFile = new DownloadingFile(this, torrent, chosenPath, false);
            }
            catch // make catch more specific?
            {
                // something went wrong, tell the user
                return;
            }
            AddNewViewListEntry(newSharedFile);
            await AddNewTorrentAsync(newSharedFile, start).ConfigureAwait(false);
        }

        private void AddNewViewListEntry(DownloadingFile newSharedFile)
        {
            // ВСЕ столбцы строки ListView индексируются (с 0)
            ListViewItem newFile = new ListViewItem(newSharedFile.torrentContents.DisplayName);
            newFile.SubItems.Add(GetAppropriateSizeForm(newSharedFile.totalSize));
            newFile.SubItems.Add("0.00%");
            newFile.SubItems.Add(STOPPEDMSG);
            newFile.SubItems.Add("");
            FilesArea.Items.Add(newFile);

            newSharedFile.listViewEntryID = FilesArea.Items.IndexOf(newFile);
        }

        private async Task AddNewTorrentAsync(DownloadingFile newSharedFile, bool start)
        {
            filesList.AddLast(newSharedFile);
            newSharedFile.AddToMainSession();
            if (start && !fileIsDownloading)
            {
                StartButton.Enabled = false;
                await newSharedFile.StartAsync().ConfigureAwait(false);
            }
        }

        public void PeerConnectedDisconnectedEvent(DownloadingFile sharedFile, int totalPeers)
        {
            string curMsg;
            if (totalPeers > 0)
            {
                curMsg = DOWNLOADINGMSG;
            }
            else if (totalPeers == 0 && sharedFile.state != DownloadState.stopped)
            {
                curMsg = SEARCHINGPEERSMSG;
            }
            else
            {
                curMsg = STOPPEDMSG;
            }
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => FilesArea.Items[sharedFile.listViewEntryID].SubItems[3].Text =
                    curMsg));
            }
            else
            {
                FilesArea.Items[sharedFile.listViewEntryID].SubItems[3].Text = curMsg;
            }
        }

        public void UpdateProgress(DownloadingFile sharedFile)
        {
            string curMsg = Math.Round(sharedFile.downloaded / (double)sharedFile.totalSize * 100, 2) + "%";

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
                Invoke(new MethodInvoker(() => FilesArea.Items[sharedFile.listViewEntryID].SubItems[3].Text = message));
            }
            else
            {
                FilesArea.Items[sharedFile.listViewEntryID].SubItems[3].Text = message;
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
            //foreach (var file in filesList)
            //{
            //    if (file.state == DownloadState.downloading)
            //    {
            //        await file.StopAsync().ConfigureAwait(false);
            //        file.SerializeToFile();
            //    }
            //    file.CloseSession();
            //}
            //FileWorker.CloseMainSession();
            //DownloadingFile.messageHandler.Stop();
        }

        private async void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            foreach (var file in filesList)
            {
                if (file.state == DownloadState.downloading)
                {
                    await file.StopAsync().ConfigureAwait(false);
                }
                file.SerializeToFile();
                file.CloseSession();
            }
            FileWorker.CloseMainSession();
            DownloadingFile.messageHandler.Stop();
            //FileWorker.
        }

        // some kind of race condition when file is downloaded.. sometimes progress bar doesn't update the last one?
        // never could repeat, so let's see

        private DownloadingFile FindEntryByIndex(int index)
        {
            DownloadingFile found = null;
            foreach (var sharedFile in filesList)
            {
                if (sharedFile.listViewEntryID == index)
                {
                    found = sharedFile;
                    break;
                }
            }
            return found;
        }

        private async void StopButton_Click(object sender, EventArgs e)
        {
            StartButton.Enabled = RehashButton.Enabled = RemoveButton.Enabled = DeleteButton.Enabled = true;
            StopButton.Enabled = false;
            fileIsDownloading = false;
            await nowSelected.StopAsync().ConfigureAwait(false);
            // save the current state to the session file
            nowSelected.SerializeToFile();
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            StartButton.Enabled = RehashButton.Enabled = RemoveButton.Enabled = DeleteButton.Enabled = false;
            StopButton.Enabled = true;
            fileIsDownloading = true;
            await nowSelected.StartAsync().ConfigureAwait(false);
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {

        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {

        }

        private void FilesArea_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            StartButton.Enabled = StopButton.Enabled = RehashButton.Enabled =
                RemoveButton.Enabled = DeleteButton.Enabled = false;

            if (e.IsSelected)
            {
                nowSelected = FindEntryByIndex(e.ItemIndex);
                StartButton.Enabled = RehashButton.Enabled = RemoveButton.Enabled = DeleteButton.Enabled =
                    (nowSelected.state == DownloadState.stopped);
                StopButton.Enabled = (nowSelected.state == DownloadState.downloading);
            }
        }

        private async void RehashButton_Click(object sender, EventArgs e)
        {
            UpdateStatus(nowSelected, "Checking hashes...");
            await nowSelected.Rehash();
            UpdateProgress(nowSelected);
            UpdateStatus(nowSelected, STOPPEDMSG);
        }
    }
}
