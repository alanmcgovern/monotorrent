using System;
using System.Net;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Tracker
{
    /// <summary>This class holds informations about Peers downloading Files</summary>
    public class Peer : IEquatable<Peer>
    {
        internal Peer(AnnounceParameters par, object dictionaryKey)
        {
            DictionaryKey = dictionaryKey;
            Update(par);
        }


        /// <summary>
        ///     The IPEndpoint at which the client is listening for connections at
        /// </summary>
        public IPEndPoint ClientAddress { get; private set; }

        /// <summary>
        ///     A byte[] containing the peer's IPEndpoint in compact form
        /// </summary>
        internal byte[] CompactEntry
        {
            get { return GenerateCompactPeersEntry(); }
        }

        internal object DictionaryKey { get; }

        /// <summary>
        ///     The amount of data (in bytes) which the peer has downloaded this session
        /// </summary>
        public long Downloaded { get; private set; }

        /// <summary>
        ///     The estimated download speed of the peer in bytes/second
        /// </summary>
        public int DownloadSpeed { get; private set; }

        /// <summary>
        ///     True if the peer has completed the torrent
        /// </summary>
        public bool HasCompleted
        {
            get { return Remaining == 0; }
        }

        /// <summary>
        ///     The time when the peer last announced at
        /// </summary>
        public DateTime LastAnnounceTime { get; private set; }

        /// <summary>The peer entry in non compact format.</summary>
        internal BEncodedDictionary NonCompactEntry
        {
            get { return GeneratePeersEntry(); }
        }

        /// <summary>
        ///     The Id of the client software
        /// </summary>
        public string PeerId { get; private set; }

        /// <summary>
        ///     The amount of data (in bytes) which the peer has to download to complete the torrent
        /// </summary>
        public long Remaining { get; private set; }

        /// <summary>
        ///     The amount of data the peer has uploaded this session
        /// </summary>
        public long Uploaded { get; private set; }

        /// <summary>
        ///     The estimated upload speed of the peer in bytes/second
        /// </summary>
        public int UploadSpeed { get; private set; }

        public bool Equals(Peer other)
        {
            if (other == null)
                return false;
            return DictionaryKey.Equals(other.DictionaryKey);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Peer);
        }

        public override int GetHashCode()
        {
            return DictionaryKey.GetHashCode();
        }

        internal void Update(AnnounceParameters parameters)
        {
            var now = DateTime.Now;
            var elapsedTime = (now - LastAnnounceTime).TotalSeconds;
            if (elapsedTime < 1)
                elapsedTime = 1;

            ClientAddress = parameters.ClientAddress;
            DownloadSpeed = (int) ((parameters.Downloaded - Downloaded)/elapsedTime);
            UploadSpeed = (int) ((parameters.Uploaded - Uploaded)/elapsedTime);
            Downloaded = parameters.Downloaded;
            Uploaded = parameters.Uploaded;
            Remaining = parameters.Left;
            PeerId = parameters.PeerId;
            LastAnnounceTime = now;
        }


        private BEncodedDictionary GeneratePeersEntry()
        {
            var encPeerId = new BEncodedString(PeerId);
            var encAddress = new BEncodedString(ClientAddress.Address.ToString());
            var encPort = new BEncodedNumber(ClientAddress.Port);

            var dictionary = new BEncodedDictionary();
            dictionary.Add(Tracker.PeerIdKey, encPeerId);
            dictionary.Add(Tracker.Ip, encAddress);
            dictionary.Add(Tracker.Port, encPort);
            return dictionary;
        }

        private byte[] GenerateCompactPeersEntry()
        {
            var port = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short) ClientAddress.Port));
            var addr = ClientAddress.Address.GetAddressBytes();
            var entry = new byte[addr.Length + port.Length];

            Array.Copy(addr, entry, addr.Length);
            Array.Copy(port, 0, entry, addr.Length, port.Length);
            return entry;
        }
    }
}