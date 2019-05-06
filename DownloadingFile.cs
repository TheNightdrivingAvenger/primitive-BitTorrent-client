using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using BencodeNET.Parsing;
using BencodeNET.Torrents;
using BencodeNET.Objects;
using BencodeNET.Exceptions;
using System.Net.Http;

namespace CourseWork
{
    public enum DownloadState { queued, downloading, stopped };

    // TODO: finally get all the needed stuff locked!!!
    public class DownloadingFile
    {
        /* Information about shared files */
        private string infoFilePath;
        public string downloadPath;
        public DownloadState state { get; private set; }
        // doesn't seem like I access piece from anywhere except for MessageHandler's thread
        public BitArray pieces { get; private set; }

        private long trackerInterval;
        private long trackerMinInterval;
        public string trackerID { get; private set; }

        private Timer trackerTimer;

        // may be accessed from several threads?
        public long downloaded { get; private set; }
        /**/
        public long totalSize { get; }
        public long uploaded { get; private set; }

        public Torrent torrentContents { get; }
        public FileWorker fileWorker { get; private set; }
        /****/

        /* Connections related information */
        private LinkedList<IPEndPoint> peersAddr;

        // may be accessed from several threads!
        private LinkedList<PeerConnection> connectedPeers;
        /**/

        private CancellationTokenSource connectionsCancellationToken;

        // may be accessed from several threads?
        private int unchokedPeersCount;

        //private byte outgoingRequestsCount;
        //private static byte maxOutgoingRequestsCount { get; set; } = 10;
        // TODO: maybe LinkedList is not that good, because I traverse it really-really often
        // (and I also traverse bitfields inside its elements)
        private LinkedList<PieceInfoNode> pendingIncomingPiecesInfo;
        /****/

        /* UI related information */
        private MainForm ownerForm;
        public int listViewEntryID { get; private set; }
        /****/

        /* Handler for messages from connections */
        public static MessageHandler messageHandler;
        /**/

        /* Working with files related information*/
        //private long pieceOffset;
        private int blockSize;
        private string rootDir;
        private long lastPieceSize;
        /****/

        public delegate void TimeOutCallBack(PeerConnection connection);

        // block size of 16384 is recommended and highly unlikely will change
        public DownloadingFile(MainForm ownerForm, int entryID, Torrent torrent, string downloadPath)
        {
            this.ownerForm = ownerForm;
            this.listViewEntryID = entryID;

            pieces = new BitArray(torrent.NumberOfPieces);
            peersAddr = new LinkedList<IPEndPoint>();
            connectedPeers = new LinkedList<PeerConnection>();
            state = DownloadState.stopped;

            trackerID = null;
            trackerInterval = 0;
            trackerMinInterval = 0;
            downloaded = 0;
            uploaded = 0;

            unchokedPeersCount = 0;

            torrentContents = torrent;
            this.infoFilePath = downloadPath + FileWorker.ClearPath(torrent.DisplayName) + "VST.session";
            this.downloadPath = downloadPath;
            fileWorker = new FileWorker(torrentContents.PieceSize, downloadPath, infoFilePath, torrentContents);
            totalSize = torrentContents.TotalSize;

            lastPieceSize = (int)(torrent.NumberOfPieces * torrent.PieceSize - totalSize);
            lastPieceSize = (lastPieceSize == 0) ? torrent.PieceSize : torrent.PieceSize - lastPieceSize;

            pendingIncomingPiecesInfo = new LinkedList<PieceInfoNode>();
            //outgoingRequestsCount = 0;
            blockSize = 16384;
        }

        public void SerializeToFile(string path)
        {
            // TODO: saving objects to file
        }

        public async Task StartAsync()
        {
            state = DownloadState.downloading;

            int result = await FirstConnectToTrackerAsync().ConfigureAwait(false);

            if (result == 0)
            {
                if (trackerInterval != 0)
                {
                    trackerTimer = new Timer(ReannounceTimerCallback, null, trackerInterval * 1000, trackerInterval * 1000);
                }
                else
                {
                    // if for some reason we didn't get the interval, set it to 50 minutes
                    trackerTimer = new Timer(ReannounceTimerCallback, null, 50 * 60 * 1000, 50 * 60 * 1000);
                }
                connectionsCancellationToken = new CancellationTokenSource();
                await ConnectToPeersAsync().ConfigureAwait(false);
            }
            else
            {
                state = DownloadState.stopped;
            }
        }

        private void ReannounceTimerCallback(object info)
        {
            // TODO: interval reannounce here
        }

        private async Task<int> FirstConnectToTrackerAsync()
        {
            try
            {
                await GetAndParseTrackerResponseAsync().ConfigureAwait(false);
            }
            catch (WebException)
            {
                ownerForm.UpdateStatus(this, MainForm.NOTRACKER);
                return 1;
            }
            catch (HttpRequestException)
            {
                ownerForm.UpdateStatus(this, MainForm.NOTRACKER);
                return 1;
            }
            catch (InvalidBencodeException<BObject>)
            {
                ownerForm.ShowError(MainForm.INVALTRACKRESPMSG);
                return 1;
            }

            if (peersAddr.Count == 0)
            {
                ownerForm.UpdateStatus(this, MainForm.NOPEERSMSG);
            }
            else
            {
                ownerForm.UpdateStatus(this, MainForm.SEARCHINGPEERSMSG);
            }
            return 0;
        }

        private async Task GetAndParseTrackerResponseAsync()
        {
            var trackerResponse = new TrackerResponse();
            await trackerResponse.GetTrackerResponse(torrentContents, downloaded, totalSize,
                ownerForm.myPeerID, 25000, "started", trackerID).ConfigureAwait(false);

            var parser = new BencodeParser();
            foreach (var item in trackerResponse.response)
            {
                switch (item.Key.ToString())
                {
                    case "failure reason":
                        // something went wrong; other keys may not be present
                        ownerForm.ShowError(MainForm.TRACKERERRORMSG + item.Value.EncodeAsString());
                        break;
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
                        ownerForm.UpdateSeedersNum(this, item.Value.EncodeAsString());
                        // number of seeders (peers with completed file). Only for UI purposes I guess...
                        break;
                    case "incomplete":
                        ownerForm.UpdateLeechersNum(this, item.Value.EncodeAsString());
                        // number of leechers; purpose is the same
                        break;
                    case "peers":
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
            connectionsCancellationToken.Dispose();
        }

        private void ConnectionTimeOut(PeerConnection connection)
        {
            messageHandler.AddTask(new CommandMessage(ControlMessageType.SendKeepAlive, this, connection, -1, -1, -1));
        }

        private void MessageRecieved(PeerMessage msg, PeerConnection connection)
        {
            // if msg == null, close the connection and remove it from the list
            if (msg == null)
            {
                messageHandler.AddTask(new CommandMessage(ControlMessageType.CloseConnection, this, connection, -1, -1, -1));
            }
            else
            {
                msg.targetConnection = connection;
                msg.targetFile = this;
                messageHandler.AddTask(msg);
            }
        }

        public void RemoveConnection(PeerConnection connection)
        {
            // I guess I don't need exception-handling
            lock (connectedPeers)
            {
                connectedPeers.Remove(connection);
            }
            ReceivedChokeOrDisconnected(connection);
            ownerForm.PeerConnectedDisconnectedEvent(this, connectedPeers.Count);
        }

        public async Task Stop()
        {
            // removing all pending requests from both pendingPieces list and connections
            // if needed, send "cancel" to peer
            state = DownloadState.stopped;

            try
            {
                connectionsCancellationToken.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // all possible peers from peersAddr list are connected, so this token is already disposed
            }

            var peer = connectedPeers.First;
            // no locking because while I go through all connected peers I post messages
            // for closing connections; closing means removing from this list,
            // that means acquiring lock. So MessageHandler's Message Pump would be stopped until
            // I sorted out all the connections and released the lock
            // and by this point nothing new can be added to this list, so it's safe
            while (peer != null)
            {
                var nextPeer = peer.Next;
                messageHandler.AddTask(new CommandMessage(ControlMessageType.CloseConnection, this, peer.Value, -1, -1, -1));
                peer = nextPeer;
            }
            // I don't clear the list of incoming pieces because maybe it'll be used later;
            // TODO: clear it when program is closing or when another download started
            connectedPeers.Clear();
            await fileWorker.FlushAllAsync();
            ReannounceAsync("stopped");
        }


        private LinkedListNode<IPEndPoint> GetPeerFromBytes(byte[] peer)
        {
            byte[] IPArr = new byte[4];
            Array.Copy(peer, IPArr, 4);
            int port = peer[4] * 256 + peer[5];
            return new LinkedListNode<IPEndPoint>(new IPEndPoint(new IPAddress(IPArr), port));
        }

        public void ProgramClosing()
        {
            // TODO: implement graceful closing (call stop I guess)
        }

        // called from separate thread, synchronization is needed

        public bool PeerHasInterestingPieces(PeerConnection connection)
        {
            // for now it just finds the first interesting piece the Peer can offer that I haven't downloaded yet;
            // later can be simply changed to whatever algorithm is needed
            for (int i = 0; i < connection.peersPieces.Count; i++)
            {
                if ((pieces[i] == false) && (connection.peersPieces[i] == true))
                {
                    return true;
                }
            }
            return false;
        }

        // returns piece index, piece offset, block size
        public Tuple<int, int, int> FindNextRequest(PeerConnection connection)
        {
            Tuple<int, int, int> result;
            // можно добавить флаг для того, чтобы различать: у пира нет кусочков из моего списка,
            // или просто все блоки уже запрошены и надо запросить новый кусочек, но не содержащийся в этом списке.
            // поможет потом при оптимизации циклов (в одном случае нужно просто найти у себя первый не загруженный,
            // в другом -- смотреть ещё, есть ли в списке запрошенных уже этот кусочек или нет)
            // here we search for any non-downloaded and non-requested blocks within pending pieces
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

            // for now it just finds the first interesting piece the Peer can offer;
            // later can be simply changed to whatever algorithm is needed

            // if we didn't find anything in pieces we're already downloading (or the Peer doesn't have it),
            // we simply find the first fitting piece the Peer has
            int interestingPieceIndex = -1;
            for (int i = 0; i < connection.peersPieces.Count; i++)
            {
                if (FindRequestsPiece(i) == null && pieces[i] == false && (connection.peersPieces[i] == true))
                {
                    interestingPieceIndex = i;
                    break;
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

        private async void ReannounceAsync(string message)
        {
            // I don't care if this request didn't work out.
            // I'm only interested in getting (if any) new reannounce interval
            // so it's async void, and I'm gonna try {...} catch {};
            try
            {
                await GetAndParseReannounceResponseAsync(message);
            }
            catch{}
        }

        private async Task GetAndParseReannounceResponseAsync(string message)
        {
            var trackerResponse = new TrackerResponse();
            await trackerResponse.GetTrackerResponse(torrentContents, downloaded, totalSize,
                ownerForm.myPeerID, 25000, message, trackerID).ConfigureAwait(false);

            var parser = new BencodeParser();
            foreach (var item in trackerResponse.response)
            {
                switch (item.Key.ToString())
                {
                    case "interval":
                        // reset timer?
                        trackerInterval = parser.Parse<BNumber>(item.Value.EncodeAsBytes()).Value;
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
                        // remove from pending incoming requests
                        pendingIncomingPiecesInfo.Remove(entry);
                        pieces[pieceIndex] = true;

                        ownerForm.UpdateProgress(this);
                        
                        // Now we can send "HAVE" message
                        SendBroadcastHave(entry.pieceIndex);

                        if (downloaded == totalSize)
                        {
                            // TODO: send the completed event to the tracker
                            ReannounceAsync("completed");
                        }
                    }
                    else
                    {
                        // if not, need to download the whole piece again
                        pendingIncomingPiecesInfo.Remove(entry);
                        downloaded -= entry.pieceBuffer.Length;
                    }
                }
            }
            else
            {
                // something went wrong. I recieved a block I didn't asked for. May happen
                // if my "cancel" went for too long. What should I do?
                // Create a new entry for it, or just discard it?
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
            var msg = new PeerMessage(pieceIndex);
            // TODO: maybe, I lock them for too long? Maybe copy, then work?
            lock (connectedPeers)
            {
                foreach (var connection in connectedPeers)
                {
                    try
                    {
                        connection.SendPeerMessage(msg);
                    }
                    catch
                    {
                        messageHandler.AddTask(new CommandMessage(ControlMessageType.CloseConnection, this, connection, -1, -1, -1));
                    }
                }
            }
        }

        public void ReceivedChokeOrDisconnected(PeerConnection connection)
        {
            if (connection.outgoingRequestsCount != 0)
            {
                for (int i = 0; i < connection.outgoingRequests.Length; i++)
                {
                    if (connection.outgoingRequests[i] != null)
                    {
                        // no null-checking because it can't be null. Or can it?
                        FindRequestsPiece(connection.outgoingRequests[i].Item1).requestedBlocksMap[connection.outgoingRequests[i].Item2 /
                            blockSize] = false;
                        connection.outgoingRequests[i] = null;
                    }
                }
            }
        }
    }
}
