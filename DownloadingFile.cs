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

    // what if I get two reannounces at once? For example, the one from timer starts and then user hits "Stop"
    // so I create a new CancellationToken, and if old hasn't been cancelled and disposed yet, it just
    // gets lost. A dangling pointer. Perfect.
    // Or I hit Stop and then immidiately hit Start. I can happen that ConnectToPeersAsync hasn't seen
    // the cancellation, and Start creates a new token. So it continues to work with this new token as with
    // the old one. And old one is a dangling pointer. Fixed by a bit of busy-waiting
    // TODO: finally get all the needed stuff locked!!!
    public class DownloadingFile
    {
        /* Information about shared files */
        public string downloadPath;
        public DownloadState state { get; private set; }
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
        private const int MAXUNCHOKEDPEERSCOUNT = 4;
        //private byte outgoingRequestsCount;
        //private static byte maxOutgoingRequestsCount { get; set; } = 10;
        private LinkedList<PieceInfoNode> pendingIncomingPiecesInfo;

        // 30-seconds timer for unchoking
        private Timer unchokeTimer;
        /****/

        /* UI related information */
        private MainForm ownerForm;
        public int listViewEntryID { get; set; }
        /****/

        /* Handler for messages from connections */
        public static MessageHandler messageHandler;
        /**/

        /* Working with files related information*/
        private int blockSize;
        private string rootDir;
        private long lastPieceSize;
        public bool filesCorrupted { get; }
        /****/



        public delegate void TimeOutCallBack(PeerConnection connection);

        // block size of 16384 is recommended and highly unlikely will change
        public DownloadingFile(MainForm ownerForm, Torrent torrent, string downloadPath, bool restoring)
        {
            this.ownerForm = ownerForm;

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
            : this(ownerForm, torrent, downloadPath, true)
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

        public async Task Rehash()
        {
            BitArray result = await fileWorker.CalculateSHA1().ConfigureAwait(false);
            SetPiecesState(result);
        }

        public async Task StartAsync()
        {
            // TODO: check for file corruption, and rehash instead of starting if needed
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
                while (connectionsCancellationToken != null)
                {
                    // a bit of busy-waiting here. In worst case takes 3 seconds. Not so goog
                    // really, but helps to avoid race condition
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
            // TODO: interval reannounce here; executed in ThreadPool, sync
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
            await trackerResponse.GetTrackerResponse(this, ownerForm.myPeerID,
                25000, "started", 50).ConfigureAwait(false);

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
        }

        private async Task ConnectToPeersAsync()
        {
            // race condition when this here is trying to connect,
            // then called "Stop" and "Start", so "Start" changes this list,
            // and this method was in the middle of changing it too
            // so just create a local copy and work with it
            var localCopy = new LinkedList<IPEndPoint>(peersAddr);
            foreach (var peer in localCopy)
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
            localCopy.Clear();
            connectionsCancellationToken.Dispose();
            connectionsCancellationToken = null;
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
            // I guess I don't need exception-handling
            lock (connectedPeers)
            {
                connectedPeers.Remove(connection);
            }
            ReceivedChokeOrDisconnected(connection);
            ownerForm.PeerConnectedDisconnectedEvent(this, connectedPeers.Count);
        }

        public async Task StopAsync()
        {
            // removing all pending requests from both pendingPieces list and connections
            if (state == DownloadState.stopped)
            {
                return;
            }
            state = DownloadState.stopped;
            
            try
            {
                connectionsCancellationToken.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // all possible peers from peersAddr list are connected, so this token is already disposed
            }
            catch (NullReferenceException)
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
                messageHandler.AddTask(new CommandMessage(ControlMessageType.CloseConnection,
                    this, peer.Value));

                peer = nextPeer;
            }
            // (old)I don't clear the list of incoming pieces because maybe it'll be used later;
            // clear it when program is closing or when another download started(/old)
            // (new)clear it now to avoid race-conditions when saving download state to the file,
            // because if new pieces will arrive after connection closing they won't be found
            // in the list of pending incoming and saved(/new)
            ClearPendingList();
            await fileWorker.FlushAllAsync().ConfigureAwait(false);
            // I need somehow to wait until all the connections are closed, so I can be sure
            // that no pieces will arrive and be written to the files, because I'm gonna start
            // saving the downloaded pieces' state here
            // don't forget to call SaveSession

            ReannounceAsync("stopped", 0);
        }


        private LinkedListNode<IPEndPoint> GetPeerFromBytes(byte[] peer)
        {
            byte[] IPArr = new byte[4];
            Array.Copy(peer, IPArr, 4);
            int port = peer[4] * 256 + peer[5];
            return new LinkedListNode<IPEndPoint>(new IPEndPoint(new IPAddress(IPArr), port));
        }

        // called from separate thread, synchronization is needed

        public bool PeerHasInterestingPieces(PeerConnection connection)
        {
            // for now it just finds the first interesting piece the Peer can offer that I haven't downloaded yet;
            // later can be simply changed to whatever algorithm is needed
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
            // можно добавить флаг для того, чтобы различать: у пира нет кусочков из моего списка,
            // или просто все блоки уже запрошены и надо запросить новый кусочек, но не содержащийся в этом списке.
            // поможет потом при оптимизации циклов (в одном случае нужно просто найти у себя первый не загруженный,
            // в другом -- смотреть ещё, есть ли в списке запрошенных уже этот кусочек или нет)
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
            }
            // for now it just finds the first interesting piece the Peer can offer;
            // later can be simply changed to whatever algorithm is needed

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
            lock (pendingIncomingPiecesInfo)
            {
                pendingIncomingPiecesInfo.AddLast(newEntry);
            }
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
            lock (pendingIncomingPiecesInfo)
            {
                foreach (var entry in pendingIncomingPiecesInfo)
                {
                    if (entry.pieceIndex == pieceIndex)
                    {
                        return entry;
                    }
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
            // so it's async void, and I'm gonna try {...} catch {};
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

        // what if it throws (fileWorker)?
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

                // TODO: need to decrease it when pending list is cleared and
                // some downloaded blocks in buffers are lost
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

                        ownerForm.UpdateProgress(this);
                        
                        // Now we can send "HAVE" message
                        SendBroadcastHave(entry.pieceIndex);

                        if (downloaded == totalSize)
                        {
                            ReannounceAsync("completed", 0);
                        }
                    }
                    else
                    {
                        // if not, need to download the whole piece again
                        downloaded -= entry.pieceBuffer.Length;
                    }
                    lock (pendingIncomingPiecesInfo)
                    {
                        pendingIncomingPiecesInfo.Remove(entry);
                    }
                }
            }
            else
            {
                // something went wrong. I recieved a block I didn't asked for. May happen
                // if my "cancel" went for too long. Or if list is cleared because of "Stop"
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
            LinkedList<PeerConnection> localCopy;
            lock (connectedPeers)
            {
                localCopy = new LinkedList<PeerConnection>(connectedPeers);
            }

            foreach (var connection in localCopy)
            {
                var msg = new PeerMessage(pieceIndex);
                msg.targetFile = this;
                msg.targetConnection = connection;
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
