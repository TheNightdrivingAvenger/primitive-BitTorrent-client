using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using BencodeNET.Parsing;
using BencodeNET.Torrents;
using BencodeNET.Objects;
using BencodeNET.Exceptions;
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
        public const string SHARINGMSG = "Sharing";
        public const string CHECKINGMSG = "Checking hashes...";
        public const string DOWNLOADINGMSG = "Downloading...";
        public const string STOPPINGMSG = "Stopping...";
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
                                MessageBox.Show("This session file is corrupted and cannot be read:\r\n" +
                                    fileNames[i], "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    UpdateStatus(downloadingFile, STOPPEDMSG);
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
                if (Path.GetExtension(OpenFileDia.FileName) == ".torrent")
                {
                    Torrent newTorrent = OpenAndParse(OpenFileDia.FileName);
                    if (newTorrent != null)
                    {
                        var InfoWindow = new TorrentInfo(this, newTorrent);
                        InfoWindow.Show();
                    }
                }
                else if (Path.GetExtension(OpenFileDia.FileName) == ".session")
                {
                    try
                    {
                        var dictionary = standardParser.Parse<BDictionary>(File.ReadAllBytes(OpenFileDia.FileName));
                        string rootSessionPath = Path.GetDirectoryName(OpenFileDia.FileName) +
                            Path.DirectorySeparatorChar;
                        Torrent newTorrent = OpenAndParse(rootSessionPath + dictionary.First().Key.ToString());
                        if (newTorrent != null)
                        {
                            ParseSession(dictionary, newTorrent, rootSessionPath);
                            AddNewViewListEntry(filesList.Last.Value);
                            if (filesList.Last.Value.filesCorrupted)
                            {
                                UpdateStatus(filesList.Last.Value, FILESCORRUPTEDMSG);
                            }
                            else
                            {
                                UpdateProgress(filesList.Last.Value);
                            }
                            try
                            {
                                filesList.Last.Value.AddToMainSession();
                            }
                            catch
                            {
                                // cannot add the file to the main session
                                // (disk space or something, just cannot write to the file)
                            }
                        }
                    }
                    catch
                    {
                        MessageBox.Show("This session file cannot be accessed or is corrupted and cannot be read:\r\n" +
                            OpenFileDia.FileName, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        public async void TorrentSubmitted(Torrent torrent, string chosenPath, bool start)
        {
            DownloadingFile newSharedFile;
            try
            {
                newSharedFile = new DownloadingFile(this, torrent, chosenPath, false);
            }
            catch // make catch more specific?
            {
                MessageBox.Show("Could not create some or all of the files.\r\nMake sure the directory is readable" +
                    "and writeable, and that you have enough free disk space.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                fileIsDownloading = true;
                await newSharedFile.StartAsync().ConfigureAwait(false);
            }
        }

        public void PeerConnectedDisconnectedEvent(DownloadingFile sharedFile, int totalPeers)
        {
            string curMsg = "";
            if (totalPeers > 0)
            {
                if (sharedFile.state == DownloadState.completed)
                {
                    curMsg = SHARINGMSG;
                }
                else if (sharedFile.state == DownloadState.downloading)
                {
                    curMsg = DOWNLOADINGMSG;
                }
                else if (sharedFile.state == DownloadState.stopped)
                {
                    curMsg = STOPPEDMSG;
                }
            }
            else
            {
                if (sharedFile.state == DownloadState.completed || sharedFile.state == DownloadState.stopped)
                {
                    curMsg = STOPPEDMSG;
                }
                else
                {
                    curMsg = SEARCHINGPEERSMSG;
                }
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
                Invoke(new MethodInvoker(() => {
                    if (message != null)
                    {
                        FilesArea.Items[sharedFile.listViewEntryID].SubItems[3].Text = message;
                    }
                    ChangeButtonsState();
                }));
            }
            else
            {
                if (message != null)
                {
                    FilesArea.Items[sharedFile.listViewEntryID].SubItems[3].Text = message;
                }
                ChangeButtonsState();
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

        // remove it
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (var file in filesList)
            {
                if (file.state == DownloadState.downloading ||
                    file.state == DownloadState.stopping)
                {
                    MessageBox.Show("Please, stop all pending downloads before exiting", "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                    return;
                }
                file.SerializeToFile();
                file.CloseSession();
            }
            FileWorker.CloseMainSession();
            DownloadingFile.messageHandler.Stop();
        }

        private async void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {

            //FileWorker.
        }

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
            var tempSelected = nowSelected;
            await Task.Run(async () => await tempSelected.StopAsync().ConfigureAwait(false));
            // save the current state to the session file
            tempSelected.SerializeToFile();
            fileIsDownloading = false;
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            fileIsDownloading = true;
            await nowSelected.StartAsync().ConfigureAwait(false);
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Удалить также файл сессии?\r\nПосле его удаления загрузку" +
                " будет невозможно продолжить",
                "Внимание", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                nowSelected.CloseSession();
                nowSelected.RemoveEntry();
                filesList.Remove(nowSelected);
                FilesArea.Items.RemoveAt(nowSelected.listViewEntryID);
            }
            else if (result == DialogResult.No)
            {
                nowSelected.SerializeToFile();
                nowSelected.CloseSession();
                filesList.Remove(nowSelected);
                FilesArea.Items.RemoveAt(nowSelected.listViewEntryID);
            }
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Все загруженные файлы и файл сессии будут удалены\r\n" +
                "Эту операцию невозможно отменить. Продолжить?",
                "Внимание", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                nowSelected.CloseSession();
                nowSelected.RemoveEntry();
                nowSelected.RemoveDownloadedFiles();
                filesList.Remove(nowSelected);
                FilesArea.Items.RemoveAt(nowSelected.listViewEntryID);
            }
        }

        private void FilesArea_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            StartButton.Enabled = StopButton.Enabled = RehashButton.Enabled =
                RemoveButton.Enabled = DeleteButton.Enabled = false;
            nowSelected = null;
            if (e.IsSelected)
            {
                nowSelected = FindEntryByIndex(e.ItemIndex);
                ChangeButtonsState();
            }
        }

        private void ChangeButtonsState()
        {
            if (nowSelected == null)
            {
                return;
            }

            bool stopButtonState = true;
            bool startButtonState = false;

            switch (nowSelected.state)
            {
                case DownloadState.completed:
                case DownloadState.downloading:
                    break;
                case DownloadState.stopping:
                case DownloadState.checking:
                    // if it's stopping or checking hashes, no buttons should be active
                    startButtonState = false;
                    stopButtonState = false;
                    break;
                case DownloadState.stopped:
                    if (!nowSelected.filesCorrupted)
                    {
                        startButtonState = true;
                    }
                    stopButtonState = false;
                    break;
            }
            StartButton.Enabled = RehashButton.Enabled = RemoveButton.Enabled = DeleteButton.Enabled =
                startButtonState;
            StopButton.Enabled = stopButtonState;
        }

        private async void RehashButton_Click(object sender, EventArgs e)
        {
            //UpdateStatus(nowSelected, "Checking hashes...");
            await nowSelected.Rehash();
        }

        private void createANewTorrentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var createTorrentForm = new CreateTorrent();
            createTorrentForm.Show();
        }
    }
}
