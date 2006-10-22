//
// Tracker.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Net;
using System.Web;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;

using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{   
    public class Tracker
    {
        private Dictionary<string, ITorrentManager> torrents;
                
        ///<summary>
        ///starts the tracker
        ///</summary>
        private Tracker()
        {
            torrents = new Dictionary<string, ITorrentManager>();
            
        }
        
        ///<summary>Singleton returning an instance of the Tracker class.</summary>
        public static Tracker Instance
        {
            get {
                if (tracker == null)
                    tracker = new Tracker();
                return tracker;
            }
        }
        private static Tracker tracker = null;
        
        ///<summary>AllowNonCompact allows or denie requests in compact or 
        ///non compact format default is true</summary>
        ///
        public bool AllowNonCompact
        {
            get { 
                return allow_non_compact; 
            }
            set { 
                allow_non_compact = value; 
            }
        }       
        private bool allow_non_compact = true;
        
        ///<summary>Get and set the IntervalAlgorithm used by this Tracker</summary>
        public IIntervalAlgorithm IntervalAlgorithm
        {
            get {
                return interval_algorithm;
            }
            set {
                interval_algorithm = value;
            }
        }
        private IIntervalAlgorithm interval_algorithm = new StaticIntervalAlgorithm();
        
        ///<summary>
        ///Add a Torrent to this Tracker. After the Add was called this torrent is served to the Peers.
        ///
        ///</summary>
        ///<param name="torrent">
        ///The torrent which should be added. If it is already in the List the Method returns immidiatly.
        ///</param>
        public void AddTorrent(Torrent torrent)
        {                       
            //Console.WriteLine("adding torrent " + HttpUtility.UrlEncode(torrent.InfoHash) + " to the tracker"); 
            Console.WriteLine("adding torrent " + ToolBox.GetHex(torrent.InfoHash) + " to the tracker");
            
            if (torrents.ContainsKey(ToolBox.GetHex(torrent.InfoHash))) {
                Console.WriteLine("torrent already added");//TODO remove
                return;
            }            
            torrents.Add(ToolBox.GetHex(torrent.InfoHash), new SimpleTorrentManager(torrent));
            
        }
        
        ///<summary>This Method is used to Disable Torrents.
        ///</summary>
        ///<param>The Torrent to be removed from the Tracker</param>
        public void RemoveTorrent(Torrent torrent)
        {
            if (torrents.ContainsKey(ToolBox.GetHex(torrent.InfoHash)))
                torrents.Remove(ToolBox.GetHex(torrent.InfoHash));
        }
        
        ///<summary>This Method is called by the Frontend if a Peer called the announc URL
        ///</summary>
        public void Announce(AnnounceParameters par, Stream stream)
        {
            //some pre checks
            if (!torrents.ContainsKey(ToolBox.GetHex(par.infoHash))) {
                throw new TrackerException("Torrent not Registered at this Tracker");                
            }
            
            if (!AllowNonCompact && par.compact) {
                throw new TrackerException("Tracker does not allow Non Compact Format");
            }        
            
            ITorrentManager torrent = torrents[ToolBox.GetHex(par.infoHash)];                      
            
            switch (par.@event)
            {
                case TorrentEvent.Completed:
                    torrent.Update(par);                    
                    break;
                case TorrentEvent.Stopped:
                    torrent.Remove(par);
                    IntervalAlgorithm.PeerRemoved();
                    //Alan said do nothing, me agrees
                    //Debug.WriteLine("removed peer and do nothing");
                    //return;
                    break;
                case TorrentEvent.Started:
                    torrent.Add(par);
                    IntervalAlgorithm.PeerAdded();
                    break;
                case TorrentEvent.None:
                    torrent.Update(par);
                    break;
                default:
                    throw new TorrentException("unknown announce event");                    
            }
            
            //write response
            byte[] encData = GetAnnounceResponse(par).Encode();           
            stream.Write(encData, 0, encData.Length);
            
            WriteResult("announce", encData);
        }
        
        ///<summary>Handles Scrape requests</summary>
        ///
        public void Scrape(ScrapeParameters par, Stream stream)
        {                        
            byte[] encData = GetScrapeResponse(par).Encode();
            stream.Write(encData, 0, encData.Length);
            
            WriteResult("scrape", encData);
        }
        
        [Conditional("DEBUG")]
        private void WriteResult(string prefix, byte[] encData) 
        {
            string tmpPath = Path.GetTempFileName();
            using (FileStream tmpFile = new FileStream(tmpPath, FileMode.Open)) {
                tmpFile.Write(encData, 0, encData.Length);
                Debug.WriteLine(prefix +" return written to: " + tmpPath);
            }
        }
        
        ///<summary>writes the failure bencoded dict to the calling peer</summary>
        ///
        ///
        public void Failure(string reason, Stream stream)
        {
            BEncodedDictionary bencErrorDict = new BEncodedDictionary();
            bencErrorDict.Add("failure reason", new BEncodedString(reason));
            
            byte[] encData = bencErrorDict.Encode();  
            stream.Write(encData, 0, encData.Length);           
            //Console.WriteLine("failue contents: -->" + Encoding.ASCII.GetString(encData)+ "<--");              
            //TODO check if we need to close the writer; does it close the stream?; http 1.1 needs an open stream?
            WriteResult("error", encData);
        }
        
        ///<summary>
        ///This is called if we would like to Reset the Tracker and 
        ///clear all Torrents from the internal List of Torrents. This Method should only be called from 
        ///TrackeEngine see TrackerEngine.cs
        ///</summary>       
        internal void ClearTorrents()
        {           
            torrents.Clear();
        }
        
        private BEncodedDictionary GetAnnounceResponse(AnnounceParameters par)  
        {
            ITorrentManager torrentManager = torrents[ToolBox.GetHex(par.infoHash)];
            BEncodedDictionary dict = new BEncodedDictionary();
            
            Debug.WriteLine(torrentManager.Count);
            
            dict.Add("complete", new BEncodedNumber(torrentManager.CountComplete));
            dict.Add("incomplete", new BEncodedNumber(torrentManager.Count - torrentManager.CountComplete));
            dict.Add("interval", new BEncodedNumber((int)IntervalAlgorithm.Interval));

            dict.Add("peers", torrentManager.GetPeersList(par));
            
			dict.Add("min interval", new BEncodedNumber((int)IntervalAlgorithm.MinInterval));

			if (par.trackerId == null)//FIXME is this the right behaivour 
				par.trackerId = "monotorrent-tracker";
			dict.Add("tracker id", new BEncodedString(par.trackerId));            

            return dict;
        }
        
        private BEncodedDictionary GetScrapeResponse(ScrapeParameters par)
        {
            Console.WriteLine("GetScrapeResponse number of infohashes " + par.Count);
            BEncodedDictionary dict = new BEncodedDictionary();
            BEncodedDictionary filesDict = new BEncodedDictionary();
            
            if (par.Count == 1) {                
                //uniscrape
                ITorrentManager torrent = torrents[ToolBox.GetHex(par.InfoHash)];
                //string infoHashString = ToolBox.SingleByteString(torrent.Torrent.InfoHash);
                filesDict.Add(torrent.Torrent.InfoHash, torrent.GetScrapeEntry());
            } 
            if (par.Count == 0) {
                //fullscrape
                foreach (ITorrentManager torrent in torrents.Values) {
                    //string infoHashString = ToolBox.SingleByteString(torrent.Torrent.InfoHash);
                    filesDict.Add(torrent.Torrent.InfoHash, torrent.GetScrapeEntry());
                }
                
            }
            
            if (par.Count > 1) {
                //multiscrape
                foreach (byte[] infoHash in par) {
                    ITorrentManager torrent = torrents[ToolBox.GetHex(infoHash)];
                    //string infoHashString = ToolBox.SingleByteString(torrent.Torrent.InfoHash);
                    filesDict.Add(new BEncodedString(torrent.Torrent.InfoHash), torrent.GetScrapeEntry());
                }
            }
            dict.Add("files", filesDict);
            return dict;
        }
    }
    
    public class TrackerException : Exception
    {
        internal TrackerException(string message) : base(message)
        {
        }
    }
}
