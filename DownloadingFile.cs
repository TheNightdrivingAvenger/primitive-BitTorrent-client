﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Threading;

using BencodeNET.Parsing;
using BencodeNET.Torrents;
using BencodeNET.Objects;
using BencodeNET.Exceptions;
using System.Net.Http;

namespace CourseWork
{
    public enum DownloadState { completed, downloading, stopped, stopping, checking };

    public class DownloadingFile
    {
        /* Information about shared files */
        public DownloadState state { get; private set; }
        public BitArray pieces { get; private set; }

        public string downloadPath { get; }
        private long trackerInterval;
        private long trackerMinInterval;
        public string trackerID { get; private set; }

        private Timer trackerTimer;

        public long downloaded { get; private set; }
        public long totalSize { get; }
        public long uploaded { get; private set; }

        public Torrent torrentContents { get; }
        public FileWorker fileWorker { get; private set; }
        /****/
        /* Connections related information */
        private Semaphore connectionsSemaphore;
        private LinkedList<IPEndPoint> peersAddr;

        private LinkedList<PeerConnection> connectedPeers;

        private CancellationTokenSource connectionsCancellationToken;

        private const int MAXUNCHOKEDPEERSCOUNT = 4;

        private LinkedList<PieceInfoNode> pendingIncomingPiecesInfo;
        /****/

        /* UI related information */
        private MainForm ownerForm;
        public int listViewEntryID { get; set; }
        /****/

        /* Handler for messages from connections */
        public static MessageHandler messageHandler;

        /* Working with files related information*/
        private int blockSize;
        private long lastPieceSize;
        public bool filesCorrupted { get; private set; }
        /****/
        public string stringStatus { get; private set; }
        private bool reannouncing;

        public delegate void TimeOutCallBack(PeerConnection connection);

        // block size of 16384 is recommended and highly unlikely will change
        public DownloadingFile(MainForm ownerForm, Torrent torrent, string downloadPath, string subDir, bool restoring)
        {
            this.ownerForm = ownerForm;

            pieces = new BitArray(torrent.NumberOfPieces);
            peersAddr = new LinkedList<IPEndPoint>();
            connectedPeers = new LinkedList<PeerConnection>();
            state = DownloadState.stopped;
            stringStatus = MainForm.STOPPEDMSG;

            trackerID = null;
            trackerInterval = 0;
            trackerMinInterval = 0;
            downloaded = 0;
            uploaded = 0;

            torrentContents = torrent;
            if (subDir != null)
            {
                downloadPath += subDir + System.IO.Path.DirectorySeparatorChar;
            }
            this.downloadPath = downloadPath;
            fileWorker = new FileWorker(downloadPath, torrentContents, restoring);
            filesCorrupted = fileWorker.filesMissing;
            totalSize = torrentContents.TotalSize;

            lastPieceSize = (int)(torrent.NumberOfPieces * torrent.PieceSize - totalSize);
            lastPieceSize = (lastPieceSize == 0) ? torrent.PieceSize : torrent.PieceSize - lastPieceSize;

            connectionsCancellationToken = null;
            pendingIncomingPiecesInfo = new LinkedList<PieceInfoNode>();
            blockSize = 16384;
        }

        public DownloadingFile(MainForm ownerForm, Torrent torrent, string piecesState, string downloadPath) 
            : this(ownerForm, torrent, downloadPath, null, true)
        {
            if (piecesState.Length == pieces.Count)
            {
                SetPiecesState(piecesState);
            }
            else
            {
                filesCorrupted = true;
            }
        }

        private void SetPiecesState(string state)
        {
            downloaded = 0;
            for (int i = 0; i < state.Length; i++)
            {
                pieces.Set(i, state[i] == '1');
                if (pieces[i])
                {
                    downloaded += (i == state.Length - 1 ? lastPieceSize : torrentContents.PieceSize);
                }
            }
        }

        private void SetPiecesState(BitArray bitfield)
        {
            downloaded = 0;
            for (int i = 0; i < bitfield.Length; i++)
            {
                pieces.Set(i, bitfield[i]);
                if (pieces[i])
                {
                    downloaded += (i == bitfield.Length - 1 ? lastPieceSize : torrentContents.PieceSize);
                }
            }
        }

        public void SerializeToFile()
        {
            // without lock and clearing the pendingIncomingPieces list produces race-conditions
            // disadvantage: newly arrived pieces are discarded, and all the buffers are lost
            fileWorker.SaveSession(pieces);
        }

        public void AddToMainSession()
        {
            fileWorker.AddToMainSession();
        }

        public void CloseSession()
        {
            fileWorker.CloseSession();
        }

        public void RemoveEntry()
        {
            fileWorker.RemoveSession();
        }

        public void RemoveDownloadedFiles()
        {
            fileWorker.RemoveAllDownloads();
        }

        public async Task Rehash()
        {
            state = DownloadState.checking;
            ownerForm.UpdateStatus(this, MainForm.CHECKINGMSG);
            stringStatus = MainForm.CHECKINGMSG;
            BitArray result = await fileWorker.CalculateSHA1().ConfigureAwait(false);
            SetPiecesState(result);
            state = DownloadState.stopped;
            filesCorrupted = false;
            ownerForm.UpdateStatus(this, MainForm.STOPPEDMSG);
            stringStatus = MainForm.STOPPEDMSG;
            ownerForm.UpdateProgress(this);
        }

        public async Task StartAsync()
        {
            if (filesCorrupted)
            {
                return;
            }
            state = DownloadState.downloading;
            connectionsSemaphore = new Semaphore(1, 1);
            // acquiring it means that we don't want to start another download on this file until this stops
            connectionsSemaphore.WaitOne();
            connectionsCancellationToken = new CancellationTokenSource();
            ownerForm.UpdateStatus(this, MainForm.CONNTOTRACKERMSG);
            stringStatus = MainForm.CONNTOTRACKERMSG;
            int result = await ConnectToTrackerAsync(false).ConfigureAwait(false);
            if (result == 0)
            {
                ownerForm.UpdateStatus(this, MainForm.SEARCHINGPEERSMSG);
                stringStatus = MainForm.SEARCHINGPEERSMSG;
                if (trackerInterval >= 600)
                {
                    trackerTimer = new Timer(ReannounceTimerCallback, null, trackerInterval * 1000, trackerInterval * 1000);
                }
                else
                {
                    // if for some reason we didn't get the interval, 
                    // or if it's too small, set it to 40 minutes
                    trackerTimer = new Timer(ReannounceTimerCallback, null, 40 * 60 * 1000, 40 * 60 * 1000);
                }

                await ConnectToPeersAsync().ConfigureAwait(false);
            }
            else
            {
                connectionsSemaphore.Release();
                connectionsSemaphore.Dispose();
                connectionsSemaphore = null;
                connectionsCancellationToken.Dispose();
                state = DownloadState.stopped;
            }
            if (result == 1)
            {
                ownerForm.UpdateStatus(this, MainForm.NOTRACKER);
                stringStatus = MainForm.NOTRACKER;
            }
            else if (result == 2)
            {
                ownerForm.ShowError(MainForm.INVALTRACKRESPMSG);
                ownerForm.UpdateStatus(this, MainForm.STOPPEDMSG);
                stringStatus = MainForm.STOPPEDMSG;
            }
            else if (result == 3)
            {
                ownerForm.UpdateStatus(this, MainForm.NOPEERSMSG);
                stringStatus = MainForm.NOPEERSMSG;
            }
        }

        private async void ReannounceTimerCallback(object info)
        {
            if (reannouncing)
            {
                return;
            }
            reannouncing = true;
            // had to do with lock here, because it could be disposed while waiting
            lock (connectionsSemaphore)
            {
                if (connectionsSemaphore == null)
                {
                    reannouncing = false;
                    return;
                }
                connectionsSemaphore.WaitOne();
            }
            if (connectionsCancellationToken != null && !connectionsCancellationToken.IsCancellationRequested)
            {
                if (await ConnectToTrackerAsync(true).ConfigureAwait(false) == 0)
                {
                    if (trackerInterval >= 600)
                    {
                        trackerTimer.Change(trackerInterval * 1000, trackerInterval * 1000);
                    }
                    else
                    {
                        // if for some reason we didn't get the interval, 
                        // or if it's too small, set it to 40 minutes
                        trackerTimer.Change(40 * 60 * 1000, 40 * 60 * 1000);
                    }

                    await ConnectToPeersAsync().ConfigureAwait(false);
                }
            }
            else
            {
                connectionsSemaphore.Release();
            }
            reannouncing = false;
        }

        private async Task<int> ConnectToTrackerAsync(bool isReannounce)
        {
            try
            {
                await GetAndParseTrackerResponseAsync(isReannounce).ConfigureAwait(false);
            }
            catch (WebException)
            {
                return 1;
            }
            catch (HttpRequestException)
            {
                return 1;
            }
            catch (BencodeException)
            {
                return 2;
            }
            if (peersAddr.Count == 0)
            {
                return 3;
            }
            else
            {
                return 0;
            }
        }

        private async Task GetAndParseTrackerResponseAsync(bool isReannounce)
        {
            var trackerResponse = new TrackerResponse();
            string trackerEvent = null;
            if (!isReannounce)
            {
                trackerEvent = "started";
            }
            await trackerResponse.GetTrackerResponse(this, ownerForm.myPeerID,
                25000, trackerEvent, 50).ConfigureAwait(false);

            var parser = new BencodeParser();
            int complete = 0, incomplete = 0;
            foreach (var item in trackerResponse.response)
            {
                switch (item.Key.ToString())
                {
                    case "failure reason":
                        // something went wrong
                        if (!isReannounce)
                        {
                            ownerForm.ShowError(MainForm.TRACKERERRORMSG + item.Value.ToString());
                        }
                        return;
                    case "interval":
                        trackerInterval = parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        break;
                    case "min interval":
                        trackerMinInterval = parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        break;
                    case "tracker id":
                        trackerID = parser.Parse<BString>(item.Value.EncodeAsBytes()).ToString();
                        break;
                    case "complete":
                        complete = (int)parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        // number of seeders (peers with completed file). Only for UI purposes I guess...
                        break;
                    case "incomplete":
                        incomplete = (int)parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        // number of leechers; purpose is the same
                        break;
                    case "peers":
                        peersAddr.Clear();
                        var peers = parser.Parse(item.Value.EncodeAsBytes());
                        if (peers is BString)
                        {
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
                                    peersAddr.AddLast(GetPeerFromBytes(oneEntry));
                                }
                            }
                        }
                        else if (peers is BList)
                        {
                            foreach (var peerEntry in (BList)peers)
                            {
                                if (peerEntry is BDictionary)
                                {
                                    string IP = parser.Parse<BString>(((BDictionary)peerEntry)["ip"].EncodeAsBytes()).ToString();
                                    long port = parser.Parse<BNumber>(((BDictionary)peerEntry)["port"].EncodeAsBytes()).Value;
                                    peersAddr.AddLast(new IPEndPoint(IPAddress.Parse(IP), (int)port));
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
            ownerForm.UpdateSeedersLeechersNum(this, complete, incomplete);
        }

        private async Task ConnectToPeersAsync()
        {
            foreach (var peer in peersAddr)
            {
                if (connectionsCancellationToken.IsCancellationRequested)
                {
                    break;
                }
                var connection = new PeerConnection(peer, MessageRecieved, pieces.Count, torrentContents.OriginalInfoHashBytes,
                    ConnectionTimeOut);
                try
                {
                    if (await connection.PeerHandshakeAsync(torrentContents.OriginalInfoHashBytes, ownerForm.myPeerID,
                        connectionsCancellationToken).ConfigureAwait(false) == 0)
                    {
                        lock (connectedPeers)
                        {
                            connectedPeers.AddLast(connection);
                        }
                        if (downloaded == 0)
                        {
                            // nothing to send, so count it as sent
                            connection.bitfieldSent = true;
                        }
                        else
                        {
                            PeerMessage message;
                            lock (pieces)
                            {
                                // copy here for no race-conditions
                                // peer will receive either a complete BitField or a subsequent "have"(-s)
                                message = new PeerMessage(new BitArray(pieces));
                            }
                            message.targetConnection = connection;
                            message.targetFile = this;
                            messageHandler.AddTask(new CommandMessage(ControlMessageType.SendInner, message));

                        }
                        ownerForm.PeerConnectedDisconnectedEvent(this, connectedPeers.Count);
                        connection.StartPeerMessageLoop();
                    }
                    else
                    {
                        connection.CloseConnection();
                    }
                }
                catch
                {
                    connection.CloseConnection();
                }
            }
            connectionsSemaphore.Release();
        }

        private void ConnectionTimeOut(PeerConnection connection)
        {
            if (!messageHandler.isStopped)
            {
                var msg = new PeerMessage(PeerMessageType.keepAlive);
                msg.targetConnection = connection;
                msg.targetFile = this;
                messageHandler.AddTask(new CommandMessage(ControlMessageType.SendInner, msg));
            }
        }

        private void MessageRecieved(PeerMessage msg, PeerConnection connection)
        {
            // if msg == null, close the connection and remove it from the list
            if (msg == null)
            {
                // message handler's loop stops only when the program is closing
                // this means calling "Stop" on DownloadingFile, that means
                // sending "CloseConnection" message to the Pump for EVERY connection
                // so if it's stopped, the connection is waiting to be closed, and no more
                // messages can be received
                if (!messageHandler.isStopped)
                {
                    messageHandler.AddTask(new CommandMessage(ControlMessageType.CloseConnection,
                        this, connection));
                }
            }
            else
            {
                if (!messageHandler.isStopped)
                {
                    msg.targetConnection = connection;
                    msg.targetFile = this;
                    messageHandler.AddTask(msg);
                }
            }
        }

        public void RemoveConnection(PeerConnection connection)
        {
            lock (connectedPeers)
            {
                connectedPeers.Remove(connection);
            }
            ReceivedChokeOrDisconnected(connection);
            ownerForm.PeerConnectedDisconnectedEvent(this, connectedPeers.Count);
        }

        public async Task StopAsync()
        {
            if (state == DownloadState.stopped)
            {
                return;
            }
            state = DownloadState.stopping;
            ownerForm.UpdateStatus(this, MainForm.STOPPINGMSG);
            stringStatus = MainForm.STOPPINGMSG;
            connectionsCancellationToken.Cancel();
            
            connectionsSemaphore.WaitOne();
            // no locking because while I go through all connected peers I post messages
            // for closing connections; closing means removing from this list,
            // that means acquiring lock. So MessageHandler's Message Pump would be stopped until
            // I sorted out all the connections and released the lock
            List<PeerConnection> localCopy;
            lock (connectedPeers)
            {
                localCopy = new List<PeerConnection>(connectedPeers);
            }
            if (localCopy.Count != 0)
            {
                for (int i = 0; i < localCopy.Count; i++)
                {
                    messageHandler.AddTask(new CommandMessage(ControlMessageType.CloseConnection,
                        this, localCopy[i]));
                }
            }
            localCopy.Clear();
            // clear it now to avoid race-conditions when saving download state to the file,
            // because if new pieces will arrive after connection closing they won't be found
            // in the list of pending incoming and saved
            ClearPendingList();
            trackerTimer.Dispose();
            await fileWorker.FlushAllAsync().ConfigureAwait(false);

            ReannounceAsync("stopped", 0);

            connectionsCancellationToken.Dispose();
            connectionsCancellationToken = null;

            connectionsSemaphore.Release();
            // lock because I'm gonna dispose the semaphore, so other threads
            // won't get stuck on WaitOne() if I dispose it while they're blocked
            lock (connectionsSemaphore)
            {
                connectionsSemaphore.Dispose();
                connectionsSemaphore = null;
            }
            state = DownloadState.stopped;
            ownerForm.UpdateStatus(this, MainForm.STOPPEDMSG);
            stringStatus = MainForm.STOPPEDMSG;
            ownerForm.UpdateProgress(this);
        }


        private LinkedListNode<IPEndPoint> GetPeerFromBytes(byte[] peer)
        {
            byte[] IPArr = new byte[4];
            Array.Copy(peer, IPArr, 4);
            int port = peer[4] * 256 + peer[5];
            return new LinkedListNode<IPEndPoint>(new IPEndPoint(new IPAddress(IPArr), port));
        }


        public bool PeerHasInterestingPieces(PeerConnection connection)
        {
            lock (pieces)
            {
                for (int i = 0; i < connection.peersPieces.Count; i++)
                {
                    if ((pieces[i] == false) && (connection.peersPieces[i] == true))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // returns piece index, piece offset, block size
        public Tuple<int, int, int> FindNextRequest(PeerConnection connection)
        {
            Tuple<int, int, int> result;
            // here we search for any non-downloaded and non-requested blocks within pending pieces
            lock (pendingIncomingPiecesInfo)
            {
                foreach (var entry in pendingIncomingPiecesInfo)
                {
                    // if peer doesn't have this piece, go further
                    if (connection.peersPieces[entry.pieceIndex] == false)
                    {
                        continue;
                    }

                    // otherwise check if we have something left to download in this piece
                    int blockIndex;
                    bool spaceFound = false;
                    for (blockIndex = 0; blockIndex < entry.blocksMap.Count; blockIndex++)
                    {
                        if (entry.blocksMap[blockIndex] == false && entry.requestedBlocksMap[blockIndex] == false)
                        {
                            spaceFound = true;
                            break;
                        }
                    }

                    if (spaceFound)
                    {
                        int resultOffset = blockIndex * blockSize;
                        if (blockIndex == entry.blocksMap.Count - 1)
                        {
                            result = new Tuple<int, int, int>(entry.pieceIndex, resultOffset, GetLastBlockSize(entry.pieceIndex));
                        }
                        else
                        {
                            result = new Tuple<int, int, int>(entry.pieceIndex, resultOffset, blockSize);
                        }
                        entry.requestedBlocksMap[blockIndex] = true;
                        return result;
                    }
                }

                // if we didn't find anything in pieces we're already downloading (or the Peer doesn't have it),
                // we simply find the first fitting piece the Peer has
                int interestingPieceIndex = -1;
                lock (pieces)
                {
                    for (int i = 0; i < connection.peersPieces.Count; i++)
                    {
                        if (FindRequestsPiece(i) == null && pieces[i] == false && (connection.peersPieces[i] == true))
                        {
                            interestingPieceIndex = i;
                            break;
                        }
                    }
                }
                // still haven't found anything? Well, this Peer has nothing interesting
                if (interestingPieceIndex == -1)
                {
                    return null;
                }

                // peer has some piece we haven't added yet; so we add it and request block with 0-offset (the first one)
                var newEntry = AddPendingIncomingPiece(interestingPieceIndex);
                newEntry.requestedBlocksMap[0] = true;
                pendingIncomingPiecesInfo.AddLast(newEntry);
                // just for safety (what if it's only one block in piece?)
                if (0 == newEntry.blocksMap.Count - 1)
                {
                    return new Tuple<int, int, int>(interestingPieceIndex, 0, GetLastBlockSize(newEntry.pieceIndex));
                }
                else
                {
                    return new Tuple<int, int, int>(interestingPieceIndex, 0, blockSize);
                }
            }
        }

        private PieceInfoNode FindRequestsPiece(int pieceIndex)
        {
            foreach (var entry in pendingIncomingPiecesInfo)
            {
                if (entry.pieceIndex == pieceIndex)
                {
                    return entry;
                }
            }
            return null;
        }

        private int GetLastBlockSize(int index)
        {
            int totalBlocks;
            if (index == torrentContents.NumberOfPieces - 1)
            {
                // how many blocks are in the last piece?
                totalBlocks = (int)Math.Ceiling((double)lastPieceSize / blockSize);
                int size = (int)(totalBlocks * blockSize - lastPieceSize);
                return size == 0 ? blockSize : blockSize - size;
            }
            else
            {
                // how many blocks are in a regular piece?
                totalBlocks = (int)Math.Ceiling((double)torrentContents.PieceSize / blockSize);
                int size = (int)(totalBlocks * blockSize - torrentContents.PieceSize);
                return size == 0 ? blockSize : blockSize - size;
            }
        }

        private async void ReannounceAsync(string message, int numWant)
        {
            // I don't care if this request didn't work out.
            // I'm only interested in getting (if any) new reannounce interval
            // so it's async void
            try
            {
                await GetAndParseReannounceResponseAsync(message, numWant);
            }
            catch{}
        }

        private async Task GetAndParseReannounceResponseAsync(string message, int numWant)
        {
            var trackerResponse = new TrackerResponse();
            await trackerResponse.GetTrackerResponse(this, ownerForm.myPeerID, 25000,
                message, numWant).ConfigureAwait(false);

            var parser = new BencodeParser();
            foreach (var item in trackerResponse.response)
            {
                switch (item.Key.ToString())
                {
                    case "interval":
                        trackerInterval = parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        if (trackerInterval >= 600)
                        {
                            trackerTimer.Change(trackerInterval * 1000, trackerInterval * 1000);
                        }
                        else
                        {
                            // if for some reason we didn't get the interval, 
                            // or if it's too small, set it to 40 minutes
                            trackerTimer.Change(40 * 60 * 1000, 40 * 60 * 1000);
                        }
                        break;
                    case "min interval":
                        trackerMinInterval = parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
                        break;
                    case "tracker id":
                        trackerID = parser.Parse<BString>(item.Value.EncodeAsBytes()).ToString();
                        break;
                }
            }
        }

        public void AddBlock(int pieceIndex, int offset, byte[] block)
        {
            bool update = false;
            lock (pendingIncomingPiecesInfo)
            {
                var entry = FindRequestsPiece(pieceIndex);

                if (entry != null && entry.blocksMap[offset / blockSize] == false)
                {
                    // check for safety. If received a block that is larger than we could possibly ask for,
                    // then consider it as ill-formed and drop it
                    if (block.Length > 16384)
                    {
                        entry.requestedBlocksMap[offset / blockSize] = false;
                        return;
                    }

                    // block.length because the last block may be smaller than others
                    Array.Copy(block, 0, entry.pieceBuffer, offset, block.Length);
                    entry.blocksMap[offset / blockSize] = true;
                    entry.bufferSize += block.Length;

                    downloaded += block.Length;

                    // we accumulated the whole piece?
                    if (entry.bufferSize == entry.pieceBuffer.Length)
                    {
                        // check SHA1 and save to disk if OK
                        if (fileWorker.SaveToDisk(entry))
                        {
                            lock (pieces)
                            {
                                pieces[pieceIndex] = true;
                            }

                            update = true;

                            // Now we can send "HAVE" message
                            SendBroadcastHave(entry.pieceIndex);

                            if (downloaded == totalSize)
                            {
                                state = DownloadState.completed;
                                ownerForm.UpdateStatus(this, MainForm.DOWNLOADINGMSG);
                                stringStatus = MainForm.DOWNLOADINGMSG;
                                ReannounceAsync("completed", 0);
                            }
                        }
                        else
                        {
                            // if not, need to download the whole piece again
                            downloaded -= entry.pieceBuffer.Length;
                        }
                        pendingIncomingPiecesInfo.Remove(entry);
                    }
                }
                else
                {
                    // something went wrong. I recieved a block I didn't asked for. May happen
                    // if my "cancel" went for too long. Or if list is cleared because of "Stop"
                }
            }
            if (update)
            {
                ownerForm.UpdateProgress(this);
            }
        }

        private void ClearPendingList()
        {
            lock (pendingIncomingPiecesInfo)
            {
                var entry = pendingIncomingPiecesInfo.First;
                while (entry != null)
                {
                    var nextEntry = entry.Next;
                    downloaded -= entry.Value.bufferSize;
                    pendingIncomingPiecesInfo.Remove(entry);
                    entry = nextEntry;
                }
            }
        }

        private PieceInfoNode AddPendingIncomingPiece(int index)
        {
            long actualPieceSize;
            if (index == torrentContents.NumberOfPieces - 1)
            {
                actualPieceSize = lastPieceSize;
            }
            else
            {
                actualPieceSize = torrentContents.PieceSize;
            }

            return new PieceInfoNode(index, new byte[actualPieceSize],
                new BitArray((int)Math.Ceiling((double)actualPieceSize / blockSize)));
        }

        private void SendBroadcastHave(int pieceIndex)
        {
            // copy the list here for not locking it for too long
            List<PeerConnection> localCopy;
            lock (connectedPeers)
            {
                localCopy = new List<PeerConnection>(connectedPeers);
            }

            for (int i = 0; i < localCopy.Count; i++)
            {
                var msg = new PeerMessage(pieceIndex);
                msg.targetFile = this;
                msg.targetConnection = localCopy[i];
                messageHandler.AddTask(new CommandMessage(ControlMessageType.SendInner, msg));
            }
            localCopy.Clear();
        }

        public void ReceivedChokeOrDisconnected(PeerConnection connection)
        {
            if (connection.outgoingRequestsCount != 0)
            {
                for (int i = 0; i < connection.outgoingRequests.Length; i++)
                {
                    if (connection.outgoingRequests[i] != null)
                    {
                        // null checking because the list may be already empty
                        lock (pendingIncomingPiecesInfo)
                        {
                            var node = FindRequestsPiece(connection.outgoingRequests[i].Item1);
                            if (node != null)
                            {
                                node.requestedBlocksMap[connection.outgoingRequests[i].Item2 / blockSize] = false;
                            }
                            connection.outgoingRequests[i] = null;
                        }
                    }
                }
            }
        }
    }
}
