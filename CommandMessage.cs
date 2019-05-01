using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseWork
{
    public enum ControlMessageType { SendKeepAlive, SendCancel };
    public class CommandMessage : Message
    {
        public ControlMessageType messageType;
        public int pieceIndex;
        public int pieceOffset;

        public CommandMessage(ControlMessageType messageType, int pieceIndex, int pieceOffset)
        {
            this.messageType = messageType;
            this.pieceIndex = pieceIndex;
            this.pieceOffset = pieceOffset;
        }
    }
}
