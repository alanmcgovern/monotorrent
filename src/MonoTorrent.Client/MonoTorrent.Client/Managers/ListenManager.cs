//
// ListenManager.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Linq;

using MonoTorrent.Client.Listeners;
using MonoTorrent.Connections;
using MonoTorrent.Connections.Peer;
using MonoTorrent.Connections.Peer.Encryption;
using MonoTorrent.Logging;
using MonoTorrent.Messages.Peer;

using ReusableTasks;

namespace MonoTorrent.Client
{
    class ListenManager
    {
        static readonly Logger logger = Logger.Create (nameof (ListenManager));

        ClientEngine Engine { get; set; }

        IPeerConnectionListener Listener { get; set; }

        InfoHash[] SKeys { get; set; }

        internal ListenManager (ClientEngine engine)
        {
            Engine = engine ?? throw new ArgumentNullException (nameof (engine));
            Listener = new NullPeerListener ();
            SKeys = Array.Empty<InfoHash> ();
        }

        public void Add (InfoHash skey)
        {
            var clone = new InfoHash[SKeys.Length + 1];
            Array.Copy (SKeys, clone, SKeys.Length);
            clone[clone.Length - 1] = skey;
            SKeys = clone;
        }

        public void Remove (InfoHash skey)
        {
            var clone = new InfoHash[SKeys.Length - 1];
            var index = Array.IndexOf (SKeys, skey);
            Array.Copy (SKeys, clone, index);
            Array.Copy (SKeys, index + 1, clone, index, clone.Length - index);
            SKeys = clone;
        }

        public void SetListener (IPeerConnectionListener listener)
        {
            Listener.ConnectionReceived -= ConnectionReceived;
            Listener = listener ?? new NullPeerListener ();
            Listener.ConnectionReceived += ConnectionReceived;
        }

        async void ConnectionReceived (object sender, PeerConnectionEventArgs e)
        {
            await ClientEngine.MainLoop;
            var peer = new Peer ("", e.Connection.Uri, EncryptionTypes.All);

            try {
                if (Engine.ConnectionManager.ShouldBanPeer (peer)) {
                    e.Connection.Dispose ();
                    return;
                }
                if (!e.Connection.IsIncoming) {
                    var manager = Engine.Torrents.FirstOrDefault (t => t.InfoHash == e.InfoHash);
                    var id = new PeerId (peer, e.Connection, new MutableBitField (manager.Bitfield.Length).SetAll (false));
                    id.LastMessageSent.Restart ();
                    id.LastMessageReceived.Restart ();

                    Engine.ConnectionManager.ProcessNewOutgoingConnection (manager, id);
                    return;
                }

                logger.Info (e.Connection, "ConnectionReceived");

                var supportedEncryptions = EncryptionTypes.GetSupportedEncryption (peer.AllowedEncryption, Engine.Settings.AllowedEncryption);
                EncryptorFactory.EncryptorResult result = await EncryptorFactory.CheckIncomingConnectionAsync (e.Connection, supportedEncryptions, SKeys, Engine.Factories);
                if (!await HandleHandshake (peer, e.Connection, result.Handshake, result.Decryptor, result.Encryptor))
                    e.Connection.Dispose ();
            } catch {
                e.Connection.Dispose ();
            }
        }

        async ReusableTask<bool> HandleHandshake (Peer peer, IPeerConnection connection, HandshakeMessage message, IEncryption decryptor, IEncryption encryptor)
        {
            TorrentManager man = null;
            if (message.ProtocolString != Constants.ProtocolStringV100)
                return false;

            // If we're forcing encrypted connections and this is in plain-text, close it!
            if (encryptor is PlainTextEncryption && !Engine.Settings.AllowedEncryption.Contains (EncryptionType.PlainText))
                return false;

            for (int i = 0; i < Engine.Torrents.Count; i++)
                if (message.InfoHash == Engine.Torrents[i].InfoHash)
                    man = Engine.Torrents[i];

            // We're not hosting that torrent
            if (man == null)
                return false;

            if (man.State == TorrentState.Stopped)
                return false;

            if (!man.Mode.CanAcceptConnections)
                return false;

            peer.PeerId = message.PeerId;
            var id = new PeerId (peer, connection, new MutableBitField (man.Bitfield.Length).SetAll (false)) {
                Decryptor = decryptor,
                Encryptor = encryptor
            };

            man.Mode.HandleMessage (id, message);
            logger.Info (id.Connection, "Handshake successful handled");

            id.ClientApp = new Software (message.PeerId);

            return await Engine.ConnectionManager.IncomingConnectionAcceptedAsync (man, id);
        }
    }
}
