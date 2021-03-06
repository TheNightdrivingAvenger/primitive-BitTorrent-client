﻿using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CourseWork
{
    public class MessageHandler
    {
        public bool isStarted { get; private set; }
        public bool isStopped { get; private set; }
        public bool isCompleted { get; private set; }
        private Thread workerThread;
        private BlockingCollection<Message> messageQueue;

        public MessageHandler(int maxQueueLength)
        {
            //By default, the storage for a System.Collections.Concurrent.BlockingCollection<T> 
            //is System.Collections.Concurrent.ConcurrentQueue<T>.
            // why exactly maxQueueLength? Idk, OK for now
            messageQueue = new BlockingCollection<Message>(maxQueueLength);
            isStarted = false;
            isStopped = false;
            isCompleted = false;
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
                throw new InvalidOperationException("This instance of MessageHandler has been marked " +
                    "as finished OR is already started");
            }
        }

        private void HandlerLoop()
        {
            while (!messageQueue.IsCompleted)
            {
                Message msg;
                try
                {
                    msg = messageQueue.Take();
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                Handler(msg);
            }
            isCompleted = true;
        }

        private void Handler(Message msg)
        {
            if (msg is CommandMessage)
            {
                var message = (CommandMessage)msg;
                // perform needed connection controlling and management
                switch (message.messageType)
                {
                    case ControlMessageType.SendInner:
                        try
                        {
                            if (message.messageToSend.messageType == PeerMessageType.have)
                            {
                                // we haven't yet sent the bitfield, so can't send any other messages
                                // add it to the end of the queue
                                if (!message.messageToSend.targetConnection.bitfieldSent)
                                {
                                    AddTask(message);
                                    break;
                                }
                            }
                            message.messageToSend.targetConnection.SendPeerMessage(message.messageToSend);
                            if (message.messageToSend.messageType == PeerMessageType.bitfield)
                            {
                                message.messageToSend.targetConnection.bitfieldSent = true;
                            }
                        }
                        catch
                        {
                            message.messageToSend.targetConnection.CloseConnection();
                            message.messageToSend.targetFile.RemoveConnection(message.messageToSend.targetConnection);
                        }
                        break;
                    case ControlMessageType.CloseConnection:
                        // what if here I try to close an already closed connection?
                        // for example somewhere in the queue there's a message "Close connection",
                        // but it fails to send something and is closed earlier
                        try
                        {
                            message.targetConnection.CloseConnection();
                            message.targetFile.RemoveConnection(message.targetConnection);
                        }
                        catch (ObjectDisposedException)
                        {
                            // then do nothing; it has been closed somewhere else before
                        }
                        break;
                }
            }
            else
            {
                var message = (PeerMessage)msg;
                switch (message.messageType)
                {
                    case PeerMessageType.keepAlive:
                        return; // return because I don't need to call "ConnectionStateChanged"
                    case PeerMessageType.choke:
                        message.targetConnection.SetPeerChoking();
                        message.targetFile.ReceivedChokeOrDisconnected(message.targetConnection);
                        break;
                    case PeerMessageType.unchoke:
                        message.targetConnection.SetPeerUnchoking();
                        break;
                    case PeerMessageType.interested:
                        message.targetConnection.SetPeerInterested();
                        return;
                    case PeerMessageType.notInterested:
                        message.targetConnection.SetPeerNotInterested();
                        if (!message.targetConnection.connectionState.HasFlag(CONNSTATES.AM_CHOKING))
                        {
                            try
                            {
                                message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.choke));
                                message.targetConnection.SetAmChoking();
                            }
                            catch
                            {
                                message.targetConnection.CloseConnection();
                                message.targetFile.RemoveConnection(message.targetConnection);
                            }
                        }
                        return;
                    case PeerMessageType.have:
                        message.targetConnection.SetPeerHave(message.pieceIndex);
                        break;
                    case PeerMessageType.bitfield:
                        message.targetConnection.SetBitField(message);
                        break;
                    case PeerMessageType.request:
                        return;
                    case PeerMessageType.piece:
                        byte[] block = new byte[message.GetMsgContents().Length - message.rawBytesOffset];
                        Array.Copy(message.GetMsgContents(), message.rawBytesOffset, block, 0, block.Length);
                        message.targetFile.AddBlock(message.pieceIndex, message.pieceOffset, block);
                        try
                        {
                            message.targetConnection.RemoveOutgoingRequest(message.pieceIndex, message.pieceOffset);
                        }
                        catch (ArgumentException)
                        {
                            // do nothing, because we (most likely) received a piece we cancelled sometime earlier
                        }
                        break;
                    case PeerMessageType.cancel:
                        return;
                    case PeerMessageType.port:
                        return;
                }
                if (message.targetFile.state == DownloadState.downloading)
                {
                    // stopping means we shouldn't ask any more pieces
                    ConnectionStateChanged(message);
                }
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
                        try
                        {
                            message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.notInterested));
                            message.targetConnection.SetAmNotInterested();
                        }
                        catch
                        {
                            message.targetConnection.CloseConnection();
                            message.targetFile.RemoveConnection(message.targetConnection);
                        }
                    }
                }
                else
                {
                    if (interestingPieces)
                    {
                        try
                        {
                            message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.interested));
                            message.targetConnection.SetAmInterested();
                        }
                        catch
                        {
                            message.targetConnection.CloseConnection();
                            message.targetFile.RemoveConnection(message.targetConnection);
                        }
                    }
                }
            }
            else
            {
                if (!interestingPieces && message.targetConnection.outgoingRequestsCount == 0)
                {
                    try
                    {
                        message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.notInterested));
                        message.targetConnection.SetAmNotInterested();
                    }
                    catch
                    {
                        message.targetConnection.CloseConnection();
                        message.targetFile.RemoveConnection(message.targetConnection);
                    }
                }
                else if (interestingPieces && 
                    message.targetConnection.outgoingRequestsCount < message.targetConnection.maxPendingOutgoingRequestsCount)
                {

                    Tuple<int, int, int> nextRequest = message.targetFile.FindNextRequest(message.targetConnection);
                    // <= because last ++ (if nextRequest is not null) occured in FindNextRequest, so we need to send
                    // this newly added request
                    while (nextRequest != null &&
                        message.targetConnection.outgoingRequestsCount <= message.targetConnection.maxPendingOutgoingRequestsCount)
                    {
                        try
                        {
                            message.targetConnection.AddOutgoingRequest(nextRequest.Item1, nextRequest.Item2);
                            message.targetConnection.SendPeerMessage(new PeerMessage(PeerMessageType.request, nextRequest.Item1,
                                nextRequest.Item2, nextRequest.Item3));
                        }
                        catch
                        {
                            message.targetConnection.CloseConnection();
                            message.targetFile.RemoveConnection(message.targetConnection);
                            break;
                        }

                        if (message.targetConnection.outgoingRequestsCount < message.targetConnection.maxPendingOutgoingRequestsCount)
                        {
                            nextRequest = message.targetFile.FindNextRequest(message.targetConnection);
                        }
                        else
                        {
                            nextRequest = null;
                        }
                    }
                }
            }
        }

        public void AddTask(Message newMessage)
        {
            messageQueue.Add(newMessage);
        }

        public void Stop()
        {
            // first set isStopped, so no more threads can add to the collection
            // if this method is interrupted by task-switching between calling
            // CompleteAdding and setting isStopped
            isStopped = true;
            messageQueue.CompleteAdding();
        }
    }
}
