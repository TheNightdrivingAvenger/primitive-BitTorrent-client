using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BencodeNET.Objects;
using BencodeNET.Torrents;

namespace CourseWork
{
    public class FileWorker
    {
        private const string sessionFileName = "currentsessionVST.mainsess";

        private FileStream[] files;
        private string rootDir;
        private string infoFileName;
        private string torrentCopyName;
        private static FileStream mainSession;
        private FileStream infoFile;
        private FileStream torrentCopy;
        Torrent torrent;

        public bool filesMissing { get; }
        //private LinkedList<PieceInfoNode> pendingOutgoingPiecesInfo;

        public FileWorker(string rootDir, Torrent torrent, bool restoring)
        {
            this.rootDir = rootDir;
            this.torrent = torrent;
            Directory.CreateDirectory(rootDir);

            FileMode fileMode = FileMode.Create;
            torrentCopyName = rootDir + ClearPath(torrent.DisplayName) + "VSTtbup.torrent";
            if (restoring)
            {
                fileMode = FileMode.Open;
            }
            else
            {
                torrentCopy = File.Open(torrentCopyName, FileMode.Create, FileAccess.Write, FileShare.Read);
                try
                {
                    CreateTorrentBackup();
                }
                catch
                {
                    CleanUp(true);
                    throw;
                }
            }
            filesMissing = false;

            try
            {
                infoFileName = rootDir + ClearPath(torrent.DisplayName) + "VST.session";
                infoFile = File.Open(infoFileName, fileMode, FileAccess.Write, FileShare.Read);
            }
            catch
            {
                CleanUp(!restoring);
                throw;
            }

            if (torrent.File != null)
            {
                files = new FileStream[1];

                try
                {
                    files[0] = File.Open(rootDir + ClearPath(torrent.File.FileName),
                        fileMode, FileAccess.ReadWrite, FileShare.Read);
                    if (restoring && files[0].Length != torrent.File.FileSize)
                    {
                        filesMissing = true;
                    }
                    files[0].SetLength(torrent.File.FileSize);
                }
                catch
                {
                    files[0].Close();
                    CleanUp(!restoring);
                    if (!restoring)
                    {
                        try
                        {
                        //filestream.Name contains filename
                            File.Delete(files[0].Name);
                        }
                        catch { }
                    }
                    throw;
                }
            }
            else
            {
                files = new FileStream[torrent.Files.Count];
                int fileIndex = 0;

                foreach (var fileInfo in torrent.Files)
                {
                    string path = "";
                    
                    // get path to the file (except for the file name)

                    for (int i = 0; i < fileInfo.Path.Count - 1; i++)
                    {
                        path += ClearPath(fileInfo.Path[i]) + Path.DirectorySeparatorChar;
                    }
                    try
                    {
                        Directory.CreateDirectory(rootDir + path);

                        files[fileIndex] = File.Open(rootDir + path + ClearPath(fileInfo.FileName),
                            fileMode, FileAccess.ReadWrite, FileShare.Read);
                        if (restoring && files[fileIndex].Length != torrent.Files[fileIndex].FileSize)
                        {
                            filesMissing = true;
                        }
                        files[fileIndex].SetLength(fileInfo.FileSize);
                    }
                    catch (FileNotFoundException)
                    {
                        // we can't catch FileNotFound if we're creating files; if opening, then we're restoring
                        filesMissing = true;
                        // and try to create missing files then. If it fails, a new exception
                        // will be thrown no matter what
                        files[fileIndex] = File.Open(rootDir + path + ClearPath(fileInfo.FileName),
                            FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                        files[fileIndex].SetLength(fileInfo.FileSize);
                    }
                    catch
                    {
                        CleanUp(!restoring);
                        for (int i = 0; i < files.Length; i++)
                        {
                            if (files[i] != null)
                            {
                                files[i].Close();
                                File.Delete(files[i].Name);
                            }

                        }
                        throw;
                    }
                    fileIndex++;
                }
            }
        }

        private void CleanUp(bool delete)
        {
            infoFile?.Close();
            torrentCopy?.Close();
            if (delete)
            {
                try
                {
                    File.Delete(infoFileName);
                    File.Delete(torrentCopyName);
                }
                catch { }
            }
        }

        public void RemoveSession()
        {
            try
            {
                File.Delete(infoFileName);
                File.Delete(torrentCopyName);
            }
            catch
            { }
        }

        public void RemoveAllDownloads()
        {
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    File.Delete(files[i].Name);
                }
                catch { }
            }
        }

        private void CreateTorrentBackup()
        {
            torrent.EncodeTo(torrentCopy);
            torrentCopy.Close();
        }

        public static byte[][] LoadMainSession(out string[] fileNames)
        {
            fileNames = null;
            try
            {
                string[] session = File.ReadAllLines(sessionFileName, Encoding.UTF8);
                if (session.Length == 0)
                {
                    return null;
                }
                fileNames = session;
                return GetAllSessionData(session);
            }
            catch
            {
                return null;
            }
        }

        private static byte[][] GetAllSessionData(string[] sessions)
        {
            byte[][] result = new byte[sessions.Length][];
            for (int i = 0; i < sessions.Length; i++)
            {
                try
                {
                    result[i] = File.ReadAllBytes(sessions[i]);
                }
                catch
                {
                    result[i] = null;
                }
            }
            return result;
        }

        public bool SaveToDisk(PieceInfoNode entry)
        {
            byte[] hashResult;
            using (SHA1 hasher = new SHA1CryptoServiceProvider())
            {
                hashResult = hasher.ComputeHash(entry.pieceBuffer);
            }
            // check if HASH is OK
            if (!CompareHashes(hashResult, entry.pieceIndex * 20))
            {
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
            files[i - 1].Flush(true);
            // can get an "Out of range" if the next file doesn't exist. Need to watch out for this if peer sends wrong data!
            if (countToWrite < entry.pieceBuffer.Length)
            {
                files[i].Seek(0, SeekOrigin.Begin);
                files[i].Write(entry.pieceBuffer, countToWrite, entry.pieceBuffer.Length - countToWrite);
                files[i].Flush(true);
            }
            return true;
        }

        private bool CompareHashes(byte[] hash, int strOffset)
        {
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

        public async Task FlushAllAsync()
        {
            for (int i = 0; i < files.Length; i++)
            {
                await files[i].FlushAsync();
            }
        }

        public static void CreateMainSession()
        {
            mainSession = File.Open(sessionFileName, FileMode.Create, FileAccess.Write, FileShare.Read);
        }

        public void AddToMainSession()
        {
            var tempArr = Encoding.UTF8.GetBytes(infoFileName + "\r\n");
            mainSession.Write(tempArr, 0, tempArr.Length);
        }

        public static void CloseMainSession()
        {
            mainSession.Close();
        }

        public void SaveSession(BitArray pieces)
        {
            var dictionary = new BDictionary();
            string state = "";
            for (int i = 0; i < pieces.Count; i++)
            {
                state += pieces[i] ? "1" : "0";
            }
            dictionary.Add(Path.GetFileName(torrentCopyName), new BString(state));
            infoFile.SetLength(0);
            dictionary.EncodeTo(infoFile);
        }

        public void CloseSession()
        {
            infoFile.Close();
            for (int i = 0; i < files.Length; i++)
            {
                files[i].Close();
            }
        }

        public async Task<BitArray> CalculateSHA1()
        {
            var result = new BitArray(torrent.NumberOfPieces);
            await Task.Run(() =>
            {
                byte[] hashResult;
                byte[] readBuf = new byte[torrent.PieceSize];
                using (SHA1 hasher = new SHA1CryptoServiceProvider())
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        files[i].Seek(0, SeekOrigin.Begin);
                    }
                    int pieceIndex = 0;
                    int newFileOffset = 0;
                    for (int i = 0; i < files.Length; i++)
                    {
                        int totalReadFromFile = newFileOffset;
                        while (totalReadFromFile < files[i].Length)
                        {
                            newFileOffset = 0;
                            int readRes = files[i].Read(readBuf, 0, readBuf.Length);
                            totalReadFromFile += readRes;
                            // we read less data, but if the next file exists, we must read more
                            // to get the whole piece
                            if (readRes < readBuf.Length && i != files.Length - 1)
                            {
                                newFileOffset = files[i + 1].Read(readBuf, readRes, readBuf.Length - readRes);
                                readRes += newFileOffset;
                            }
                            hashResult = hasher.ComputeHash(readBuf, 0, readRes);
                            result[pieceIndex] = CompareHashes(hashResult, pieceIndex * 20);
                            pieceIndex++;
                        }
                    }
                }
            }).ConfigureAwait(false);
            return result;
        }

        public void LoadPieceFromDisk(int index)
        {

        }

        public static string ClearPath(string path)
        {
            return string.Join("_", path.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
