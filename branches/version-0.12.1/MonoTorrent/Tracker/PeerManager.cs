//
// PeerManager.cs
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
using System.Collections;
using System.Collections.Generic;

namespace MonoTorrent.Tracker
{
    ///<summar>This class is Responsible fro managing the Peers registered at the Tracker for a specific.
    ///Torrent.
    ///</summary>
    public class PeerManager 
    {
        List<Peer> peer_list;//same as _peers but no indexed. used for index based peer retrieval for randomization
        Dictionary<string, Peer> peer_dict;//hold the list of peers attatched to this torrent
        
        public PeerManager()
        {
            peer_list = new List<Peer>();
            peer_dict = new Dictionary<string, Peer>();
        }
        
        ///<summary>Returns the number of Peers registered.</summary>                
        public int Count {
            get {
                return peer_list.Count;
            }
        }
        
        ///</summary>Adds p to this Manager.</summary>
        public void Add(Peer peer)
        {            
            lock (this) {
                peer_dict.Add(peer.Key, peer);
                peer_list.Add(peer);
            }
        }
        
        ///</summary>Removes p from the Manager.</summary>
        public void Remove(Peer p)
        {
            Remove(p.Key);
        }
        
        ///</summary>Removes the Peer with key key from the Manager.</summary>
        public void Remove(string key)
        {
            lock (this) {
                Peer p = peer_dict[key];
                p.Stop();
                peer_dict.Remove(key);
                peer_list.Remove(p);
            }                
        }        
        
        ///<summary>Returns the Peer with key key</summary>
        public Peer Get(string key)
        {            
            return peer_dict[key];
        }
        
        ///<summary>Checks if this Manager conatains a Peer with key key.</summary>
        public bool Contains(string key)
        {            
            return peer_dict.ContainsKey(key);
        }
        
        ///<summary>Returns at most count random Peers which are registered at this
        ///Manager. 
        ///</summary>
        ///<param name=exclude>do not include this peer in the resulting list.</parm>
        ///<param name=count>return at most count peers</param>
        public List<Peer> GetRandomPeers(int count, Peer exclude)
        {
            List<Peer> randomPeers = new List<Peer>(count);
            if (peer_list.Count == 0) {
                return randomPeers;                
            }
            
            if (count >= peer_list.Count) {
                return GetAllPeers(exclude);    
            }
            
            for (int i = 0; i < count;) {
                Peer random = GetRandom();
                if (exclude != null && !exclude.Equals(random)) {
                    randomPeers.Add(random);
                    i++;
                }
            }
            
            return randomPeers;
        }
        
        ///<summary>return a random peer</summary>
        private Peer GetRandom()
        {
            Random rand = new Random();
            int next = rand.Next(peer_list.Count);
            return peer_list[next];
        }
        
        ///<summary>returns all peers from this Manager but not exlcude</summary>
        private List<Peer> GetAllPeers(Peer exclude)
        {
            List<Peer> randomPeers = new List<Peer>(peer_list.Count);
            foreach(Peer p in peer_list) {
                if (exclude != null && !exclude.Equals(p)) {
                    randomPeers.Add(p);
                }
            }
            return randomPeers;
        }
    }     
}
