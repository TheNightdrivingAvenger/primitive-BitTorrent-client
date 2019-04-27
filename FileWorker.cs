using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

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
        public int lastPieceSize { get; private set; }
        public long totalSize { get; private set; }

        private FileStream[] files;

        private LinkedList<PieceInfoNode> pendingIncomingPiecesInfo;
        //private LinkedList<PieceInfoNode> pendingOutgoingiecesInfo;

        public delegate void SavedToDiskDelegate(int pieceIndex);
        private SavedToDiskDelegate SavedToDisk;

        public FileWorker(long pieceSize, string rootDir, Torrent torrent, int blockSize, SavedToDiskDelegate savedDelegate)
        {
            this.SavedToDisk = savedDelegate;

            this.blockSize = blockSize;
            // I hope only MessageHandler's thread will write to the file, so FileShare can be read for others and write only for this thread
            if (torrent.File != null)
            {
                files = new FileStream[1];
                // check FileName for errors!
                files[0] = File.Open(rootDir + Path.DirectorySeparatorChar + torrent.File.FileName, FileMode.Create, FileAccess.Write, FileShare.Read);
                totalSize = torrent.File.FileSize;
                files[0].SetLength(totalSize);
            }
            else
            {
                files = new FileStream[torrent.Files.Count];
                int i = 0;
                foreach (var fileInfo in torrent.Files)
                {
                    // will be there an exception if path (directories) does not exist? // CreateNew instead of Create?
                    files[i] = File.Open(rootDir + Path.DirectorySeparatorChar + fileInfo.FullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    files[i].SetLength(fileInfo.FileSize);
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
            bool entryFound = false;
            bool spaceFound = false;
            foreach (var entry in pendingIncomingPiecesInfo)
            {
                if (entry.pieceIndex == index)
                {
                    for (int i = 0; i < entry.blocksMap.Count; i++)
                    {
                        // well, it can't be that all of them will be "true", but...
                        // can't be because all completed pieces we write to the disk and remove them from this list,
                        // so completed pieces (with all blocks) can't be in pendingIncomingPiecesInfo
                        if (entry.blocksMap[i] == false && entry.requestedBlocksMap[i] == false)
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
                            spaceFound = true;
                            entry.requestedBlocksMap[i] = true;
                            break;
                        }
                    }
                    entryFound = true;
                    break;
                }
            }
            if (!entryFound)
            {
                AddPendingIncomingPiece(index);
                // I guess I need to figure out real BlockSize, not just standard
                result = new Tuple<int, int>(0, blockSize);
            }
            else if (!spaceFound)
            {
                // all blocks from this piece have been already requested, so caller must try again
                return null;
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
                int size = totalBlocks * blockSize - lastPieceSize;
                return size == 0 ? blockSize : size;
            }
            else
            {
                // how many blocks are in a regular piece?
                totalBlocks = (int)Math.Ceiling((double)torrent.PieceSize / blockSize);
                int size = (int)(totalBlocks * blockSize - torrent.PieceSize);
                return size == 0 ? blockSize : size;
            }
        }

        private void AddPendingIncomingPiece(int index)
        {
            long actualPieceSize;
            if (index == torrent.NumberOfPieces - 1)
            {
                actualPieceSize = lastPieceSize;
            }
            else
            {
                actualPieceSize = torrent.PieceSize;
            }

            var newEntry = new PieceInfoNode(index, new byte[actualPieceSize],
                new BitArray((int)Math.Ceiling((double)actualPieceSize / blockSize)));
            // true because its index is gonna be returned and then immediatly requested
            newEntry.requestedBlocksMap[0] = true;
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
                            // check SHA1 and save to disk if OK
                            if (SaveToDisk(entry.Value))
                            {
                                // remove from pending incoming requests
                                SavedToDisk(entry.Value.pieceIndex);
                                pendingIncomingPiecesInfo.Remove(entry);
                                // TODO: Now we can send "HAVE" message!
                            }
                            else
                            {
                                // need to download the whole piece again
                                entry.Value.blocksMap.SetAll(false);
                                entry.Value.requestedBlocksMap.SetAll(false);
                                entry.Value.bufferSize = 0;
                            }
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

        private bool SaveToDisk(PieceInfoNode entry)
        {
           /* byte[] hashResult;
            // something is wrong with resulting hash..
            using (SHA1 hasher = new SHA1CryptoServiceProvider())
            {
                hashResult = hasher.ComputeHash(entry.pieceBuffer);
            }
            // check if HASH is OK
            if (!CompareHashes(hashResult, entry.pieceIndex * 20))
            {
                // an error here.. TODO: Need to drop the piece and mark it as available for downloading
                return false;
            }*/
            

            // is data reliable? Won't I jump off the file boundaries?..
            long fileOffset = entry.pieceIndex * torrent.PieceSize;
            int i = 0;
            long curOffset = 0;
            do
            {
                curOffset += files[i].Length;
                i++;
            } while (i < files.Length && curOffset < fileOffset);

            // offset in the target [i - 1] file
            fileOffset -= curOffset - files[i - 1].Length;
            files[i - 1].Seek(fileOffset, SeekOrigin.Begin);
            int countToWrite = files[i - 1].Length - fileOffset >= entry.pieceBuffer.Length ? entry.pieceBuffer.Length :
                (int)(files[i - 1].Length - fileOffset);

            // async would be nice, but what about synchronization then?
            files[i - 1].Write(entry.pieceBuffer, 0, countToWrite);
            // can get an "Out of range" if the next file doesn't exist. Need to watch out for this if peer sends wrong data!
            if (countToWrite < entry.pieceBuffer.Length)
            {
                files[i].Seek(0, SeekOrigin.Begin);
                files[i].Write(entry.pieceBuffer, countToWrite, entry.pieceBuffer.Length - countToWrite);
            }
            return true;
        }

        private bool CompareHashes(byte[] hash, int strOffset)
        {
            //int i = strOffset;
            int i = 0;
            while (i < hash.Length && (hash[i] == torrent.Pieces[i + strOffset]))
            {
                i++;
            }
            if (i == hash.Length)
            {
                return true;
            }
            return false;
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
