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
    // invalid means 
    public enum PeerMessageType
    {
        unknown = 100, invalid, handshake, keepAlive, choke = 0, unchoke, interested, notInterested, have, bitfield,
        request, piece, cancel, port
    };

    public class PeerMessage : Message
    {
        private byte[] msgContents;
        public PeerMessageType messageType { get; private set; }

        public int pieceIndex { get; private set; } = -1;
        public int pieceOffset { get; private set; } = -1;
        public int length { get; private set; } = -1;
        // raw bytes: "block" in "piece" message;
        //            "bitfield" in "bitfield" message;
        public int rawBytesOffset { get; private set; } = -1;

        public const int pstrLenSpace = 1;
        public const string pstr = "BitTorrent protocol";
        public const int reservedLen = 8;

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

        public PeerMessage(byte[] msgContents, byte[] expectedInfoHash)
        {
            messageType = PeerMessageType.invalid;
            this.msgContents = msgContents;

            if (msgContents[0] != pstr.Length)
            {
                return;
            }
            if (Encoding.ASCII.GetString(msgContents, 1, pstr.Length) != pstr)
            {
                return;
            }
            if (!CompareHashes(expectedInfoHash))
            {
                return;
            }
            messageType = PeerMessageType.handshake;
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
            messageType = PeerMessageType.handshake;
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
        public PeerMessage(PeerMessageType type)
        {
            if (type == PeerMessageType.keepAlive)
            {
                msgContents = new byte[msgLenSpace];
            }
            else if (type == PeerMessageType.choke || type == PeerMessageType.unchoke || type == PeerMessageType.interested
                || type == PeerMessageType.notInterested)
            {
                msgContents = new byte[msgLenSpace + msgTypeSpace];
                Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(1)), msgContents, msgLenSpace);
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
            messageType = PeerMessageType.bitfield;
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
            messageType = PeerMessageType.have;
            msgContents = new byte[msgLenSpace + msgTypeSpace + msgIntSpace];
            Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(5)), msgContents, msgLenSpace);
            msgContents[msgLenSpace] = (byte)messageType;
            Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(pieceIndex)), 0, msgContents, msgLenSpace + msgTypeSpace, msgIntSpace);
            this.pieceIndex = pieceIndex;
        }

        /// <summary>
        /// Creates "request" or "cancel" messages
        /// </summary>
        /// <param name="index">Zero-based piece index</param>
        /// <param name="begin">Zero-based offset within the piece</param>
        /// <param name="length">Specifies the requested (cancelled) block's length</param>
        public PeerMessage(PeerMessageType type, int index, int begin, int length)
        {
            if (!(type == PeerMessageType.request || type == PeerMessageType.cancel))
            {
                throw new ArgumentException("Type of the message must be \"request\" or \"cancel\"");
            }
            messageType = type;
            msgContents = new byte[msgLenSpace + msgTypeSpace + msgIntSpace * 3];
            Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(13)), msgContents, msgLenSpace);
            msgContents[msgLenSpace] = (byte)messageType;
            Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(index)), 0, msgContents, msgLenSpace + msgTypeSpace, msgIntSpace);
            Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(begin)), 0, msgContents, msgLenSpace + msgTypeSpace + msgIntSpace, msgIntSpace);
            Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(length)), 0, msgContents, msgLenSpace + msgTypeSpace + msgIntSpace * 2, msgIntSpace);
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
            messageType = PeerMessageType.piece;
            msgContents = new byte[msgLenSpace + msgTypeSpace + msgIntSpace * 2 + block.Length];
            Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(msgContents.Length - msgLenSpace)), msgContents, msgLenSpace);
            msgContents[msgLenSpace] = (byte)messageType;
            Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(index)), 0, msgContents, msgLenSpace + msgTypeSpace, msgIntSpace);
            Array.Copy(PeerConnection.HTONNTOH(BitConverter.GetBytes(begin)), 0, msgContents, msgLenSpace + msgTypeSpace + msgIntSpace, msgIntSpace);
            this.pieceIndex = index;
            this.pieceOffset = begin;
            this.rawBytesOffset = msgLenSpace + msgTypeSpace + msgIntSpace * 2;
            Array.Copy(block, 0, msgContents, rawBytesOffset, block.Length);
        }

        public PeerMessage(byte[] msgContents, int expectedBitfieldLength)
        {
            if (msgContents.Length == msgLenSpace)
            {
                messageType = PeerMessageType.keepAlive;
                return;
            }

            // TODO: move checking to MessageHandler probably
            if (msgContents[msgLenSpace] == 5)
            {
                int bitsCount = (msgContents.Length - msgLenSpace - msgTypeSpace) * 8;

                if (bitsCount < expectedBitfieldLength || bitsCount > expectedBitfieldLength + 7)
                {
                    messageType = PeerMessageType.invalid;
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
                        messageType = PeerMessageType.invalid;
                        return;
                    }
                }
            }
            // 10 is a damn magic constant, but ok for now...
            // it's maximum possible ID of a message
            if (msgContents[msgLenSpace] <= 10)
            {
                messageType = (PeerMessageType)msgContents[msgLenSpace];
            }
            else
            {
                messageType = PeerMessageType.unknown;
                return;
            }

            this.msgContents = msgContents;

            switch (messageType)
            {
                case PeerMessageType.have:
                    byte[] num = new byte[msgIntSpace];
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace, num, 0, msgIntSpace);
                    this.pieceIndex = BitConverter.ToInt32(PeerConnection.HTONNTOH(num), 0);
                    break;
                case PeerMessageType.bitfield:
                    this.rawBytesOffset = msgLenSpace + msgTypeSpace;
                    break;
                case PeerMessageType.request:
                case PeerMessageType.cancel:
                    byte[] index = new byte[msgIntSpace];
                    byte[] begin = new byte[msgIntSpace];
                    byte[] length = new byte[msgIntSpace];
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace, index, 0, msgIntSpace);
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace + msgIntSpace, begin, 0, msgIntSpace);
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace + msgIntSpace * 2, length, 0, msgIntSpace);
                    this.pieceIndex = BitConverter.ToInt32(PeerConnection.HTONNTOH(index), 0);
                    this.pieceOffset = BitConverter.ToInt32(PeerConnection.HTONNTOH(begin), 0);
                    this.length = BitConverter.ToInt32(PeerConnection.HTONNTOH(length), 0);
                    break;
                case PeerMessageType.piece:
                    byte[] index1 = new byte[msgIntSpace];
                    byte[] begin1 = new byte[msgIntSpace];
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace, index1, 0, msgIntSpace);
                    Array.Copy(msgContents, msgLenSpace + msgTypeSpace + msgIntSpace, begin1, 0, msgIntSpace);

                    this.pieceIndex = BitConverter.ToInt32(PeerConnection.HTONNTOH(index1), 0);
                    this.pieceOffset = BitConverter.ToInt32(PeerConnection.HTONNTOH(begin1), 0);
                    this.rawBytesOffset = msgLenSpace + msgTypeSpace + msgIntSpace * 2;
                    break;
                case PeerMessageType.port:
                    break;
            }
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
    }
}
