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
using System.Collections.Generic;
using System.Linq;

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

        IList<IPeerConnectionListener> Listeners { get; set; }

        IPeerConnectionGate ConnectionGate { get; set; }

        InfoHash[] SKeys { get; set; }

        internal ListenManager (ClientEngine engine, IPeerConnectionGate connectionGate)
        {
            Engine = engine ?? throw new ArgumentNullException (nameof (engine));
            ConnectionGate = connectionGate ?? throw new ArgumentNullException (nameof (connectionGate));
            Listeners = Array.Empty<IPeerConnectionListener> ();
            SKeys = Array.Empty<InfoHash> ();
        }

        public void Add (InfoHashes skey)
        {
            var clone = new List<InfoHash> (SKeys);
            if (skey.V1 != null)
                clone.Add (skey.V1);
            if (skey.V2 != null)
                clone.Add (skey.V2.Truncate ());
            SKeys = clone.ToArray ();
        }

        public void Remove (InfoHashes skey)
        {
            var clone = new List<InfoHash> (SKeys);
            if (skey.V1 != null)
                clone.Remove (skey.V1);
            if (skey.V2 != null)
                clone.Remove (skey.V2.Truncate ());
            SKeys = clone.ToArray ();
        }

        public void SetListeners (IList<IPeerConnectionListener> listeners)
        {
            foreach (var v in Listeners)
                v.ConnectionReceived -= ConnectionReceived;
            Listeners = Array.AsReadOnly (listeners.ToArray ());
            foreach (var v in Listeners)
                v.ConnectionReceived += ConnectionReceived;
        }

        async void ConnectionReceived (object? sender, PeerConnectionEventArgs e)
        {
            await ClientEngine.MainLoop;
            var peerInfo = new PeerInfo (e.Connection.Uri);
            try {
                if (Engine.ConnectionManager.ShouldBanPeer (peerInfo)) {
                    e.Connection.Dispose ();
                    return;
                }
                if (!e.Connection.IsIncoming) {
                    var peer = new Peer (peerInfo, e.InfoHash!);
                    var manager = Engine.Torrents.FirstOrDefault (t => t.InfoHashes.Contains (e.InfoHash!))!;
                    var id = new PeerId (peer, e.Connection, new BitField (manager.Bitfield.Length).SetAll (false));
                    id.LastMessageSent.Restart ();
                    id.LastMessageReceived.Restart ();

                    Engine.ConnectionManager.ProcessNewOutgoingConnection (manager, id);
                    return;
                }

                logger.Info (e.Connection, "ConnectionReceived");

                var supportedEncryptions = Engine.Settings.AllowedEncryption;
                EncryptorFactory.EncryptorResult result = await EncryptorFactory.CheckIncomingConnectionAsync (e.Connection, supportedEncryptions, SKeys, Engine.Factories);
                if (!await HandleHandshake (peerInfo, e.Connection, result.Handshake!, result.Decryptor, result.Encryptor))
                    e.Connection.Dispose ();
            } catch {
                e.Connection.Dispose ();
            }
        }

        async ReusableTask<bool> HandleHandshake (PeerInfo peerInfo, IPeerConnection connection, HandshakeMessage message, IEncryption decryptor, IEncryption encryptor)
        {
            TorrentManager? man = null;
            if (message.ProtocolString != Constants.ProtocolStringV100)
                return false;

            // If we're forcing encrypted connections and this is in plain-text, close it!
            if (encryptor is PlainTextEncryption && !Engine.Settings.AllowedEncryption.Contains (EncryptionType.PlainText))
                return false;

            for (int i = 0; i < Engine.Torrents.Count; i++)
                if (Engine.Torrents[i].InfoHashes.Contains (message.InfoHash))
                    man = Engine.Torrents[i];

            // We're not hosting that torrent
            if (man == null)
                return false;

            if (man.State == TorrentState.Stopped)
                return false;

            if (!man.Mode.CanAcceptConnections)
                return false;

            peerInfo = peerInfo.WithPeerId (message.PeerId);
            if (!await ConnectionGate.TryAcceptHandshakeAsync (Engine.PeerId, peerInfo, connection, man.InfoHashes.V1OrV2)) {
                logger.InfoFormatted (connection, "[incoming] Handshake with peer_id '{0}' rejected by the connection gate", message.PeerId);
                return false;
            }

            var peer = new Peer (peerInfo, man.InfoHashes.Expand (message.InfoHash));
            peer.UpdatePeerId (message.PeerId);

            logger.InfoFormatted (connection, "[incoming] Received handshake with peer_id '{0}'", message.PeerId);

            var id = new PeerId (peer, connection, new BitField (man.Bitfield.Length).SetAll (false)) {
                Decryptor = decryptor,
                Encryptor = encryptor
            };

            man.Mode.HandleMessage (id, message, default);
            logger.Info (id.Connection, "Handshake successful handled");

            id.ClientApp = new Software (message.PeerId);

            return await Engine.ConnectionManager.IncomingConnectionAcceptedAsync (man, id);
        }
    }
}
