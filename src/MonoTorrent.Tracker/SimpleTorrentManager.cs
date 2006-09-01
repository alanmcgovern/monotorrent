//
// SimpleTorrentManager.cs
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
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using MonoTorrent.Common;


namespace MonoTorrent.Tracker
{
    ///<summary>
    ///This class is a TorrentManager which uses .Net Generics datastructures, such 
    ///as Dictionary and List to manage Peers from a Torrent.
    ///</summary>
    public class SimpleTorrentManager : ITorrentManager
    {
        PeerManager peers;
        
        public SimpleTorrentManager(ITorrent torrent)
        {
            this.torrent = torrent;
            peers = new PeerManager();
        }
        
        public ITorrent Torrent
        {
            get {
                return torrent;
            }
        }
        private ITorrent torrent;
        
        public int Count
        {
            get {
                return peers.Count;
            }
        }
        
        public int CountComplete
        {
            get {
                return complete;
            }
        }
        private int complete = 0;
        
        public int Downloaded
        {
            get {
                return downloaded;
            }
        }
        private int downloaded;
                
        
        public void Add(AnnounceParameters par)
        {
            string key = Peer.GetKey(par);
            Debug.WriteLine("adding peer: " + par.ip + ":" + par.port);            
            
            if (peers.Contains(key)) {
                Debug.WriteLine("peer already in there. maybe the client restarted?");
                peers.Remove(key);
            }

            Peer peer = new Peer(par, new System.Threading.TimerCallback(PeerTimeout));
            
            peers.Add(peer);
            
            if (peer.IsCompleted) {
                System.Threading.Interlocked.Increment(ref complete);
                System.Threading.Interlocked.Increment(ref downloaded);
            }
        }
        
        public void Remove(Peer peer)
        {
            if (peer.IsCompleted)
                System.Threading.Interlocked.Decrement(ref complete);
            
            peers.Remove(peer);                       
        }
        
        public void Remove(AnnounceParameters par)
        {    
            string key = Peer.GetKey(par);
            Debug.WriteLine("removing: |" + key +"|");           
            
            peers.Remove(key);
            //Remove(peers.Get(key));
        }
        
        public void Update(AnnounceParameters par)
        {
            string key = Peer.GetKey(par);            
            Debug.WriteLine("updating peer: " + par.ip + ":" + par.port);
            if (!peers.Contains(key)) {
                Add(par);
                Console.Error.WriteLine("warning: Peer not managed. If you restarted the Tracker ignore this message"); 
                return;
            }
            Peer peer = peers.Get(key);
            
            if (par.@event.Equals(TorrentEvent.Completed)) {
                System.Threading.Interlocked.Increment(ref complete);
                System.Threading.Interlocked.Increment(ref downloaded);
            }                
            
            peer.Update(par);
        }
        
        public IBEncodedValue GetPeersList(AnnounceParameters par)
        {
            if (par.compact) {
                return GetCompactList(par);
            } else {
                return GetNonCompactList(par);
            }            
        }
        
        //TODO refactor - done not debuged
        private IBEncodedValue GetCompactList(AnnounceParameters par)
        {            
            Peer exclude = null;
            if (peers.Contains(Peer.GetKey(par))) {
                exclude =  peers.Get(Peer.GetKey(par));
            }
            IList<Peer> randomPeers = peers.GetRandomPeers(par.numberWanted, exclude);
            byte[] peersBuffer = new byte[randomPeers.Count * 6];
            int offset = 0;
            Debug.WriteLine("number of peers returned: " + randomPeers.Count);
            foreach (Peer each in randomPeers) {
                byte[] entry = each.CompactPeersEntry;
                Array.Copy(entry, 0, peersBuffer, offset, entry.Length);
                offset += entry.Length;
            }
//            Debug.WriteLine("stream.length: "+stream.Length);
//            Debug.WriteLine("stream.buffer.length: "+stream.GetBuffer().Length);
//            Debug.Assert(stream.GetBuffer().Length == stream.Length);
            return new BEncodedString(peersBuffer);            
        }
        
        //TODO refactor: done - not debuged
        private IBEncodedValue GetNonCompactList(AnnounceParameters par)
        {
            Peer exclude = peers.Get(Peer.GetKey(par));            
            IList<Peer> randomPeers = peers.GetRandomPeers(par.numberWanted, exclude);            
            List<IBEncodedValue> announceList = new List<IBEncodedValue>(randomPeers.Count);
            
            foreach (Peer each in randomPeers) {
                announceList.Add(each.PeersEntry);
            }
            
            return new BEncodedList(announceList);
        }
        
        public BEncodedDictionary GetScrapeEntry()
        {
            BEncodedDictionary dict = new BEncodedDictionary();
            
            dict.Add("complete", new BEncodedNumber(CountComplete));
            dict.Add("downloaded", new BEncodedNumber(Downloaded));
            dict.Add("incomplete", new BEncodedNumber(Count - CountComplete));
            if (!torrent.Equals(String.Empty))
                dict.Add("name", new BEncodedString(torrent.Name));
            
            return dict;
        }
        
        //this is the handle from the peer timer. it is called when the peer is not responding anymore.
        //if this is the case we remove em to save memory. this is neccesary if peers shut down but do 
        //not send the finished update. 
        private void PeerTimeout(object peer)
        {
            Debug.WriteLine("peer is not updating anymore");
            Peer p = peer as Peer;
            
            if (p == null) {
                throw new ArgumentException("not a Peer instance", "peer"); 
            }
            
            Remove(p);
        }        
    }    
}
