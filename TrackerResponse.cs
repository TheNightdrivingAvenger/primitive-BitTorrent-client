﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

using System.Net.Http;
using System.Web;
using BencodeNET.Parsing;
using BencodeNET.Objects;
using BencodeNET.Torrents;

namespace CourseWork
{
    class TrackerResponse
    {

        public BDictionary response { get; private set; }

        public async Task GetTrackerResponse(Torrent torrent, long downloaded, long totalSize, string peerID, int myPort,
            string eventStr, string trackerID)
        {
            // do something with different trackers
            string baseURI = torrent.Trackers.ElementAt(0).ElementAt(0);
            //foreach (var URIList in torrent.Trackers)
            //{
            //    foreach (var URI in URIList)
            //    {
            //        //getting tracker's URIs
            //    }
            //}
            string URIFormedHash = URIEncode(torrent.OriginalInfoHashBytes);

            string requestURI = baseURI + '?' + "info_hash=" + URIFormedHash +
                "&peer_id=" + peerID + "&port=" + myPort.ToString() +
                "&uploaded=" + 0 + "&downloaded=" + downloaded + "&left=" + (totalSize - downloaded) +
                "&event=started" + "&numwant=50" + "&compact=1" + "&no_peer_id=1" +
                (eventStr == null ? "" : "&event=" + eventStr) + (trackerID == null ? "" : "&trackerid=" + trackerID);

            byte[] response;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Connection", "Close");
                // user-agent header param was needed for some reason!
                client.DefaultRequestHeaders.Add("User-Agent", "myTorrent");
                // watch out for HTTPRequestException!
                // бывают исключения при отправке запроса!.. Inner WebException -- не получиолсь разрешить DNS
                response = await client.GetByteArrayAsync(requestURI).ConfigureAwait(false); ;
            }
            // watch out for duplicate keys (will throw an exception) here (various things can happen...)
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
