﻿using System;
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
        private BlockingCollection<Message> messageQueue;

        public MessageHandler(int maxQueueLength, LinkedList<DownloadingFile> downloadingFiles)
        {
            //By default, the storage for a System.Collections.Concurrent.BlockingCollection<T> 
            //is System.Collections.Concurrent.ConcurrentQueue<T>.
            // TODO: why exactly maxQueueLength? Idk, OK for now
            messageQueue = new BlockingCollection<Message>(maxQueueLength);
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
                Message msg;
                try
                {
                    // TODO: It's a good idea to not block here, but instead go and check if there're any
                    // requests for me to send blocks. If there are, then go and send a couple of messages to peers
                    // (keep-alives maybe, "pieces"). OR wait for CommandMessage (now things have changed)
                    msg = messageQueue.Take();
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                Handler(msg);
            }
        }

        private void Handler(Message msg)
        {
            // TODO: what if I recieve handshake? I mean, if someone's trying to connect to me and I'm not the initiator
            if (msg is CommandMessage)
            {
                var message = (CommandMessage)msg;
                // perform needed connection controlling and management
                switch (message.messageType)
                {
                    case ControlMessageType.SendKeepAlive:
                        // send keep-alive here
                        break;
                    case ControlMessageType.SendCancel:
                        // send cancel here
                        break;
                }
            }
            else
            {
                var message = (PeerMessage)msg;
                switch (message.messageType)
                {
                    case PeerMessageType.keepAlive:
                        // TODO: reset the activity timer
                        return; // return because I don't need to call "ConnectionStateChanged"
                                // maybe if I have some pending requests on this connection and do not receive them, I could send "cancel"
                                // and then ask for blocks somewhere else
                    case PeerMessageType.choke:
                        message.targetConnection.SetPeerChoking();
                        message.targetFile.ReceivedChokeOrDisconnected(message.targetConnection);
                        break;
                    case PeerMessageType.unchoke:
                        message.targetConnection.SetPeerUnchoking();
                        break;
                    case PeerMessageType.interested:
                        message.targetConnection.SetPeerInterested();
                        // TODO: choking-unchoking algorithms and stuff
                        break;
                    case PeerMessageType.notInterested:
                        message.targetConnection.SetPeerNotInterested();
                        if (!message.targetConnection.connectionState.HasFlag(CONNSTATES.AM_CHOKING))
                        {
                            message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.choke));
                            message.targetConnection.SetAmChoking();
                        }
                        break;
                    case PeerMessageType.have:
                        message.targetConnection.SetPeerHave(message.pieceIndex);
                        break;
                    case PeerMessageType.bitfield:
                        message.targetConnection.SetBitField(message);
                        break;
                    case PeerMessageType.request:
                        // add pending !INCOMING! piece request to FileWorker's list! (if it's not there yet)
                        break;
                    case PeerMessageType.piece:
                        // CHECK IF PIECE (BLOCK) SIZE IS CORRECT! (<= 2^14)
                        // TODO: move this check to FileWorker/DownloadingFile
                        if (message.GetMsgContents().Length - message.rawBytesOffset <= 16384)
                        {
                            byte[] block = new byte[message.GetMsgContents().Length - message.rawBytesOffset];
                            Array.Copy(message.GetMsgContents(), message.rawBytesOffset, block, 0, block.Length);
                            // for now it sends "HAVE" messages by itself, but for consistency it could be better
                            // if this method would do this, because it controls all other behavior of connection
                            message.targetFile.AddBlock(message.pieceIndex, message.pieceOffset, block);
                        }
                        //try
                        //{
                            message.targetConnection.RemoveOutgoingRequest(message.pieceIndex, message.pieceOffset);
                        //}
                        //catch (ArgumentException)
                        //{
                            // do nothing, because we (most likely) received a piece we cancelled sometime earlier
                        //}
                        break;
                    case PeerMessageType.cancel:
                        // TODO: remove from pending incoming requests (whatever this means now)
                        break;
                    case PeerMessageType.port:
                        return;
                }
                ConnectionStateChanged(message);
            }

        }

        private void ConnectionStateChanged(PeerMessage message)
        {
            bool interestingPieces = message.targetFile.PeerHasInterestingPieces(message.targetConnection);
            if (message.targetConnection.connectionState.HasFlag(CONNSTATES.PEER_CHOKING))
            {
                if (message.targetConnection.connectionState.HasFlag(CONNSTATES.AM_INTERESTED))
                {
                    if (!interestingPieces)
                    {
                        message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.notInterested));
                        message.targetConnection.SetAmNotInterested();
                    }
                }
                else
                {
                    if (interestingPieces)
                    {
                        message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.interested));
                        message.targetConnection.SetAmInterested();
                    }
                }
            }
            else
            {
                if (!interestingPieces && message.targetConnection.outgoingRequestsCount == 0)
                {
                    message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.notInterested));
                    message.targetConnection.SetAmNotInterested();
                }
                else if (interestingPieces && 
                    message.targetConnection.outgoingRequestsCount < message.targetConnection.maxPendingOutgoingRequestsCount)
                {
                    // TODO: try to optimize this
                    Tuple<int, int, int> nextRequest = message.targetFile.FindNextRequest(message.targetConnection);
                    // <= because last ++ (if nextRequest is not null) occured in FindNextRequest, so we need to send
                    // this newly added request
                    while (nextRequest != null &&
                        message.targetConnection.outgoingRequestsCount <= message.targetConnection.maxPendingOutgoingRequestsCount)
                    {
                        message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.request, nextRequest.Item1,
                            nextRequest.Item2, nextRequest.Item3));
                        //tuple.Item3.AddOutgoingRequest(nextRequest.Item1, nextRequest.Item2);

                        if (message.targetConnection.outgoingRequestsCount < message.targetConnection.maxPendingOutgoingRequestsCount)
                        {
                            nextRequest = message.targetFile.FindNextRequest(message.targetConnection);
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
        public void AddTask(Message newMessage)
        {
            messageQueue.Add(newMessage);
        }

        public void Stop()
        {
            messageQueue.CompleteAdding();
        }
    }
}
