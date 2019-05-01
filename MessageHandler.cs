using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CourseWork
{
    public class MessageHandler
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
            // TODO: why exactly maxQueueLength? Idk, OK for now
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
                    // TODO: It's a good idea to not block here, but instead go and check if there're any
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
            // TODO: what if I recieve handshake? I mean, if someone's trying to connect to me and I'm not the initiator
            switch (tuple.Item2.messageType)
            {
                case MessageType.keepAlive:
                    // TODO: reset the activity timer
                    return; // return because I don't need to call "ConnectionStateChanged"
                    // maybe if I have some pending requests on this connection and do not receive them, I could send "cancel"
                    // and then ask for blocks somewhere else
                case MessageType.choke:
                    tuple.Item3.SetPeerChoking();
                    tuple.Item1.ReceivedChokeOrDisconnected(tuple.Item3);
                    break;
                case MessageType.unchoke:
                    tuple.Item3.SetPeerUnchoking();
                    break;
                case MessageType.interested:
                    tuple.Item3.SetPeerInterested();
                    // TODO: choking-unchoking algorithms and stuff
                    break;
                case MessageType.notInterested:
                    tuple.Item3.SetPeerNotInterested();
                    if (!tuple.Item3.connectionState.HasFlag(CONNSTATES.AM_CHOKING))
                    {
                        tuple.Item3.SendPeerMessage(new PeerMessage(MessageType.choke));
                        tuple.Item3.SetAmChoking();
                    }
                    break;
                case MessageType.have:
                    tuple.Item3.SetPeerHave(tuple.Item2.pieceIndex);
                    break;
                case MessageType.bitfield:
                    tuple.Item3.SetBitField(tuple.Item2);
                    break;
                case MessageType.request:
                    // add pending !INCOMING! piece request to FileWorker's list! (if it's not there yet)
                    break;
                case MessageType.piece:
                    // CHECK IF PIECE (BLOCK) SIZE IS CORRECT! (<= 2^14)
                    // TODO: move this check to FileWorker/DownloadingFile
                    if (tuple.Item2.GetMsgContents().Length - tuple.Item2.rawBytesOffset <= 16384)
                    {
                        byte[] block = new byte[tuple.Item2.GetMsgContents().Length - tuple.Item2.rawBytesOffset];
                        Array.Copy(tuple.Item2.GetMsgContents(), tuple.Item2.rawBytesOffset, block, 0, block.Length);
                        // for now it sends "HAVE" messages by itself, but for consistency it could be better
                        // if this method would do this, because it controls all other behavior of connection
                        tuple.Item1.AddBlock(tuple.Item2.pieceIndex, tuple.Item2.pieceOffset, block);
                    }
                    tuple.Item3.RemoveOutgoingRequest(tuple.Item2.pieceIndex, tuple.Item2.pieceOffset);
                    //tuple.Item3.pendingOutgoingRequestsCount--;
                    break;
                case MessageType.cancel:
                    // TODO: remove from pending incoming requests (whatever this means now)
                    break;
                case MessageType.port:
                    return;
            }
            ConnectionStateChanged(tuple);
        }

        private void ConnectionStateChanged(Tuple<DownloadingFile, PeerMessage, PeerConnection> tuple)
        {
            bool interestingPieces = tuple.Item1.PeerHasInterestingPieces(tuple.Item3);
            if (tuple.Item3.connectionState.HasFlag(CONNSTATES.PEER_CHOKING))
            {
                if (tuple.Item3.connectionState.HasFlag(CONNSTATES.AM_INTERESTED))
                {
                    if (!interestingPieces)
                    {
                        tuple.Item3.SendPeerMessage(new PeerMessage(MessageType.notInterested));
                        tuple.Item3.SetAmNotInterested();
                    }
                }
                else
                {
                    if (interestingPieces)
                    {
                        tuple.Item3.SendPeerMessage(new PeerMessage(MessageType.interested));
                        tuple.Item3.SetAmInterested();
                    }
                }
            }
            else
            {
                if (!interestingPieces && tuple.Item3.outgoingRequestsCount == 0)
                {
                    tuple.Item3.SendPeerMessage(new PeerMessage(MessageType.notInterested));
                    tuple.Item3.SetAmNotInterested();
                }
                else if (interestingPieces && tuple.Item3.outgoingRequestsCount < tuple.Item3.maxPendingOutgoingRequestsCount)
                {
                    // TODO: try to optimize this
                    Tuple<int, int, int> nextRequest = tuple.Item1.FindNextRequest(tuple.Item3);
                    // <= because last ++ (if nextRequest is not null) occured in FindNextRequest, so we need to send
                    // this newly added request
                    while (nextRequest != null && tuple.Item3.outgoingRequestsCount <= tuple.Item3.maxPendingOutgoingRequestsCount)
                    {
                        tuple.Item3.SendPeerMessage(new PeerMessage(MessageType.request, nextRequest.Item1,
                            nextRequest.Item2, nextRequest.Item3));
                        //tuple.Item3.AddOutgoingRequest(nextRequest.Item1, nextRequest.Item2);

                        if (tuple.Item3.outgoingRequestsCount < tuple.Item3.maxPendingOutgoingRequestsCount)
                        {
                            nextRequest = tuple.Item1.FindNextRequest(tuple.Item3);
                            //tuple.Item3.AddOutgoingRequest(nextRequest.Item1, nextRequest.Item2);
                        }
                        else
                        {
                            nextRequest = null;
                        }
                    }
                }
            }


            /*Tuple<int, int, int> nextRequest = tuple.Item1.FindNextRequest(tuple.Item3);
        
            if (nextRequest != null)
            {
                // if Peer is choking me, need to tell him I'm interested
                if (tuple.Item3.connectionState.HasFlag(CONNSTATES.PEER_CHOKING))
                {
                    if (!tuple.Item3.connectionState.HasFlag(CONNSTATES.AM_INTERESTED))
                    {
                        // send AM_INTERESTED message
                        tuple.Item3.SendPeerMessage(new PeerMessage(MessageType.interested));
                        tuple.Item3.SetAmInterested();
                    }
                }
                else
                {
                    tuple.Item3.SendPeerMessage(new PeerMessage(MessageType.request, nextRequest.Item1,
                        nextRequest.Item2, nextRequest.Item3));
                }
            } // TODO: send "not interested" ONLY when peer has no interesting pieces AND I've really requested blocks
            // and got all that I requested
            else if (tuple.Item3.connectionState.HasFlag(CONNSTATES.AM_INTERESTED))
            {
                tuple.Item3.SendPeerMessage(new PeerMessage(MessageType.notInterested));
                tuple.Item3.SetAmNotInterested();
            }*/
        }

        // System.InvalidOperationException: 'Коллекция была помечена, как завершенная, с учетом добавлений.'
        // TODO: can happen if I've closed the main form (and stopped the MessageHandler),
        // but connections are still active and try to add messages to the queue
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
