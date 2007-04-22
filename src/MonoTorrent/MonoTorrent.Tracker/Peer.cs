//
// Peer.cs
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
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using MonoTorrent.Common;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Tracker
{
    ///<summary>This class holds informations about Peers downloading Files</summary>
    public class Peer
    {
        private Timer timer;
        private AnnounceParameters parameters;
        
        internal Peer(AnnounceParameters par, TimerCallback removePeerCallback)       
        {                        
            long timeout = Tracker.Instance.IntervalAlgorithm.PeerTimeout;
            timer = new Timer(new TimerCallback(removePeerCallback), this, timeout, timeout);
            Debug.WriteLine("new peer: " + par.ip + ":" + par.port);
            Update(par);
        }
        
        ///<summary>The ip Address of the Peer</summary>        
        public string Address
        {
            get {
                return parameters.ip;
            }
        }       
        
        ///<summary>The tcp port the Peer is listening on</summary>
        public ushort Port
        {
            get {
                return parameters.port;
            }
        }       
        
        ///<summary>The Id of the client software</summary>
        public string PeerId
        {
            get { 
                return parameters.peerId;
            }
        }
        
        ///<summary>Calculates a key to identify the Peer. Used as Index in Dictionaries</summary>
        public string Key
        {
            get {
                return Peer.GetKey(parameters);                
            }
        }
        
        ///<summary>Calculates a key to identify the Peer. Used as Index in Dictionaries</summary>
        ///<param name=par>the peer represented by the parameters</param>
        public static string GetKey(AnnounceParameters par)
        {
            
            if (par.key != null) {
                if (!par.key.Equals(string.Empty))
                    return par.key;
            }
            
            return MonoTorrent.Common.ToolBox.GetHex(GenerateCompactPeersEntry(par.ip, par.port));
        }
        
        ///<summary>
        ///calculates the 6 byte ip + port compact compo 
        ///</summary>
        public byte[] CompactPeersEntry
        {
            get {
                return compact_peers_entry;
            }
        }
        private byte[] compact_peers_entry;
        
        ///<summary>returns wheather the Peer has finished downloading.</summary>
        public bool IsCompleted
        {
            get {
                return completed;
            }
            set {
                completed = value;
            }
        }
        private bool completed;
        
        ///<summary>The peer entry in non compact format.</summary> 
        public BEncodedDictionary PeersEntry
        {
            get {
                return peers_entry;
            }
        }
        private BEncodedDictionary peers_entry;
        
        ///<summary>The Peer entry in compact format.</summary>
        public static byte[] GenerateCompactPeersEntry(string address, ushort sport)
        {
            if (address == null)
                throw new ArgumentNullException("address");
            
            if (sport == 0)
                throw new ArgumentException("sport");
                            
            byte[] port = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)sport));
            byte[] addr = IPAddress.Parse(address).GetAddressBytes();
            byte[] entry = new byte[addr.Length + port.Length];
            Debug.Assert(entry.Length == 6, "This should be 6 bytes");
            
            PrintArray(port);
            PrintArray(addr);
            
            Array.Copy(addr, entry, addr.Length);
            Array.Copy(port, 0, entry, addr.Length, port.Length);
            
            PrintArray(entry);
            return entry;
        }
        
        [Conditional("DEBUG")]
        private static void PrintArray(byte[] barray)
        {
            foreach (byte b in barray) {
                Console.Write(b.ToString("X") + ",");
            }
            Console.WriteLine();
        }
        
        ///<summary>Update internal datas and reset Timers</summary>
        public void Update(AnnounceParameters par)
        {
            AnnounceParameters old = parameters;
            parameters = par;
            completed = par.@event == TorrentEvent.Completed;
           
            //TODO: do not generate every time
            GenerateCompactPeersEntry();
            GeneratePeersEntry();                   
        
            ResetTimer();            
        }
        
        public void Stop()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);//stop the timer
        }
                
        private void GeneratePeersEntry()
        {
            BEncodedString encPeerId = new BEncodedString(PeerId);
            BEncodedString encAddress = new BEncodedString(Address);
            BEncodedNumber encPort = new BEncodedNumber(Port);

            BEncodedDictionary dictionary = new BEncodedDictionary();
            dictionary.Add("peer id", encPeerId);
            dictionary.Add("ip", encAddress);
            dictionary.Add("port", encPort);
            
            peers_entry = dictionary;
        }
        
        private void GenerateCompactPeersEntry()
        {           
            compact_peers_entry = GenerateCompactPeersEntry(Address, Port);
        }
        
        
        ///<summary>This Method resets the Timer. The Timer needs to be Reseted when we get an announce from a peer
        ///</summary>
        private void ResetTimer()
        {
            Debug.WriteLine("updating the timer");
            if (timer == null)
                return;
            long timeout = Tracker.Instance.IntervalAlgorithm.PeerTimeout;
            timer.Change(timeout, timeout);          
        }
    }
    
}
