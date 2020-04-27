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
using System.Threading.Tasks;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Listeners;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    class ListenManager : IDisposable
    {
        ClientEngine Engine { get; set; }
        List<IPeerListener> Listeners { get; }
        InfoHash[] SKeys { get; set; }

        internal ListenManager (ClientEngine engine)
        {
            Engine = engine ?? throw new ArgumentNullException (nameof (engine));
            Listeners = new List<IPeerListener> ();
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

        public void Dispose ()
        {
            foreach (IPeerListener listener in Listeners.ToArray ())
                Unregister (listener);
            Listeners.Clear ();
        }

        public void Register (IPeerListener listener)
        {
            Listeners.Add (listener);
            listener.ConnectionReceived += ConnectionReceived;
        }

        public void Unregister (IPeerListener listener)
        {
            listener.ConnectionReceived -= ConnectionReceived;
            Listeners.Remove (listener);
        }

        async void ConnectionReceived (object sender, NewConnectionEventArgs e)
        {
            await ClientEngine.MainLoop;
            try {
                if (Engine.ConnectionManager.ShouldBanPeer (e.Peer)) {
                    e.Connection.Dispose ();
                    return;
                }

                if (!e.Connection.IsIncoming) {
                    var id = new PeerId (e.Peer, e.Connection, e.TorrentManager.Bitfield?.Clone ().SetAll (false));
                    id.LastMessageSent.Restart ();
                    id.LastMessageReceived.Restart ();

                    Engine.ConnectionManager.ProcessNewOutgoingConnection (e.TorrentManager, id);
                    return;
                }

                Logger.Log (e.Connection, "ListenManager - ConnectionReceived");

                IConnection2 connection = ConnectionConverter.Convert (e.Connection);
                EncryptorFactory.EncryptorResult result = await EncryptorFactory.CheckIncomingConnectionAsync (connection, e.Peer.AllowedEncryption, Engine.Settings, SKeys);
                if (!await HandleHandshake (e.Peer, connection, result.Handshake, result.Decryptor, result.Encryptor))
                    connection.Dispose ();
            } catch {
                e.Connection.Dispose ();
            }
        }

        async Task<bool> HandleHandshake (Peer peer, IConnection connection, HandshakeMessage message, IEncryption decryptor, IEncryption encryptor)
        {
            TorrentManager man = null;
            if (message.ProtocolString != VersionInfo.ProtocolStringV100)
                return false;

            // If we're forcing encrypted connections and this is in plain-text, close it!
            if (encryptor is PlainTextEncryption && !Engine.Settings.AllowedEncryption.HasFlag (EncryptionTypes.PlainText))
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
            var id = new PeerId (peer, connection, man.Bitfield?.Clone ().SetAll (false)) {
                Decryptor = decryptor,
                Encryptor = encryptor
            };

            message.Handle (man, id);
            Logger.Log (id.Connection, "ListenManager - Handshake successful handled");

            id.ClientApp = new Software (message.PeerId);

            message = new HandshakeMessage (man.InfoHash, Engine.PeerId, VersionInfo.ProtocolStringV100);
            await PeerIO.SendMessageAsync (id.Connection, id.Encryptor, message, man.UploadLimiters, id.Monitor, man.Monitor);
            Engine.ConnectionManager.IncomingConnectionAccepted (man, id);
            return true;
        }
    }
}
