using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using BencodeNET.Torrents;

namespace CourseWork
{
    public enum DownloadState { queued, downloading, stopped };

    // need a timer for tracker reconnections, choking-unchoking
    class DownloadingFile // need to save objects on disk somehow; later
    {
        public string infoFilePath { get; private set; }
        public string downloadPath { get; private set; }
        public DownloadState state { get; set; }
        // doesn't seem like I access piece from anywhere except for MessageHandler's thread
        public BitArray pieces { get; private set; }

        public long trackerInterval { get; set; }
        public long trackerMinInterval { get; set; }
        public string trackerID { get; set; }

        public LinkedList<IPEndPoint> peersAddr { get; private set; }
        public LinkedList<PeerConnection> connectedPeers { get; private set; }

        public long downloaded { get; private set; }
        public long totalSize { get; private set; }

        public Torrent torrentContents { get; }
        public FileWorker fileWorker { get; private set; }

        private MainForm ownerForm;

        public static MessageHandler messageHandler;

        // block size of 16384 is recommended and highly unlikely will change
        public DownloadingFile(MainForm ownerForm, Torrent torrent, string infoFilePath, string downloadPath)
        {
            this.ownerForm = ownerForm;

            pieces = new BitArray(torrent.NumberOfPieces);
            peersAddr = new LinkedList<IPEndPoint>();
            connectedPeers = new LinkedList<PeerConnection>();
            state = DownloadState.stopped;

            torrentContents = torrent;
            this.infoFilePath = infoFilePath;
            this.downloadPath = downloadPath;
            fileWorker = new FileWorker(torrentContents.PieceSize, downloadPath, torrentContents, 16384, SavedToDisk);
            totalSize = fileWorker.totalSize;
        }

        public void SerializeToFile(string path)
        {
            // implement later
        }

        // see what exceptions may be thrown and which of them I should catch in caller method;
        public async Task ConnectToPeers()
        {
            state = DownloadState.downloading;
            foreach (var peer in peersAddr)
            {
                var connection = new PeerConnection(peer, MessageRecieved, pieces.Count, torrentContents.OriginalInfoHashBytes);
                try
                {
                    if (await connection.PeerHandshake(torrentContents.OriginalInfoHashBytes, ownerForm.myPeerID) == 0)
                    {
                        lock (connectedPeers)
                        {
                            connectedPeers.AddLast(connection);
                        }
                        connection.StartPeerMessageLoop();
                        // TODO: DEBUG ONLY (only first working connection)
                        //break;
                    } else
                    {
                        connection.CloseConnection();
                    }
                }
                catch (SocketException ex)
                {
                    connection.CloseConnection();
                }
                // IO for some reason if host reset the connection :/
                catch (System.IO.IOException)
                {
                    connection.CloseConnection();
                }
            }
        }

        private void MessageRecieved(PeerMessage msg, PeerConnection connection)
        {
            // if msg == null, close the connection and remove it from the list
            if (msg == null)
            {
                connection.CloseConnection();
                connectedPeers.Remove(connection);
                // remove the connection from the list! Will it work like this?
                // It should be equal by reference too tho
            }
            else
            {
                messageHandler.AddTask(new Tuple<DownloadingFile, PeerMessage, PeerConnection>(this, msg, connection));
            }
        }

        // called from separate thread, synchronisation is needed
        private void SavedToDisk(int pieceIndex)
        {
            if (pieceIndex == torrentContents.NumberOfPieces - 1)
            {
                downloaded += fileWorker.lastPieceSize;
            }
            else
            {
                downloaded += torrentContents.PieceSize;
            }
            pieces[pieceIndex] = true;
        }
    }
}
