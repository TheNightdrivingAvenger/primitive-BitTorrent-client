﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CourseWork
{
    /*
    * 0bWXY0 -- amNotChoking
    * 0bWXY1 -- amChoking
    * 0bWX0Z -- amNotInterested
    * 0bWX1Z -- amInterested
    * 0bW0YZ -- peerNotChoking
    * 0bW1YZ -- peerChoking
    * 0b0XYZ -- peerNotInterested
    * 0b1XYZ -- peerInterested
    */
    [Flags]
    public enum CONNSTATES { None = 0, AM_CHOKING = 0b0001, AM_INTERESTED = 0b0010, PEER_CHOKING = 0b0100, PEER_INTERESTED = 0b1000 }

    // receiving is performed from the main thread; then received message is sent to another (MessageHandler) thread
    // for processing. All subsequent actions (setting logical connection state and sending messages to peers)
    // are performed from this MessageHandler thread

    public class PeerConnection
    {
        private const int MAXWRONGMESSAGES = 10;

        public BitArray peersPieces { get; private set; }
        private TcpClient connectionClient;
        public CONNSTATES connectionState { get; private set; }
        private IPEndPoint endPoint;
        private byte[] infoHash;

        public bool bitfieldSent;

        public Tuple<int, int>[] outgoingRequests;
        public int outgoingRequestsCount { get; private set; }

        // first int = piece number; second int = piece's block number
        public LinkedList<Tuple<int, int>> IncomingRequests { get; private set; }

        public int maxPendingOutgoingRequestsCount;
        private int wrongCount;

        public delegate void MessageRecievedHandler(PeerMessage message, PeerConnection connection);
        private MessageRecievedHandler MsgRecieved;

        private Timer activityTimer;
        private const byte TIMEOUT = 120;

        private DownloadingFile.TimeOutCallBack TimeOutCallback;

        public PeerConnection(IPEndPoint ep, MessageRecievedHandler handler, int piecesCount, byte[] expectedInfoHash,
            DownloadingFile.TimeOutCallBack TimeOut)
        {
            connectionState = 0;
            connectionState = CONNSTATES.AM_CHOKING | CONNSTATES.PEER_CHOKING;
            connectionClient = new TcpClient();
            peersPieces = new BitArray(piecesCount);
            bitfieldSent = false;
            endPoint = ep;
            MsgRecieved = handler;
            wrongCount = 0;
            maxPendingOutgoingRequestsCount = 5;

            IncomingRequests = new LinkedList<Tuple<int, int>>();
            infoHash = expectedInfoHash;
            outgoingRequests = new Tuple<int, int>[maxPendingOutgoingRequestsCount];
            outgoingRequestsCount = 0;
            TimeOutCallback = TimeOut;
        }

        private void TimerCallback(object info)
        {
            TimeOutCallback(this);
        }

        private void ResetTimer()
        {
            activityTimer.Change(TIMEOUT * 1000, TIMEOUT * 1000);
        }

        public async Task<int> PeerHandshakeAsync(byte[] infoHash, string peerID, CancellationTokenSource cancellationToken)
        {            
            await connectionClient.ConnectAsync(endPoint.Address, endPoint.Port).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return -1;
            }
            var handshakeMessage = new PeerMessage(infoHash, peerID);
            await connectionClient.GetStream().WriteAsync(handshakeMessage.GetMsgContents(), 0, 
                handshakeMessage.GetMsgContents().Length, cancellationToken.Token).ConfigureAwait(false);

            var message = await RecieveHandshakeMessageAsync(cancellationToken).ConfigureAwait(false);

            if (message == null || message.messageType != PeerMessageType.handshake)
            {
                return -1;
            }
            // setting the timer for two minutes
            activityTimer = new Timer(TimerCallback, null, TIMEOUT * 1000, TIMEOUT * 1000);
            return 0;
        }

        private async Task<PeerMessage> RecieveHandshakeMessageAsync(CancellationTokenSource cancellationToken)
        {
            byte[] buf = new byte[PeerMessage.pstrLenSpace + PeerMessage.pstr.Length + PeerMessage.reservedLen + 20 + 20];
            // 3000 -- cancel receiving handshake if peer hasn't responded in 3 seconds
            int result = await GetAndDecodeHandshakeAsync(buf, 3000, cancellationToken);
            if (result != 0)
            {
                return null;
            }

            return new PeerMessage(buf, infoHash);
        }

        // Making only handshake time limited because it's the first and crucial message;
        // if it's delayed then there're probably some problems with connection to peer
        // or peer is faulting. Other (subsequent) messages may be large, and connection
        // after handshake is believed to be stable, so if something happens we wait until
        // some TCP error or something else, which will lead to connection closing
        public async Task<int> GetAndDecodeHandshakeAsync(byte[] buf, int delay, CancellationTokenSource cancellationToken)
        {
            int readres = 0;
            int read = 0;
            int bufOffset = 0;

            while (read < buf.Length)
            {
                var handshakeCancellationTokenSource = new CancellationTokenSource();
                try
                {
                    handshakeCancellationTokenSource.CancelAfter(delay);
                    readres = await connectionClient.GetStream().ReadAsync(buf, bufOffset,
                        buf.Length - read, handshakeCancellationTokenSource.Token);

                    if (cancellationToken.IsCancellationRequested || readres == 0)
                    {
                        return 1;
                    }
                    read += readres;
                    bufOffset = read;
                }
                catch // I don't care what exception occured (network error or time-out), it's all failure
                {
                    return 1;
                }
                finally
                {
                    handshakeCancellationTokenSource.Dispose();
                }
            }
            return 0;
        }

        private async Task<PeerMessage> RecievePeerMessageAsync()
        {
            int left = PeerMessage.msgLenSpace;
            int bufOffset = 0;
            byte[] buf = new byte[4];
            // getting the first 4 bytes of the message that'll tell us how many more to expect
            while (left != 0)
            {
                int readres = 0;

                readres = await connectionClient.GetStream().ReadAsync(buf, bufOffset, left);
                if (readres == 0)
                {
                    return null;
                }
                else
                {
                    left -= readres;
                    bufOffset += readres;
                }
            }
            buf = await GetAndDecodeAsync(buf);
            if (buf == null)
            {
                return null;
            }
            // count of peersPieces won't change, so no synchronization is needed
            return new PeerMessage(buf, peersPieces.Count);
        }

        /// <summary>
        /// Recieves and decodes a message after its length has been determined.
        /// </summary>
        /// <param name="buf">Buffer to receive message to</param>
        /// <returns>Status code: 1 on any error, 0 on success</returns>
        public async Task<byte[]> GetAndDecodeAsync(byte[] buf)
        {
            // no copy because msgContents is only 4 bytes long at this point and contains only BE message length
            int msgLen = BitConverter.ToInt32(HTONNTOH(buf), 0);
            if (msgLen == 0)
            {
                return new byte[4];
            }

            byte[] result = new byte[PeerMessage.msgLenSpace + msgLen];
            int readres = 0;
            int read = 0;
            int bufOffset = PeerMessage.msgLenSpace;
            while (read < msgLen)
            {
                readres = await connectionClient.GetStream().ReadAsync(result, bufOffset, msgLen - read);
                if (readres == 0)
                {
                    return null;
                }
                read += readres;
                bufOffset += readres;
            }
            return result;
        }

        public async void StartPeerMessageLoop()
        {
            while (true)
            {
                PeerMessage msg;
                try
                {
                    msg = await RecievePeerMessageAsync();
                }
                catch
                {
                    // if something went wrong, dispatch "null" instead of a message. Connection will be closed
                    msg = null;
                }
                if (msg != null)
                {
                    ResetTimer();
                    if (msg.messageType == PeerMessageType.unknown)
                    {
                        if (wrongCount <= MAXWRONGMESSAGES)
                        {
                            continue;
                        }
                        else
                        {
                            msg = null;
                        }
                    }
                    if (msg.messageType == PeerMessageType.invalid)
                    {
                        msg = null;
                    }
                }
                MsgRecieved(msg, this);
                if (msg == null)
                {
                    break;
                }
            }
            activityTimer.Dispose();
        }

        public void CloseConnection()
        {
            connectionClient.Close();
            connectionClient.Dispose();
        }

        // enum updates are atomic
        public void SetPeerChoking()
        {
            connectionState |= CONNSTATES.PEER_CHOKING;
        }

        public void SetPeerUnchoking()
        {
            connectionState &= ~CONNSTATES.PEER_CHOKING;
        }

        public void SetPeerInterested()
        {
            connectionState |= CONNSTATES.PEER_INTERESTED;
        }

        public void SetPeerNotInterested()
        {
            connectionState &= ~CONNSTATES.PEER_INTERESTED;
        }

        public void SetPeerHave(int index)
        {
            peersPieces.Set(index, true);
        }

        public void SetBitField(PeerMessage message)
        {
            for (int i = message.rawBytesOffset; i < message.GetMsgContents().Length; i++)
            {
                byte mask = 0b10000000;
                byte curByte = message.GetMsgContents()[i];
                for (int bit = 7; bit >= 0; bit--)
                {
                    if ((i - message.rawBytesOffset) * 8 + (7 - bit) >= peersPieces.Count)
                    {
                        break;
                    }
                    peersPieces.Set((i - message.rawBytesOffset) * 8 + (7 - bit), (curByte & mask) != 0);
                    mask >>= 1;
                }
            }
        }

        public void SetAmChoking()
        {
            connectionState |= CONNSTATES.AM_CHOKING;
        }

        public void SetAmUnchoking()
        {
            connectionState &= ~CONNSTATES.AM_CHOKING;
        }

        public void SetAmInterested()
        {
            connectionState |= CONNSTATES.AM_INTERESTED;
        }

        public void SetAmNotInterested()
        {
            connectionState &= ~CONNSTATES.AM_INTERESTED;
        }


        public void AddIncomingRequest(int piece, int offset)
        {
            IncomingRequests.AddLast(new Tuple<int, int>(piece, offset));
        }

        public void SendPeerMessage(PeerMessage message)
        {
            ResetTimer();
            connectionClient.GetStream().Write(message.GetMsgContents(), 0, message.GetMsgContents().Length);
        }

        public void AddOutgoingRequest(int pieceIndex, int offset)
        {
            bool placeFound = false;
            for (int i = 0; i < outgoingRequests.Length && !placeFound; i++)
            {
                if (outgoingRequests[i] == null)
                {
                    outgoingRequests[i] = new Tuple<int, int>(pieceIndex, offset);
                    placeFound = true;
                }
            }
            if (!placeFound)
            {
                throw new IndexOutOfRangeException("No place has been found for new request");
            }
            else
            {
                outgoingRequestsCount++;
            }
        }

        public void RemoveOutgoingRequest(int pieceIndex, int offset)
        {
            bool entryFound = false;
            for (int i = 0; i < outgoingRequests.Length && !entryFound; i++)
            {
                if (outgoingRequests[i] != null && outgoingRequests[i].Item1 == pieceIndex && outgoingRequests[i].Item2 == offset)
                {
                    outgoingRequests[i] = null;
                    entryFound = true;
                }
            }
            if (!entryFound)
            {
                throw new ArgumentException("Such entry could not be found");
            }
            else
            {
                outgoingRequestsCount--;
            }
        }

        /// <summary>
        /// Performs host to network and vise-versa conversion of signed 32-bit integer
        /// </summary>
        /// <param name="bytes">Number to convert (as 4 bytes array)</param>
        /// <returns>Array with needed byte order (use BitConverter to get the number)</returns>
        public static byte[] HTONNTOH(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }
    }
}
