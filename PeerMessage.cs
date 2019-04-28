using System;
using System.Collections;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// MESSAGE LENGTH DOES NOT INCLUDE 4 BYTES THAT CONTAIN LENGTH ITSELF

namespace CourseWork
{
    // negative values are not allowed
    // values >255 are not allowed
    public enum MessageType
    {
        unknown = 100, invalid, handshake, keepAlive, choke = 0, unchoke, interested, notInterested, have, bitfield,
        request, piece, cancel, port
    };

    public class PeerMessage
    {
        private byte[] msgContents;
        public MessageType messageType { get; private set; }

        public int pieceIndex { get; private set; } = -1;
        public int pieceOffset { get; private set; } = -1;
        public int length { get; private set; } = -1;
        // raw bytes: "block" in "piece" message;
        //            "bitfield" in "bitfield" message;
        public int rawBytesOffset { get; private set; } = -1;

        private const int pstrLenSpace = 1;
        private const string pstr = "BitTorrent protocol";
        private const int reservedLen = 8;

        // size of big-endian message length
        // size of message type field
        public const int msgTypeSpace = 1;
        public const int msgIntSpace = 4;
        public const int msgLenSpace = msgIntSpace;
        //private const int hashLen = 20;
        //private const int peerIDLen = 20;

        public byte[] GetMsgContents()
        {
            return msgContents;
        }

        /// <summary>
        /// Creates an empty message for subsequent retrieving and decoding with async GetAndDecode method
        /// Buffer size is 4 bytes (enough for initial getting the message's length)
        /// </summary>
        public PeerMessage()
        {
            msgContents = new byte[msgLenSpace];
            messageType = MessageType.unknown;
        }

        // Making only handshake time limited because it's the first and crucial message;
        // if it's delayed then there're probably some problems with connection to peer
        // or peer is faulting. Other (subsequent) messages may be large, and connection
        // after handshake is believed to be stable, so if something happens we wait until
        // some TCP error or something else, which will lead to connection closing

        // TODO: ✓ make awaiting for a response not infinite!
        public async Task<int> GetAndDecodeHandshake(byte[] expectedInfoHash, NetworkStream stream, int delay)
        {
            messageType = MessageType.invalid;
            Array.Resize(ref msgContents, pstrLenSpace + pstr.Length + reservedLen + 20 + 20);
            int readres = 0;
            int read = 0;
            int bufOffset = 0;

            while (read < msgContents.Length)
            {
                var handshakeCancellationTokenSource = new CancellationTokenSource();
                try
                {
                    handshakeCancellationTokenSource.CancelAfter(delay);
                    readres = await stream.ReadAsync(msgContents, bufOffset, msgContents.Length - read, handshakeCancellationTokenSource.Token);
                    if (readres == 0)
                    {
                        messageType = MessageType.invalid;
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

            if (msgContents[0] != pstr.Length)
            {
                return 2;
            }
            if (Encoding.ASCII.GetString(msgContents, 1, pstr.Length) != pstr)
            {
                return 2;
            }
            if (!CompareHashes(expectedInfoHash))
            {
                return 2;
            }
            messageType = MessageType.handshake;
            return 0;
        }

        private bool CompareHashes(byte[] expected)
        {
            int i = 0;
            int msgOffset = pstrLenSpace + pstr.Length + reservedLen;
            while (i < expected.Length && (expected[i] == msgContents[i + msgOffset]))
            {
                i++;
            }
            if (i == expected.Length)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Creates a handshake message
        /// </summary>
        /// <param name="infohash">Hash of torrent's info dictionary</param>
        /// <param name="peerID">ID of local client</param>
        public PeerMessage(byte[] infoHash, string peerID)
        {
            messageType = MessageType.handshake;
            msgContents = new byte[pstrLenSpace + pstr.Length + reservedLen + infoHash.Length + peerID.Length];
            //1 byte for pstrlen + pstrlen + 8 zeroed reserved bytes + 20 bytes of info hash + 20 bytes of peer ID
            msgContents[0] = (byte)pstr.Length;
            Encoding.ASCII.GetBytes(pstr, 0, pstr.Length, msgContents, 1);
            Array.Copy(infoHash, 0, msgContents, pstrLenSpace + (byte)pstr.Length + reservedLen, infoHash.Length);
            Encoding.ASCII.GetBytes(peerID, 0, peerID.Length, msgContents, pstrLenSpace + (byte)pstr.Length +
                reservedLen + infoHash.Length);
        }

        /// <summary>
        /// Creates a message with no payload (keep-alive, choke, unchoke, interested or not interested)
        /// </summary>
        /// <param name="type">Type of the message</param>
        /// <exception cref="ArgumentException">Thrown if passed message type must have a payload</exception>
        public PeerMessage(MessageType type)
        {
            if (type == MessageType.keepAlive)
            {
                msgContents = new byte[msgLenSpace];
            }
            else if (type == MessageType.choke || type == MessageType.unchoke || type == MessageType.interested
                || type == MessageType.notInterested)
            {
                msgContents = new byte[msgLenSpace + msgTypeSpace];
                Array.Copy(HTONNTOH(BitConverter.GetBytes(1)), msgContents, msgLenSpace);
                msgContents[msgLenSpace] = (byte)type;
            }
            else
            {
                throw new ArgumentException($"This message type: {type.ToString()} must have non-empty payload");
            }
        }

        /// <summary>
        /// Creates a bitfield message
        /// </summary>
        /// <param name="bitfield">Bitfield to include in the message</param>
        public PeerMessage(BitArray bitfield)
        {
            messageType = MessageType.bitfield;
            bitfield.CopyTo(new byte[(int)Math.Ceiling((double)bitfield.Length / 8)], 0);
            msgContents = new byte[msgLenSpace + msgTypeSpace + bitfield.Length];
            // TODO: Implement constructor for "bitfield" message
            // some WTFs with BitField.Copy method; later
        }

        /// <summary>
        /// Creates a "have" message
        /// </summary>
        /// <param name="pieceIndex">Zero-based index of the piece</param>
        public PeerMessage(int pieceIndex)
        {
            messageType = MessageType.have;
            msgContents = new byte[msgLenSpace + msgTypeSpace + msgIntSpace];
            Array.Copy(HTONNTOH(BitConverter.GetBytes(5)), msgContents, msgLenSpace);
            msgContents[msgLenSpace] = (byte)messageType;
            Array.Copy(HTONNTOH(BitConverter.GetBytes(pieceIndex)), 0, msgContents, msgLenSpace + msgTypeSpace, msgIntSpace);
            this.pieceIndex = pieceIndex;
        }

        /// <summary>
        /// Creates "request" or "cancel" messages
        /// </summary>
        /// <param name="index">Zero-based piece index</param>
        /// <param name="begin">Zero-based offset within the piece</param>
        /// <param name="length">Specifies the requested (cancelled) block's length</param>
        public PeerMessage(MessageType type, int index, int begin, int length)
        {
            if (!(type == MessageType.request || type == MessageType.cancel))
            {
                throw new ArgumentException("Type of the message must be \"request\" or \"cancel\"");
            }
            messageType = type;
            msgContents = new byte[msgLenSpace + msgTypeSpace + msgIntSpace * 3];
            Array.Copy(HTONNTOH(BitConverter.GetBytes(13)), msgContents, msgLenSpace);
            msgContents[msgLenSpace] = (byte)messageType;
            Array.Copy(HTONNTOH(BitConverter.GetBytes(index)), 0, msgContents, msgLenSpace + msgTypeSpace, msgIntSpace);
            Array.Copy(HTONNTOH(BitConverter.GetBytes(begin)), 0, msgContents, msgLenSpace + msgTypeSpace + msgIntSpace, msgIntSpace);
            Array.Copy(HTONNTOH(BitConverter.GetBytes(length)), 0, msgContents, msgLenSpace + msgTypeSpace + msgIntSpace * 2, msgIntSpace);
            this.pieceIndex = index;
            this.pieceOffset = begin;
            this.length = length;
        }

        /// <summary>
        /// Creates "piece" message
        /// </summary>
        /// <param name="index">Zero-based piece index</param>
        /// <param name="begin">Zero-based offset within the piece</param>
        /// <param name="block">Block of data itself</param>
        public PeerMessage(int index, int begin, byte[] block)
        {
            messageType = MessageType.piece;
            msgContents = new byte[msgLenSpace + msgTypeSpace + msgIntSpace * 2 + block.Length];
            Array.Copy(HTONNTOH(BitConverter.GetBytes(msgContents.Length - msgLenSpace)), msgContents, msgLenSpace);
            msgContents[msgLenSpace] = (byte)messageType;
            Array.Copy(HTONNTOH(BitConverter.GetBytes(index)), 0, msgContents, msgLenSpace + msgTypeSpace, msgIntSpace);
            Array.Copy(HTONNTOH(BitConverter.GetBytes(begin)), 0, msgContents, msgLenSpace + msgTypeSpace + msgIntSpace, msgIntSpace);
            this.pieceIndex = index;
            this.pieceOffset = begin;
            this.rawBytesOffset = msgLenSpace + msgTypeSpace + msgIntSpace * 2;
            Array.Copy(block, 0, msgContents, rawBytesOffset, block.Length);
        }

        /// <summary>
        /// Recieves and decodes a message after its length has been determined.
        /// Sets all the fields and properties according to the new message, so the object is fully initialized now
        /// </summary>
        /// <param name="stream">A NetworkStream to read message from</param>
        /// <returns>Status code: -1 on invalid message ID,
        /// 1 if connection was dropped in the middle of receiving for some reason,
        /// 2 if peer sent an ill-formed message,
        /// 0 on success</returns>
        public async Task<int> GetAndDecode(NetworkStream stream, int expectedBitfieldLength)
        {
            // no copy because msgContents is only 4 bytes long at this point and contains only BE message length
            //byte[] len = new byte[msgLenSpace];
            //Array.Copy(msgContents, 0, len, 0, msgLenSpace);
            int msgLen = BitConverter.ToInt32(HTONNTOH(msgContents), 0);
            // also can do something if the length is way too big
            if (msgLen == 0)
            {
                messageType = MessageType.keepAlive;
                return 0;
            }

            Array.Resize(ref msgContents, msgLenSpace + msgLen);
            int readres = 0;
            int read = 0;
            int bufOffset = msgLenSpace;
            while (read < msgLen)
            {
                // exceptions
                readres = await stream.ReadAsync(msgContents, bufOffset, msgLen - read);
                if (readres == 0)
                {
                    messageType = MessageType.invalid;
                    return 1;
                }
                read += readres;
                bufOffset += readres;
            }

            // additional checking for a bitfield
            // TODO: move checking to MessageHandler probably
            if (msgContents[msgLenSpace] == 5)
            {
                int bitsCount = (msgLen - msgTypeSpace) * 8;

                if (bitsCount < expectedBitfieldLength || bitsCount > expectedBitfieldLength + 7)
                {
                    return 2;
                }

                if (bitsCount == expectedBitfieldLength)
                {
                    // OK
                }
                else
                {
                    byte lastByte = msgContents[msgContents.Length - 1];
                    lastByte <<= bitsCount - expectedBitfieldLength;
                    if (lastByte != 0)
                    {
                        return 2;
                    }
                }
            }
            // 10 is a damn magic constant, but ok for now...
            // it's maximum possible ID of a message
            if (msgContents[msgLenSpace] <= 10)
            {
                messageType = (MessageType)msgContents[msgLenSpace];
            } else
            {
                messageType = MessageType.invalid;
                return -1;
            }

            switch (messageType)
            {
                case MessageType.have:
                    byte[] num = new byte[msgIntSpace];
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace, num, 0, msgIntSpace);
                    this.pieceIndex = BitConverter.ToInt32(HTONNTOH(num), 0);
                    break;
                case MessageType.bitfield:
                    this.rawBytesOffset = msgLenSpace + msgTypeSpace;
                    break;
                case MessageType.request:
                case MessageType.cancel:
                    byte[] index = new byte[msgIntSpace];
                    byte[] begin = new byte[msgIntSpace];
                    byte[] length = new byte[msgIntSpace];
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace, index, 0, msgIntSpace);
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace + msgIntSpace, begin, 0, msgIntSpace);
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace + msgIntSpace * 2, length, 0, msgIntSpace);
                    this.pieceIndex = BitConverter.ToInt32(HTONNTOH(index), 0);
                    this.pieceOffset = BitConverter.ToInt32(HTONNTOH(begin), 0);
                    this.length = BitConverter.ToInt32(HTONNTOH(length), 0);
                    break;
                case MessageType.piece:
                    byte[] index1 = new byte[msgIntSpace];
                    byte[] begin1 = new byte[msgIntSpace];
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace, index1, 0, msgIntSpace);
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace + msgIntSpace, begin1, 0, msgIntSpace);

                    this.pieceIndex = BitConverter.ToInt32(HTONNTOH(index1), 0);
                    this.pieceOffset = BitConverter.ToInt32(HTONNTOH(begin1), 0);
                    this.rawBytesOffset = msgLenSpace + msgTypeSpace + msgIntSpace * 2;
                    break;
                case MessageType.port:
                    break;
            }
            return 0;
        }
        /*
        /// <summary>
        /// Performs host to network and vise-versa conversion of signed 32-bit integer
        /// </summary>
        /// <param name="number">Number to convert</param>
        /// <returns>Unsigned integer in native/network byte order</returns>
        public static int HTONNTOH(int number)
        {
            byte[] bytesArr = BitConverter.GetBytes(number);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytesArr);
            }
            return BitConverter.ToInt32(bytesArr, 0);
        }*/

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
