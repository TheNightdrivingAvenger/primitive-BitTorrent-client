﻿using System;
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

    class DownloadingFile // need to save objects on disk somehow; later
    {
        public string infoFilePath { get; private set; }
        public string downloadPath { get; private set; }
        public DownloadState state { get; set; }
        public BitArray pieces { get; private set; }
        public long trackerInterval { get; set; }
        public long trackerMinInterval { get; set; }
        public string trackerID { get; set; }
        public LinkedList<IPEndPoint> peersAddr { get; private set; }
        public LinkedList<PeerConnection> connectedPeers { get; private set; }
        public long downloaded { get; private set; }
        //public long left { get; private set; }
        public long totalSize { get; private set; }

        public Torrent torrentContents { get; }
        public FileWorker fileWorker { get; private set; }

        private MainForm ownerForm;

        public static MessageHandler messageHandler;

        // block size of 16384 is recommended and highly unlikely will change
        public DownloadingFile(Torrent torrent, string infoFilePath, string downloadPath)
        {
            pieces = new BitArray(torrent.NumberOfPieces);
            peersAddr = new LinkedList<IPEndPoint>();
            connectedPeers = new LinkedList<PeerConnection>();
            state = DownloadState.stopped;

            torrentContents = torrent;
            this.infoFilePath = infoFilePath;
            this.downloadPath = downloadPath;
            fileWorker = new FileWorker(torrentContents.PieceSize, downloadPath, torrentContents, 16384);
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
                var connection = new PeerConnection(peer, MessageRecieved, pieces.Count);
                try
                {
                    if (await connection.PeerHandshake(torrentContents.OriginalInfoHashBytes, ownerForm.myPeerID) == 0)
                    {
                        lock (connectedPeers)
                        {
                            connectedPeers.AddLast(connection);
                        }
                    } else
                    {
                        connection.CloseConnection();
                    }
                }
                catch (SocketException ex)
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
                // remove the connection from the list!
            }
            else
            {
                messageHandler.AddTask(new Tuple<DownloadingFile, PeerMessage, PeerConnection>(this, msg, connection));
            }
        }

        // called from separate thread, synchronisation is needed

    }
}
