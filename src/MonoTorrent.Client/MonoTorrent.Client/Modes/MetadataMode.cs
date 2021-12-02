//
// MetadataMode.cs
//
// Authors:
//   Olivier Dufour olivier.duff@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
// Copyright (C) 2009 Olivier Dufour
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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Logging;
using MonoTorrent.Messages;
using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.FastPeer;
using MonoTorrent.Messages.Peer.Libtorrent;

namespace MonoTorrent.Client.Modes
{
    class MetadataMode : Mode
    {
        static readonly Logger logger = Logger.Create (nameof (MetadataMode));

        MutableBitField bitField;
        static readonly TimeSpan timeout = TimeSpan.FromSeconds (10);
        PeerId currentId;
        string savePath;
        DateTime requestTimeout;
        bool stopWhenDone;

        bool HasAnnounced { get; set; }
        internal MemoryStream Stream { get; set; }

        public override bool CanHashCheck => true;
        public override TorrentState State => TorrentState.Metadata;

        public MetadataMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings, string savePath)
            : this (manager, diskManager, connectionManager, settings, savePath, false)
        {

        }

        public MetadataMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings, string savePath, bool stopWhenDone)
            : base (manager, diskManager, connectionManager, settings)
        {
            this.savePath = savePath;
            this.stopWhenDone = stopWhenDone;
        }

        public override void Tick (int counter)
        {
            if (!HasAnnounced) {
                HasAnnounced = true;
                SendAnnounces ();
            }

            //if one request have been sent and we have wait more than timeout
            // request the next peer
            if (requestTimeout < DateTime.Now) {
                NextPeer ();

                if (currentId != null && Stream != null) {
                    RequestNextNeededPiece (currentId);
                }
            }

        }

        async void SendAnnounces ()
        {
            try {
                Manager.DhtAnnounce ();
                await Task.WhenAll (
                    Manager.TrackerManager.AnnounceAsync (CancellationToken.None).AsTask (),
                    Manager.LocalPeerAnnounceAsync ()
                );
            } catch {
                // Nothing.
            }
        }

        void NextPeer ()
        {
            bool flag = false;

            foreach (PeerId id in Manager.Peers.ConnectedPeers) {
                if (id.SupportsLTMessages && id.ExtensionSupports.Supports (LTMetadata.Support.Name)) {
                    if (id == currentId)
                        flag = true;
                    else if (flag) {
                        currentId = id;
                        return;
                    }
                }
            }
            //second pass without removing the currentid and previous ones
            foreach (PeerId id in Manager.Peers.ConnectedPeers) {
                if (id.SupportsLTMessages && id.ExtensionSupports.Supports (LTMetadata.Support.Name)) {
                    currentId = id;
                    return;
                }
            }
            currentId = null;
            return;
        }

        protected override void HandleLtMetadataMessage (PeerId id, LTMetadata message)
        {
            base.HandleLtMetadataMessage (id, message);

            switch (message.MetadataMessageType) {
                case LTMetadata.MessageType.Data:
                    // If we've already received everything successfully, do nothing!
                    if (bitField.AllTrue)
                        return;

                    if (Stream == null)
                        throw new Exception ("Need extention handshake before ut_metadata message.");

                    Stream.Seek (message.Piece * LTMetadata.BlockSize, SeekOrigin.Begin);
                    Stream.Write (message.MetadataPiece, 0, message.MetadataPiece.Length);
                    bitField[message.Piece] = true;
                    if (bitField.AllTrue) {
                        InfoHash hash;
                        Stream.Position = 0;
                        using (SHA1 hasher = Manager.Engine.Factories.CreateSHA1 ())
                            hash = InfoHash.FromMemory (hasher.ComputeHash (Stream));

                        if (Manager.InfoHash != hash) {
                            bitField.SetAll (false);
                        } else {
                            Stream.Position = 0;
                            BEncodedDictionary dict = new BEncodedDictionary ();
                            dict.Add ("info", BEncodedValue.Decode (Stream));

                            if (Manager.TrackerManager.Tiers != null && Manager.TrackerManager.Tiers.Count > 0) {
                                BEncodedList announceTrackers = new BEncodedList ();
                                foreach (var tier in Manager.TrackerManager.Tiers) {
                                    BEncodedList announceUrls = new BEncodedList ();

                                    foreach (var tracker in tier.Trackers) {
                                        announceUrls.Add (new BEncodedString (tracker.Uri.OriginalString));
                                    }

                                    announceTrackers.Add (announceUrls);
                                }

                                dict.Add ("announce-list", announceTrackers);
                            }
                            var rawData = dict.Encode ();
                            if (Torrent.TryLoad (rawData, out Torrent t)) {
                                if (stopWhenDone) {
                                    Manager.RaiseMetadataReceived (rawData);
                                    return;
                                }

                                try {
                                    if (this.Settings.AutoSaveLoadMagnetLinkMetadata) {
                                        if (!Directory.Exists (Path.GetDirectoryName (savePath)))
                                            Directory.CreateDirectory (Path.GetDirectoryName (savePath));
                                        File.Delete (savePath);
                                        File.WriteAllBytes (savePath, dict.Encode ());
                                    }
                                } catch (Exception ex) {
                                    logger.ExceptionFormated (ex, "Cannot write metadata to path '{0}'", savePath);
                                    Manager.TrySetError (Reason.WriteFailure, ex);
                                    return;
                                }
                                Manager.SetMetadata (t);
                                _ = Manager.StartAsync ();
                                Manager.RaiseMetadataReceived (rawData);
                            } else {
                                bitField.SetAll (false);
                            }
                        }
                    }
                    RequestNextNeededPiece (id);
                    break;
                case LTMetadata.MessageType.Reject:
                    //TODO
                    //Think to what we do in this situation
                    //for moment nothing ;)
                    //reject or flood?
                    break;
                case LTMetadata.MessageType.Request://ever done in base class but needed to avoid default
                    break;
                default:
                    throw new MessageException ($"Invalid messagetype in LTMetadata: {message.MetadataMessageType}");
            }

        }

        protected override void HandleAllowedFastMessage (PeerId id, AllowedFastMessage message)
        {
            // Disregard these when in metadata mode as we can't request regular pieces anyway
        }

        protected override void HandleHaveAllMessage (PeerId id, HaveAllMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveMessage (PeerId id, HaveMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveNoneMessage (PeerId id, HaveNoneMessage message)
        {
            // Nothing
        }

        protected override void HandleInterestedMessage (PeerId id, InterestedMessage message)
        {
            // Nothing
        }

        int pieceToRequest;
        void RequestNextNeededPiece (PeerId id)
        {
            if (bitField.AllTrue)
                return;

            while (bitField[pieceToRequest % bitField.Length])
                pieceToRequest++;

            pieceToRequest = pieceToRequest % bitField.Length;
            var m = new LTMetadata (id.ExtensionSupports, LTMetadata.MessageType.Request, pieceToRequest++);
            id.MessageQueue.Enqueue (m);
            requestTimeout = DateTime.Now.Add (timeout);
        }

        protected override void AppendBitfieldMessage (PeerId id, MessageBundle bundle)
        {
            if (id.SupportsFastPeer)
                bundle.Messages.Add (new HaveNoneMessage ());
            // If the fast peer extensions are not supported we must not send a
            // bitfield message because we don't know how many pieces the torrent
            // has. We could probably send an invalid one and force the connection
            // to close.
        }

        protected override void HandleBitfieldMessage (PeerId id, BitfieldMessage message)
        {
            // If we receive a bitfield message we should ignore it. We don't know how many
            // pieces the torrent has so we can't actually safely decode the bitfield.
            if (message != BitfieldMessage.UnknownLength)
                throw new InvalidOperationException ("BitfieldMessages should not be decoded normally while in metadata mode.");
        }

        protected override void HandleExtendedHandshakeMessage (PeerId id, ExtendedHandshakeMessage message)
        {
            base.HandleExtendedHandshakeMessage (id, message);

            if (id.ExtensionSupports.Supports (LTMetadata.Support.Name)) {
                var metadataSize = message.MetadataSize.GetValueOrDefault (0);
                if (Stream == null && metadataSize > 0) {
                    Stream = new MemoryStream (new byte[metadataSize], 0, metadataSize, true, true);
                    int size = metadataSize % LTMetadata.BlockSize;
                    if (size > 0)
                        size = 1;
                    size += metadataSize / LTMetadata.BlockSize;
                    bitField = new MutableBitField (size);
                }

                // We only create the Stream if the remote peer has sent the metadata size key in their handshake.
                // There's no guarantee the remote peer has the metadata yet, so even though they support metadata
                // mode they might not be able to share the data.
                if (Stream != null)
                    RequestNextNeededPiece (id);
            }
        }

        protected override void SetAmInterestedStatus (PeerId id, bool interesting)
        {
            // Never set a peer as interesting when in metadata mode
            // we don't want to try download any data
        }
    }
}
