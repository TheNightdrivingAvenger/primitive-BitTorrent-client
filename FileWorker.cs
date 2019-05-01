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

        /*private long pieceOffset;
        private int blockSize;
        public long lastPieceSize { get; private set; }
        public long totalSize { get; private set; }*/

        private FileStream[] files;
        private string rootDir;
        Torrent torrent;

        //private LinkedList<PieceInfoNode> pendingOutgoingiecesInfo;

        public FileWorker(long pieceSize, string rootDir, Torrent torrent)
        {
            // I hope only MessageHandler's thread will write to the file, so FileShare can be read for others and write only for this thread
            if (torrent.File != null)
            {
                files = new FileStream[1];
                // check FileName for errors!
                files[0] = File.Open(rootDir + Path.DirectorySeparatorChar + torrent.File.FileName, FileMode.Create, FileAccess.Write, FileShare.Read);
                files[0].SetLength(torrent.File.FileSize);
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
                }
            }

            this.rootDir = rootDir;
            this.torrent = torrent;
        }

        public bool SaveToDisk(PieceInfoNode entry)
        {
            byte[] hashResult;
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
            }
            

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
            // TODO: flush is for debugging only; disable in release
            //files[i - 1].Flush(true);
            // can get an "Out of range" if the next file doesn't exist. Need to watch out for this if peer sends wrong data!
            if (countToWrite < entry.pieceBuffer.Length)
            {
                files[i].Seek(0, SeekOrigin.Begin);
                files[i].Write(entry.pieceBuffer, countToWrite, entry.pieceBuffer.Length - countToWrite);
                //files[i].Flush(true);
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
