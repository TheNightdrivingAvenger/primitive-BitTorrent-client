using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
    * Just combinations of flags
    */
    [Flags]
    public enum CONNSTATES { None = 0, AM_CHOKING = 0b0001, AM_INTERESTED = 0b0010, PEER_CHOKING = 0b0100, PEER_INTERESTED = 0b1000 }

    // need a couple of timers for keep-alives, choking-unchoking
    // receiving is performed from the main thread; then received message is sent to another (MessageHandler) thread
    // for processing. All subsequent actions (setting logical connection state and sending messages to peers)
    // are performed from this MessageHandler thread
    public class PeerConnection
    {
        private const int MAXWRONGMESSAGES = 10;

        // for now used only by MessageHandler's thread, so no sync needed
        public BitArray peersPieces { get; private set; }
        // no sync, used only in main thread
        private TcpClient connectionClient;
        public CONNSTATES connectionState { get; private set; }
        private IPEndPoint endPoint;
        private byte[] infoHash;
        // do I need peerID here?..

        // need sync?
        // first int = piece number; second int = piece's block number
        public LinkedList<Tuple<int, int>> IncomingRequests { get; private set; }


        private int wrongCount;

        public delegate void MessageRecievedHandler(PeerMessage message, PeerConnection connection);
        private MessageRecievedHandler MsgRecieved;

        public PeerConnection(IPEndPoint ep, MessageRecievedHandler handler, int piecesCount, byte[] expectedInfoHash)
        {
            connectionState = 0;
            connectionState = CONNSTATES.AM_CHOKING | CONNSTATES.PEER_CHOKING;
            connectionClient = new TcpClient();
            peersPieces = new BitArray(piecesCount);
            endPoint = ep;
            MsgRecieved = handler;
            wrongCount = 0;

            IncomingRequests = new LinkedList<Tuple<int, int>>();
            infoHash = expectedInfoHash;
        }


        // !MAKE HANDSHAKING ALGORITHM BETTER!
        public async Task<int> PeerHandshake(byte[] infoHash, string peerID)
        {
            // exceptions (SocketException is caught by the caller)
            await connectionClient.ConnectAsync(endPoint.Address, endPoint.Port).ConfigureAwait(false);
            var handshakeMessage = new PeerMessage(infoHash, peerID);
            await connectionClient.GetStream().WriteAsync(handshakeMessage.GetMsgContents(), 0, 
                handshakeMessage.GetMsgContents().Length).ConfigureAwait(false);

            var message = await RecieveHandshakeMessage().ConfigureAwait(false);

            //add checking if hash in the response is valid
            if (message == null || message.messageType != MessageType.handshake)
            {
                return -1;
            }
            return 0;
        }

        private async Task<PeerMessage> RecieveHandshakeMessage()
        {
            var msg = new PeerMessage();
            int result = await msg.GetAndDecodeHandshake(infoHash, connectionClient.GetStream());
            if (result != 0)
            {
                return null;
            }
            return msg;
        }

        private async Task<PeerMessage> RecievePeerMessage()
        {
            int left = PeerMessage.msgLenSpace;
            int bufOffset = 0;

            var msg = new PeerMessage();
            // exceptions!
            // getting the first 4 bytes of the message that'll tell us how many more to expect
            while (left != 0)
            {
                int readres = await connectionClient.GetStream().ReadAsync(msg.GetMsgContents(), bufOffset, left);

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

            // count of peersPieces won't change, so no synchronization is needed. If it causes any problems,
            // I can just save the initial size and use it
            int result = await msg.GetAndDecode(connectionClient.GetStream(), peersPieces.Count);
            if (result == 1 || result == 2)
            {
                return null;
            }
            else if (result == 0)
            {
                return msg;
            }
            else
            { // don't do anything on -1: we got an invalid/unrecognizible message, so just skip it (calling code must check messagetype!)
                wrongCount++;
                return msg;
            }
        }

        public async void StartPeerMessageLoop()
        {
            // while (!cancelled) HERE!
            while (true)
            {
                PeerMessage msg = null;
                try
                {
                    msg = await RecievePeerMessage();
                }
                catch
                {
                    // if something went wrong, send "null" instead of message. Connection will be closed
                }
                if (msg != null)
                {
                    if (msg.messageType == MessageType.invalid)
                    {
                        if (wrongCount <= MAXWRONGMESSAGES)
                        {
                            continue;
                        }
                        else
                        {
                            msg = null;
                            MsgRecieved(msg, this);
                        }
                    }
                }
                MsgRecieved(msg, this);
                if (msg == null)
                {
                    break;
                }
            }
        }

        public void CloseConnection()
        {
            connectionClient.Close();
        }

        // ALL THE METHODS BELOW MAY BE CALLED FROM ANOTHER THREAD, SO THREAD-SAFETY MUST BE PROVIDED WHERE NEEDED //

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
            //int startOffset = PeerMessage.msgLenSpace + PeerMessage.msgTypeSpace;
            for (int i = message.rawBytesOffset; i < message.GetMsgContents().Length; i++)
            {
                // NO LOCKING because only MessageHandler's thread uses these values
                byte mask = 0b10000000;
                byte curByte = message.GetMsgContents()[i];
                for (int bit = 7; bit >= 0; bit--)
                {
                    if ((i - message.rawBytesOffset) * 8 + (7 - bit) > peersPieces.Count)
                    {
                        break;
                    }
                    peersPieces.Set((i - message.rawBytesOffset) * 8 + (7 - bit), (curByte & mask) == 1);
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

        /*public void SetAmHave(int index)
        {
            peersPieces.Set(index, true);
        }*/

        public void AddIncomingRequest(int piece, int offset)
        {
            // need lock?
            lock (IncomingRequests)
            {
                IncomingRequests.AddLast(new Tuple<int, int>(piece, offset));
            }
        }

        public void SendPeerMessage(MessageType type)
        {
            // reset the activity timer
            var message = new PeerMessage(type);

            // exceptions!
            connectionClient.GetStream().Write(message.GetMsgContents(), 0, message.GetMsgContents().Length);
        }

        // TODO: maybe I can move message creating to MessageHandler, and take as param only ready message
        /// <summary>
        /// Send "request" or "cancel" message
        /// </summary>
        /// <param name="type"></param>
        /// <param name="index"></param>
        /// <param name="begin"></param>
        /// <param name="length"></param>
        public void SendPeerMessage(MessageType type, int index, int begin, int length)
        {
            var message = new PeerMessage(type, index, begin, length);

            // exceptions!
            connectionClient.GetStream().Write(message.GetMsgContents(), 0, message.GetMsgContents().Length);
            // add to outgoing requests!
        }

        public void SendPeerMessage(int index, int begin /*, byte[] block*/)
        {

        }

        public void SendPeerMessage(int pieceIndex)
        {

        }
    }
}
