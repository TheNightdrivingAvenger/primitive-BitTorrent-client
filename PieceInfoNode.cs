using System;
using System.Collections;

namespace CourseWork
{
    // class only because it's reference type and makes it simplier; should have been a struct
    public class PieceInfoNode
    {
        public int pieceIndex;
        public int bufferSize;
        public byte[] pieceBuffer;
        public BitArray blocksMap;
        public BitArray requestedBlocksMap;
        //public long connectionID;

        public PieceInfoNode(int pieceIndex, byte[] buffer, BitArray blocksMap/*, IntPtr connectionID*/)
        {
            this.pieceIndex = pieceIndex;
            this.pieceBuffer = buffer;
            this.blocksMap = blocksMap;
            this.requestedBlocksMap = new BitArray(blocksMap.Count);
            //this.connectionID = connectionID.ToInt64();
            //this.lastBlockSize = 
        }
    }
}
