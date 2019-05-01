using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using BencodeNET.Torrents;
using System.Windows.Forms;

namespace CourseWork
{
    public enum DownloadState { queued, downloading, stopped };

    // TODO: need a timer for tracker reconnections, choking-unchoking
    // TODO: finally get all the needed stuff locked!!!
    public class DownloadingFile
    {
        /* Information about shared files */
        public string infoFilePath { get; private set; }
        public string downloadPath { get; private set; }
        public DownloadState state { get; set; }
        // doesn't seem like I access piece from anywhere except for MessageHandler's thread
        public BitArray pieces { get; private set; }

        public long trackerInterval { get; set; }
        public long trackerMinInterval { get; set; }
        public string trackerID { get; set; }

        // may be accessed from several threads?
        public long downloaded { get; private set; }
        /**/
        public long totalSize { get; private set; }

        public Torrent torrentContents { get; }
        public FileWorker fileWorker { get; private set; }
        /****/

        /* Information about connections */
        public LinkedList<IPEndPoint> peersAddr { get; private set; }
        
        // may be accessed from several threads!
        public LinkedList<PeerConnection> connectedPeers { get; private set; }
        /**/

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
        public ListViewItem listViewEntry { get; private set; }
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

        // block size of 16384 is recommended and highly unlikely will change
        public DownloadingFile(MainForm ownerForm, ListViewItem entry, Torrent torrent, string infoFilePath, string downloadPath)
        {
            this.ownerForm = ownerForm;
            this.listViewEntry = entry;

            pieces = new BitArray(torrent.NumberOfPieces);
            peersAddr = new LinkedList<IPEndPoint>();
            connectedPeers = new LinkedList<PeerConnection>();
            state = DownloadState.stopped;

            unchokedPeersCount = 0;

            torrentContents = torrent;
            this.infoFilePath = infoFilePath;
            this.downloadPath = downloadPath;
            fileWorker = new FileWorker(torrentContents.PieceSize, downloadPath, torrentContents);
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
                        ownerForm.PeerConnectedDisconnectedEvent(this, connectedPeers.Count);
                        connection.StartPeerMessageLoop();
                    }
                    else
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
                lock (connectedPeers)
                {
                    connectedPeers.Remove(connection);
                }
                ReceivedChokeOrDisconnected(connection);
                ownerForm.PeerConnectedDisconnectedEvent(this, connectedPeers.Count);
                // remove the connection from the list! Will it work like this?
                // It should be equal by reference too tho, so should work
            }
            else
            {
                msg.targetConnection = connection;
                msg.targetFile = this;
                messageHandler.AddTask(msg);
            }
        }

        public void Stop()
        {
            // removing all pending requests from both pendingPieces list and connections
            // if needed, send "cancel" to peer
            state = DownloadState.stopped;
            foreach (var peer in connectedPeers)
            {
                for (int i = 0; i < peer.outgoingRequests.Length; i++)
                {
                    if (peer.outgoingRequests[i] != null)
                    {
                        // no null-checking because it can't be null
                        PieceInfoNode node;
                        node = FindRequestsPiece(peer.outgoingRequests[i].Item1);
                        node.requestedBlocksMap[peer.outgoingRequests[i].Item2 / blockSize] = false;

                        var msg = new CommandMessage(ControlMessageType.SendCancel, node.pieceIndex,
                            peer.outgoingRequests[i].Item2 / blockSize);
                        msg.targetConnection = peer;
                        msg.targetFile = this;

                        messageHandler.AddTask(msg);

                        peer.outgoingRequests[i] = null;
                    }
                }
            }
        }

        public void ProgramClosing()
        {
            // TODO: implement graceful closing
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
            // too many requests can be bad, so cap 'em (now it's controlled in connections)
            /*if (outgoingRequestsCount >= maxOutgoingRequestsCount)
            {
                return null;
            }*/

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
                    //outgoingRequestsCount++;
                    entry.requestedBlocksMap[blockIndex] = true;
                    connection.AddOutgoingRequest(result.Item1, result.Item2);
                    return result;
                }
            }

            // for now it just finds the first interesting piece the Peer can offer;
            // later can be simply changed to whatever algorithm is needed

            // if we didn't find anything in pieces we're already downloading (or the Peer doesn't have it),
            // we simply find the first fitting piece the Peer has
            // зациклился по одному кусочку потому, что здесь нахожу первый не скачанный...
            // а надо находить первый не запрошенный
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
            connection.AddOutgoingRequest(interestingPieceIndex, 0);
            AddPendingIncomingPiece(interestingPieceIndex);
            // TODO: I guess I need to figure out real BlockSize, not just standard (what if the first block is smaller than normal, lol)
            return new Tuple<int, int, int>(interestingPieceIndex, 0, blockSize);
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

        public void AddBlock(int pieceIndex, int offset, byte[] block)
        {
            var entry = pendingIncomingPiecesInfo.First;
            bool found = false;
            while (entry != null && !found)
            {
                var nextNode = entry.Next;
                if (entry.Value.pieceIndex == pieceIndex)
                {
                    if (entry.Value.blocksMap[offset / blockSize] == false)
                    {
                        // block.length because the last block may be smaller than others
                        Array.Copy(block, 0, entry.Value.pieceBuffer, offset, block.Length);
                        entry.Value.blocksMap[offset / blockSize] = true;
                        entry.Value.bufferSize += block.Length;
                        //outgoingRequestsCount--;

                        downloaded += block.Length;
                        

                        // we accumulated the whole piece?
                        if (entry.Value.bufferSize == entry.Value.pieceBuffer.Length)
                        {
                            // check SHA1 and save to disk if OK
                            if (fileWorker.SaveToDisk(entry.Value))
                            {
                                // remove from pending incoming requests
                                pendingIncomingPiecesInfo.Remove(entry);
                                // Now we can send "HAVE" message
                                SendBroadcastHave(entry.Value.pieceIndex);
                                pieces[pieceIndex] = true;

                                ownerForm.UpdateProgress(this);
                                // if download is complete, need to send "Completed" event to the tracker
                            }
                            else
                            {
                                // if not, need to download the whole piece again
                                /*entry.Value.blocksMap.SetAll(false);
                                entry.Value.requestedBlocksMap.SetAll(false);
                                entry.Value.bufferSize = 0;*/
                                pendingIncomingPiecesInfo.Remove(entry);

                                downloaded -= entry.Value.pieceBuffer.Length;
                            }
                        }
                    }
                    found = true;
                }
                entry = nextNode;
            }
            if (!found)
            {
                // something went really wrong. I recieved a block I didn't asked for. Can it happen? What should I do?
                // Create a new entry for it, or just discard it? Or sever the connection? So many questions
            }
        }

        private void AddPendingIncomingPiece(int index)
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

            var newEntry = new PieceInfoNode(index, new byte[actualPieceSize],
                new BitArray((int)Math.Ceiling((double)actualPieceSize / blockSize)));
            // true because its index is gonna be returned and then immediatly requested
            newEntry.requestedBlocksMap[0] = true;
            pendingIncomingPiecesInfo.AddLast(newEntry);
        }

        private void SendBroadcastHave(int pieceIndex)
        {
            // TODO: lock?
            var msg = new PeerMessage(pieceIndex);
            foreach (var connection in connectedPeers)
            {
                connection.SendPeerMessage(msg);
            }
        }

        public void ReceivedChokeOrDisconnected(PeerConnection connection)
        {
            for (int i = 0; i < connection.outgoingRequests.Length; i++)
            {
                if (connection.outgoingRequests[i] != null)
                {
                    // no null-checking because it can't be null
                    FindRequestsPiece(connection.outgoingRequests[i].Item1).requestedBlocksMap[connection.outgoingRequests[i].Item2 /
                        blockSize] = false;
                    connection.outgoingRequests[i] = null;
                }
            }
        }
    }
}
