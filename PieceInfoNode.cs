using System;
using System.Collections;

namespace CourseWork
{
    public class PieceInfoNode
    {
        public int pieceIndex;
        public int bufferSize;
        public byte[] pieceBuffer;
        public BitArray blocksMap;
        public BitArray requestedBlocksMap;

        public PieceInfoNode(int pieceIndex, byte[] buffer, BitArray blocksMap)
        {
            this.pieceIndex = pieceIndex;
            this.pieceBuffer = buffer;
            this.blocksMap = blocksMap;
            this.requestedBlocksMap = new BitArray(blocksMap.Count);
        }
    }
}
