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
        private const string CONNTOTRACKERMSG = "Connecting to tracker(-s)...";
        private const string INVALTRACKRESPMSG = "Tracker's response is invalid! Try again later";
        private const string NOPEERSMSG = "No peers found, try again later";
        private const string CONNTOPEERSMSG = "Connecting to peers...";
        private const string DOWNLOADINGMSG = "Downloading...";
        private const string STOPPEDMSG = "Stopped";

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
                                // == CANCEL IS TEMPORARY! OFC SHOULD BE !=
            if (OpenFileDia.ShowDialog() == DialogResult.Cancel)
            {
                //FileWorker.OpenFileAction(OpenFileDia.FileName);
                //try
                //{
                var parser = new BencodeParser();
                // exceptions: FileNotFound (System.IO, have a look!)
                // BencodeNET.Exceptions.InvalidBencodeException
                var torrent = parser.Parse<Torrent>("F:\\test.torrent");//F:\\Music\\(Alternative Rock Modern Rock) Poets Of The Fall - Ultraviolet - 2018, MP3 (tracks), 320 kbps [rutracker-5623339].torrent");
                var InfoWindow = new TorrentInfo(this, torrent);

                InfoWindow.Show();

                //} catch (Exception ex) // catch all the needed exceptions
                //{
                //    MessageBox.Show(ex.Message);
                //}
            }
        }

        public async void TorrentSubmitted(Torrent torrent)
        {
            await AddNewTorrentAsync(torrent);
        }

        private async Task AddNewTorrentAsync(Torrent torrent)
        {
            // add copying file to program's location (so we can keep track of opened torrents
            // AND be independent from original file)

            var newSharedFile = new DownloadingFile(this, torrent,
                "F:\\test.torrent", "F:\\TORRENTTEST");//"F:\\Music\\(Alternative Rock Modern Rock) Poets Of The Fall - Ultraviolet - 2018, MP3 (tracks), 320 kbps [rutracker-5623339]", "F:\\TORRENTTEST");

            // idk if thread safety is needed. GUI windows are in the same thread, will anyone else use this list?..
            lock (filesList)
            {
                filesList.AddLast(newSharedFile);
            }            
            // ВСЕ столбцы строки ListView индексируются (с 0)
            ListViewItem newFile = new ListViewItem(torrent.DisplayName);
            newFile.SubItems.Add(GetAppropriateSizeForm(torrent.TotalSize));
            newFile.SubItems.Add(CONNTOTRACKERMSG);
            newFile.SubItems.Add("0");

            //newFile.SubItems.Add("");
            //filesView.AddFirst(newFile);
            FilesArea.Items.Add(newFile);
            // бывают исключения при отправке запроса!.. WebException -- не получиолсь разрешить DNS
            try
            {
                await EstablishTrackerConnectionAsync(torrent, newSharedFile, newFile).ConfigureAwait(false);
            } catch (InvalidBencodeException<BObject> exc)
            {
                MessageBox.Show(INVALTRACKRESPMSG + exc.Message);
                return;
            }

            string curMsg;
            if (newSharedFile.peersAddr.Count == 0)
            {
                curMsg = NOPEERSMSG;
            } else
            {
                curMsg = CONNTOPEERSMSG;
            }

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => FilesArea.Items[FilesArea.Items.IndexOf(newFile)].SubItems[2].Text =
                    curMsg));
            }
            else
            {
                FilesArea.Items[FilesArea.Items.IndexOf(newFile)].SubItems[2].Text =
                    curMsg;
            }

            await newSharedFile.ConnectToPeers().ConfigureAwait(false);
        }

        //torrent or newly added entry in filesList
        private async Task EstablishTrackerConnectionAsync(Torrent torrent, DownloadingFile sharedFile, ListViewItem listViewEntry)
        {
            // watch out for HTTPRequestException!, and WebException!
            var trackerResponse = new TrackerResponse();
            await trackerResponse.GetTrackerResponse(torrent, sharedFile, myPeerID, 25000).ConfigureAwait(false);

            var parser = new BencodeParser();

            foreach (var item in trackerResponse.response)
            {
                // try-catch here for right types!
                // WATCH OUT FOR CONFIGUREAWAIT AND THREAD SAFETY! NEED TO CONSIDER IT CAREFULLY
                switch (item.Key.ToString())
                {
                    case "failure reason":
                        // something went wrong; other keys may not be present
                        if (InvokeRequired)
                        {
                            Invoke(new MethodInvoker(() => FilesArea.Items[FilesArea.Items.IndexOf(listViewEntry)].GetSubItemAt(0, 0).Text =
                                INVALTRACKRESPMSG));
                        } else
                        {
                            FilesArea.Items[FilesArea.Items.IndexOf(listViewEntry)].GetSubItemAt(0, 0).Text =
                                INVALTRACKRESPMSG;
                        }
                        break;
                    case "interval":
                        sharedFile.trackerInterval = parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        break;
                    case "min interval":
                        sharedFile.trackerMinInterval = parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        break;
                    case "tracker id":
                        sharedFile.trackerID = parser.Parse<BString>(item.Value.EncodeAsBytes()).ToString();
                        break;
                    case "complete":
                        if (InvokeRequired)
                        {
                            Invoke(new MethodInvoker(() => FilesArea.Items[FilesArea.Items.IndexOf(listViewEntry)].SubItems[3].Text +=
                                parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value));
                        } else
                        {
                            FilesArea.Items[FilesArea.Items.IndexOf(listViewEntry)].SubItems[3].Text +=
                                parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        }
                        // number of seeders (peers with completed file). Only for UI purposes I guess...
                        break;
                    case "incomplete":
                        if (InvokeRequired)
                        {
                            Invoke(new MethodInvoker(() => FilesArea.Items[FilesArea.Items.IndexOf(listViewEntry)].SubItems[3].Text +=
                                "/" + parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value));
                        } else
                        {
                            FilesArea.Items[FilesArea.Items.IndexOf(listViewEntry)].SubItems[3].Text +=
                                "/" + parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        }
                        // number of leechers; purpose is the same
                        break;
                    case "peers":
                        // sometimes peers can be presented in binary model!
                        var peers = parser.Parse(item.Value.EncodeAsBytes());
                        if (peers is BString)
                        {
                            // ToArray seems to be a bit heavy...
                            byte[] binaryPeersList;
                            binaryPeersList = ((BString)peers).Value.ToArray();
                            if (binaryPeersList.Length % 6 != 0)
                            {
                                // not actually an invalid bencoding, but for simplicity
                                throw new InvalidBencodeException<BObject>();
                            }
                            else
                            {
                                byte[] oneEntry = new byte[6];
                                for (int i = 0; i < binaryPeersList.Length; i += 6)
                                {
                                    Array.Copy(binaryPeersList, i, oneEntry, 0, 6);
                                    sharedFile.peersAddr.AddLast(GetPeerFromBytes(oneEntry));
                                }
                            }
                        } else if (peers is BList)
                        {
                            foreach (var peerEntry in (BList)peers)
                            {
                                if (peerEntry is BDictionary)
                                {
                                    // again, exceptions..
                                    string IP = parser.Parse<BString>(((BDictionary)peerEntry)["ip"].EncodeAsBytes()).ToString();
                                    long port = parser.Parse<BNumber>(((BDictionary)peerEntry)["port"].EncodeAsBytes()).Value;
                                    sharedFile.peersAddr.AddLast(new IPEndPoint(IPAddress.Parse(IP), (int)port));
                                }
                            }
                        }
                        else
                        {
                            // not actually an invalid bencoding, but for simplicity
                            throw new InvalidBencodeException<BObject>();
                        }
                        break;
                }
            }
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

        private string GetAppropriateSizeForm(long size)
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
        }
    }
}
