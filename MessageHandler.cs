using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CourseWork
{
    class MessageHandler
    {
        // when we've got nothing to do, we go and check downloading files
        // (only active right now I guess) and their connections to see if
        // we can send something to peers on these connections (for example, pieces)
        private LinkedList<DownloadingFile> downloadingFiles;

        public bool isStarted { get; private set; }
        private Thread workerThread;
        private BlockingCollection<Tuple<DownloadingFile, PeerMessage, PeerConnection>> messageQueue;

        public MessageHandler(int maxQueueLength, LinkedList<DownloadingFile> downloadingFiles)
        {
            //By default, the storage for a System.Collections.Concurrent.BlockingCollection<T> 
            //is System.Collections.Concurrent.ConcurrentQueue<T>.
            //why exactly 500? Idk, OK for now
            messageQueue = new BlockingCollection<Tuple<DownloadingFile, PeerMessage, PeerConnection>>(maxQueueLength);
            isStarted = false;
            this.downloadingFiles = downloadingFiles;
        }

        public void Start()
        {
            if (!(messageQueue.IsAddingCompleted || isStarted))
            {
                isStarted = true;
                workerThread = new Thread(HandlerLoop);
                workerThread.Start();
            }
            else
            {
                throw new InvalidOperationException("This instance of MessageHandler has been marked as finished OR is already started");
            }
        }

        private void HandlerLoop()
        {
            while (!messageQueue.IsCompleted)
            {
                Tuple<DownloadingFile, PeerMessage, PeerConnection> msg;
                try
                {
                    // It's a good idea to not block here, but instead go and check if there're any
                    // requests for me to send blocks. If there are, then go and send a couple of messages to peers
                    // (keep-alives maybe, "pieces")
                    msg = messageQueue.Take();
                }
                catch (InvalidOperationException ex)
                {
                    break;
                }
                Handler(msg);
            }
        }

        private void Handler(Tuple<DownloadingFile, PeerMessage, PeerConnection> tuple)
        {
            switch (tuple.Item2.messageType)
            {
                case MessageType.keepAlive:
                    // send a response?
                    // reset the activity timer
                    break;
                case MessageType.choke:
                    tuple.Item3.SetChoked();
                    break;
                case MessageType.unchoke:
                    tuple.Item3.SetUnchoked();
                    break;
                case MessageType.interested:
                    tuple.Item3.SetInterested();
                    break;
                case MessageType.notInterested:
                    tuple.Item3.SetNotInterested();
                    break;
                case MessageType.have:
                    tuple.Item3.SetHave(tuple.Item2.pieceIndex);
                    break;
                case MessageType.bitfield:
                    tuple.Item3.SetBitField(tuple.Item2);
                    break;
                case MessageType.request:
                    // add pending !INCOMING! piece to FileWorker's list! (if it's not there yet)
                    break;
                case MessageType.piece:
                    // CHECK IF PIECE (BLOCK) SIZE IS CORRECT! (<= 2^14)
                    // TODO: move this check to FileWorker
                    if (tuple.Item2.GetMsgContents().Length - tuple.Item2.rawBytesOffset <= 16384)
                    {
                        byte[] block = new byte[tuple.Item2.GetMsgContents().Length - tuple.Item2.rawBytesOffset];
                        Array.Copy(tuple.Item2.GetMsgContents(), tuple.Item2.rawBytesOffset, block, 0, block.Length);
                        tuple.Item1.fileWorker.AddBlock(tuple.Item2.pieceIndex, tuple.Item2.pieceOffset, block);
                    }
                    break;
                case MessageType.cancel:
                    // remove from pending incoming requests (whatever this means now)
                    break;
                case MessageType.port:
                    break;
            }
            ConnectionStateChanged(tuple);
        }

        private void ConnectionStateChanged(Tuple<DownloadingFile, PeerMessage, PeerConnection> tuple)
        {
            
            // See if the peer have something to offer
            int interestingPieceIndex = -1;
            // no synchronization because only this thread can read and modify connection.peersPieces values
            for (int i = 0; i < tuple.Item3.peersPieces.Length; i++)
            {
                if (tuple.Item1.pieces[i] == false && tuple.Item3.peersPieces[i] == true)
                {
                    interestingPieceIndex = i;
                    break;
                }
            }

            if (interestingPieceIndex >= 0)
            {
                //ADD AN OUTGOING REQUEST to FileWorker
                // if I'm choked
                if (tuple.Item3.connectionState.HasFlag(CONNSTATES.PEER_CHOKING))
                {
                    if (!tuple.Item3.connectionState.HasFlag(CONNSTATES.AM_INTERESTED))
                    {
                        // send AM_INTERESTED message
                        tuple.Item3.SendPeerMessage(MessageType.interested);
                    }
                }
                else
                {
                    // send REQUEST message and add an outgoing request
                    // if PIECE !requestedYet && BLOCK !requestedYet
                    Tuple<int, int> offsetAndSize = tuple.Item1.fileWorker.FindNextOffsetAndSize(interestingPieceIndex);
                    tuple.Item3.SendPeerMessage(MessageType.request, interestingPieceIndex, offsetAndSize.Item1, offsetAndSize.Item2);
                }
            }
            else
            {
                if (tuple.Item3.connectionState.HasFlag(CONNSTATES.AM_INTERESTED))
                {
                    // send AM_NOT_INTERESTED message
                    tuple.Item3.SendPeerMessage(MessageType.notInterested);
                }
            }
        }

        public void AddTask(Tuple<DownloadingFile, PeerMessage, PeerConnection> messageFromDownload)
        {
            messageQueue.Add(messageFromDownload);
        }

        public void Stop()
        {
            messageQueue.CompleteAdding();
        }
    }
}
