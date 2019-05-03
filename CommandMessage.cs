using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseWork
{
    public enum ControlMessageType { SendKeepAlive, SendCancel, CloseConnection };
    public class CommandMessage : Message
    {
        public ControlMessageType messageType;
        public int pieceIndex;
        public int pieceOffset;
        public int blockSize;

        public CommandMessage(ControlMessageType messageType, DownloadingFile file, PeerConnection connection,
            int pieceIndex, int pieceOffset, int blockSize)
        {
            this.messageType = messageType;
            this.pieceIndex = pieceIndex;
            this.pieceOffset = pieceOffset;
            this.blockSize = blockSize;

            base.targetFile = file;
            base.targetConnection = connection;
        }
    }
}
