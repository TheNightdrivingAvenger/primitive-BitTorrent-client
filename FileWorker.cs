using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using BencodeNET;
using BencodeNET.Torrents;

namespace CourseWork
{
    public class FileWorker
    {
        // element contains piece index, buffer actual (used) size, piece buffer and BitArray describing the piece

        private long pieceOffset;
        private int blockSize;
        private string rootDir;
        Torrent torrent;
        private int lastPieceSize;
        public long totalSize { get; private set; }

        private FileStream[] files;

        private LinkedList<PieceInfoNode> pendingIncomingPiecesInfo;
        //private LinkedList<PieceInfoNode> pendingOutgoingiecesInfo;

        public FileWorker(long pieceSize, string rootDir, Torrent torrent, int blockSize)
        {
            this.blockSize = blockSize;
            // I hope only MessageHandler's thread will write to the file, so FileShare can be read for others and write only for this thread
            if (torrent.File != null)
            {
                files = new FileStream[1];
                // check FileName for errors!
                files[0] = File.Open(rootDir + torrent.File.FileName, FileMode.Open, FileAccess.Write, FileShare.Read);
                totalSize = torrent.File.FileSize;
                
            }
            else
            {
                files = new FileStream[torrent.Files.Count];
                int i = 0;
                foreach (var fileInfo in torrent.Files)
                {
                    // will be there an exception if path (directories) does not exist?
                    files[i] = File.Open(rootDir + fileInfo.FullPath, FileMode.Open, FileAccess.Write, FileShare.Read);
                    i++;
                    totalSize += fileInfo.FileSize;
                }
            }
            lastPieceSize = (int)(torrent.NumberOfPieces * torrent.PieceSize - totalSize);

            this.rootDir = rootDir;
            this.torrent = torrent;

            pendingIncomingPiecesInfo = new LinkedList<PieceInfoNode>();
            //pendingOutgoingiecesInfo = new LinkedList<PieceInfoNode>();
        }

        public Tuple<int, int> FindNextOffsetAndSize(int index)
        {
            Tuple<int, int> result = null;
            int resultOffset = 0;
            bool found = false;
            foreach (var entry in pendingIncomingPiecesInfo)
            {
                if (entry.pieceIndex == index)
                {
                    for (int i = 0; i < entry.blocksMap.Count; i++)
                    {
                        // well, it can't be that all of them will be "true", but...
                        // can't be because all completed pieces we write in the disk and remove them from this list,
                        // so completed pieces (with all blocks) can't be in pendingIncomingPiecesInfo
                        if (entry.blocksMap[i] == false)
                        {
                            resultOffset = i * blockSize;
                            if (i == entry.blocksMap.Count - 1)
                            {
                                result = new Tuple<int, int>(resultOffset, GetLastBlockSize(index));
                            }
                            else
                            {
                                result = new Tuple<int, int>(resultOffset, blockSize);
                            }
                            break;
                        }
                    }
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                AddPendingIncomingPiece(index);
                result = new Tuple<int, int>(0, blockSize);
            }
            return result;
        }

        private int GetLastBlockSize(int index)
        {
            int totalBlocks;
            if (index == torrent.NumberOfPieces - 1)
            {
                // how many blocks are in the last piece?
                totalBlocks = (int)Math.Ceiling((double)lastPieceSize / blockSize);
                return totalBlocks * blockSize - lastPieceSize;
            }
            else
            {
                // how many blocks are in a regular piece?
                totalBlocks = (int)Math.Ceiling((double)torrent.PieceSize / blockSize);
                return (int)(totalBlocks * blockSize - torrent.PieceSize);
            }
        }

        private void AddPendingIncomingPiece(int index)
        {
            var newEntry = new PieceInfoNode(index, new byte[torrent.PieceSize],
                new BitArray((int)Math.Ceiling((double)torrent.PieceSize / blockSize)));
            pendingIncomingPiecesInfo.AddLast(newEntry);
        }

        public void AddBlock(int pieceIndex, int offset, byte[] block)
        {
            var entry = pendingIncomingPiecesInfo.First;
            bool found = false;
            while (entry != null)
            {
                var nextNode = entry.Next;
                if (entry.Value.pieceIndex == pieceIndex)
                {
                    if (entry.Value.blocksMap[offset / blockSize] == false)
                    {
                        // block.length because the last block may be smaller than others
                        Array.Copy(block, 0, entry.Value.pieceBuffer, offset, block.Length);
                        entry.Value.blocksMap[offset / blockSize] = true;
                        entry.Value.bufferSize += block.Length;
                        
                        if (entry.Value.bufferSize == entry.Value.pieceBuffer.Length)
                        {
                            // remove from pending incoming requests
                            // check SHA1 and save to disk if OK
                            SaveToDisk(entry.Value);
                            pendingIncomingPiecesInfo.Remove(entry);
                        }
                    }
                    found = true;
                    break;
                }
                entry = nextNode;
            }
            if (!found)
            {
                // something went really wrong. I recieved a block I didn't asked for. Can it happen? What should I do?
                // Create a new entry for it, or just discard it? Or sever the connection? So many questions
            }

        }

        private void SaveToDisk(PieceInfoNode entry)
        {
            byte[] hashResult;
            using (SHA1 hasher = new SHA1CryptoServiceProvider())
            {
                hashResult = hasher.ComputeHash(entry.pieceBuffer);
            }

        }

        public void AddToPieceBuffer(int index, byte[] block, int offset)
        {
            
        }

        //public void AddPendingOutgoingPiece()

        public void LoadPieceFromDisk(int index)
        {

        }
    }
}
