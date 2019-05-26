using System.Linq;
using System.Threading.Tasks;

using System.Net.Http;
using BencodeNET.Parsing;
using BencodeNET.Objects;

namespace CourseWork
{
    class TrackerResponse
    {

        public BDictionary response { get; private set; }

        public async Task GetTrackerResponse(DownloadingFile downloadingFile, string peerID, int myPort,
            string eventStr, int numWant)
        {
            string baseURI = downloadingFile.torrentContents.Trackers.ElementAt(0).ElementAt(0);
            string URIFormedHash = URIEncode(downloadingFile.torrentContents.OriginalInfoHashBytes);

            string requestURI = baseURI + '?' + "info_hash=" + URIFormedHash +
                "&peer_id=" + peerID + "&port=" + myPort.ToString() +
                "&uploaded=" + 0 + "&downloaded=" + downloadingFile.downloaded + "&left=" + (downloadingFile.totalSize -
                downloadingFile.downloaded) + "&numwant=" + numWant +
                "&compact=1" + "&no_peer_id=1" + (eventStr == null ? "" : "&event=" + eventStr) +
                (downloadingFile.trackerID == null ? "" : "&trackerid=" + downloadingFile.trackerID);

            byte[] response;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Connection", "Close");
                client.DefaultRequestHeaders.Add("User-Agent", "VST0001");
                response = await client.GetByteArrayAsync(requestURI).ConfigureAwait(false); ;
            }
            var parser = new BencodeParser();
            this.response = parser.Parse<BDictionary>(response);
        }

        private string URIEncode(byte[] array)
        {
            const int digitStart = 0x30, digitEnd = 0x39, letterStart = 0x41, letterEnd = 0x5a,
                smallLetterStart = 0x61, smallLetterEnd = 0x7a, underscore = 0x5f, minus = 0x2d, dot = 0x2e,
                tilde = 0x7e;

            string encoded = null;

            for (int i = 0; i < array.Length; i++)
            {
                if (((array[i] >= digitStart) && (array[i] <= digitEnd)) || ((array[i] >= letterStart) && (array[i] <= letterEnd))
                    || ((array[i] >= smallLetterStart) && (array[i] <= smallLetterEnd)) || (array[i] == underscore)
                    || (array[i] == minus) || (array[i] == dot) || (array[i] == tilde))
                {
                    encoded += (char)array[i];
                } else
                {                  
                    encoded += "%" + array[i].ToString("x2");
                }
            }
            return encoded;
        }
    }
}
