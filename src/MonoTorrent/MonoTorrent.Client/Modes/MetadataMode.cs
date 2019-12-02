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
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Libtorrent;

namespace MonoTorrent.Client.Modes
{
    class MetadataMode : Mode
    {
        private BitField bitField;
        static readonly TimeSpan timeout = TimeSpan.FromSeconds(10);
        private PeerId currentId;
        string savePath;
        private DateTime requestTimeout;

        bool HasAnnounced { get; set; }
        internal MemoryStream Stream { get; set; }

        public override bool CanHashCheck => true;
        public override TorrentState State => TorrentState.Metadata;

        public MetadataMode (TorrentManager manager, DiskManager diskManager, ConnectionManager connectionManager, EngineSettings settings, string savePath)
            : base (manager, diskManager, connectionManager, settings)
        {
            this.savePath = savePath;
        }

        public override void Tick(int counter)
        {
            if (!HasAnnounced) {
                HasAnnounced = true;
                SendAnnounces ();
            }

            //if one request have been sent and we have wait more than timeout
            // request the next peer
            if (requestTimeout < DateTime.Now)
            {
                SendRequestToNextPeer();
            }
            
        }

        async void SendAnnounces ()
        {
            try {
                Manager.DhtAnnounce ();
                await Task.WhenAll (
                    Manager.TrackerManager.Announce (),
                    Manager.LocalPeerAnnounceAsync ()
                );
            } catch {
                // Nothing.
            }
        }

        private void SendRequestToNextPeer()
        {
            NextPeer();

            if (currentId != null)
            {
                RequestNextNeededPiece (currentId);
            }
        }

        private void NextPeer()
        {
            bool flag = false;

            foreach (PeerId id in Manager.Peers.ConnectedPeers)
            {
                if (id.SupportsLTMessages && id.ExtensionSupports.Supports(LTMetadata.Support.Name))
                {
                    if (id == currentId)
                        flag = true;
                    else if (flag)
                    {
                        currentId = id;
                        return;
                    }
                }
            }
            //second pass without removing the currentid and previous ones
            foreach (PeerId id in Manager.Peers.ConnectedPeers)
            {
                if (id.SupportsLTMessages && id.ExtensionSupports.Supports(LTMetadata.Support.Name))
                {
                    currentId = id;
                    return;
                }
            }
            currentId = null;
            return;
        }

        protected override void HandleLtMetadataMessage(PeerId id, LTMetadata message)
        {
            base.HandleLtMetadataMessage(id, message);

            switch (message.MetadataMessageType)
            {
                case LTMetadata.eMessageType.Data:
                    if (Stream == null)
                        throw new Exception("Need extention handshake before ut_metadata message.");

                    Stream.Seek(message.Piece * LTMetadata.BlockSize, SeekOrigin.Begin);
                    Stream.Write(message.MetadataPiece, 0, message.MetadataPiece.Length);
                    bitField[message.Piece] = true;
                    if (bitField.AllTrue)
                    {
                        byte[] hash;
                        Stream.Position = 0;
                        using (SHA1 hasher = HashAlgoFactory.Create<SHA1>())
                            hash = hasher.ComputeHash(Stream);

                        if (!Manager.InfoHash.Equals (hash))
                        {
                            bitField.SetAll(false);
                        }
                        else
                        {
                            Torrent t;
                            Stream.Position = 0;
                            BEncodedDictionary dict = new BEncodedDictionary();
                            dict.Add ("info", BEncodedValue.Decode(Stream));
                            // FIXME: Add the trackers too
                            if (Torrent.TryLoad(dict.Encode (), out t))
                            {
                                try
                                {
                                    if (Directory.Exists(savePath))
                                        savePath = Path.Combine (savePath, Manager.InfoHash.ToHex() + ".torrent");
                                    File.Delete (savePath);
                                    File.WriteAllBytes(savePath, dict.Encode ());
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(null, "*METADATA EXCEPTION* - Can not write in {0} : {1}", savePath, ex);
                                    Manager.TrySetError (Reason.WriteFailure, ex);
                                    return;
                                }
                                t.TorrentPath = savePath;
                                Manager.Torrent = t;
                                SwitchToRegular();
                            }
                            else
                            {
                                bitField.SetAll(false);
                            }
                        }
                    }
					//Double test because we can change the bitfield in the other block
                    if (!bitField.AllTrue)
                    {
                        RequestNextNeededPiece(id);
                    }
                    break;
                case LTMetadata.eMessageType.Reject:
                    //TODO
                    //Think to what we do in this situation
                    //for moment nothing ;)
                    //reject or flood?
                    break;
                case LTMetadata.eMessageType.Request://ever done in base class but needed to avoid default
                    break;
                default:
                    throw new MessageException(string.Format("Invalid messagetype in LTMetadata: {0}", message.MetadataMessageType));
            }

        }

        private void SwitchToRegular()
        {
            foreach (PeerId id in new List<PeerId> (Manager.Peers.ConnectedPeers))
                Manager.Engine.ConnectionManager.CleanupSocket (Manager, id);
            Manager.Bitfield = new BitField(Manager.Torrent.Pieces.Count);
            Manager.PartialProgressSelector = new BitField (Manager.Torrent.Pieces.Count);
            Manager.UnhashedPieces = new BitField(Manager.Torrent.Pieces.Count).SetAll (true);

            Manager.ChangePicker(Manager.CreateStandardPicker());
            foreach (TorrentFile file in Manager.Torrent.Files)
                file.FullPath = Path.Combine (Manager.SavePath, file.Path);
            _ = Manager.StartAsync();
        }

        protected override void HandleAllowedFastMessage (PeerId id, MonoTorrent.Client.Messages.FastPeer.AllowedFastMessage message)
        {
            // Disregard these when in metadata mode as we can't request regular pieces anyway
        }

        protected override void HandleHaveAllMessage(PeerId id, MonoTorrent.Client.Messages.FastPeer.HaveAllMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveMessage(PeerId id, MonoTorrent.Client.Messages.Standard.HaveMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveNoneMessage(PeerId id, MonoTorrent.Client.Messages.FastPeer.HaveNoneMessage message)
        {
            // Nothing
        }

        protected override void HandleInterestedMessage(PeerId id, MonoTorrent.Client.Messages.Standard.InterestedMessage message)
        {
            // Nothing
        }

        private void RequestNextNeededPiece(PeerId id)
        {
            int index = bitField.FirstFalse();
            if (index == -1)
                return;//throw exception or switch to regular?

            LTMetadata m = new LTMetadata(id, LTMetadata.eMessageType.Request, index);
            id.Enqueue(m);
            requestTimeout = DateTime.Now.Add(timeout);
        }

        internal Torrent GetTorrent()
        {
            byte[] calculatedInfoHash;
            using (SHA1 sha = HashAlgoFactory.Create<SHA1>())
                calculatedInfoHash = sha.ComputeHash(Stream.ToArray());
            if (!Manager.InfoHash.Equals (calculatedInfoHash))
                throw new Exception("invalid metadata");//restart ?

            BEncodedValue d = BEncodedValue.Decode(Stream);
            BEncodedDictionary dict = new BEncodedDictionary();
            dict.Add("info", d);

            return Torrent.LoadCore(dict);
        }

        protected override void AppendBitfieldMessage(PeerId id, MessageBundle bundle)
        {
            // We can't send a bitfield message in metadata mode as
            // we don't know what size the bitfield is
        }

        protected override void HandleExtendedHandshakeMessage(PeerId id, ExtendedHandshakeMessage message)
        {
            base.HandleExtendedHandshakeMessage(id, message);

            if (id.ExtensionSupports.Supports(LTMetadata.Support.Name))
            {
                Stream = new MemoryStream(new byte[message.MetadataSize], 0, message.MetadataSize, true, true);
                int size = message.MetadataSize % LTMetadata.BlockSize;
                if (size > 0)
                    size = 1;
                size += message.MetadataSize / LTMetadata.BlockSize;
                bitField = new BitField(size);
                RequestNextNeededPiece(id);
            }
        }

        protected override void SetAmInterestedStatus(PeerId id, bool interesting)
        {
            // Never set a peer as interesting when in metadata mode
            // we don't want to try download any data
        }
    }
}
