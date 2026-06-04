//
// IMessageHandler.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2026 Alan McGovern
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

using MonoTorrent.Messages;

namespace MonoTorrent.Client.Modes
{
    interface IMessageHandler
    {
        // bittorrent v1 (bep3)
        void HandleMessage (PeerId id, KeepAliveMessage message);
        void HandleMessage (PeerId id, ChokeMessage message);
        void HandleMessage (PeerId id, UnchokeMessage message);
        void HandleMessage (PeerId id, InterestedMessage message);
        void HandleMessage (PeerId id, NotInterestedMessage message);
        void HandleMessage (PeerId id, HaveMessage message);
        void HandleMessage (PeerId id, BitfieldMessage message);
        void HandleMessage (PeerId id, RequestMessage message);
        void HandleMessage (PeerId id, PieceMessage message);
        void HandleMessage (PeerId id, CancelMessage message);

        // DHT (bep5)
        void HandleMessage (PeerId id, PortMessage message);

        // Fast extensions
        void HandleMessage (PeerId id, SuggestMessage message);
        void HandleMessage (PeerId id, HaveAllMessage message);
        void HandleMessage (PeerId id, HaveNoneMessage message);
        void HandleMessage (PeerId id, RejectRequestMessage message);
        void HandleMessage (PeerId id, AllowedFastMessage message);

        // bittorrent v2 (bep52)
        void HandleMessage (PeerId id, HashRequestMessage message);
        void HandleMessage (PeerId id, HashesMessage message);
        void HandleMessage (PeerId id, HashRejectMessage message);

        // LT Extensions
        void HandleMessage (PeerId id, Extended.HandshakeMessage message);
        void HandleMessage (PeerId id, Extended.PeerExchangeMessage message);
        void HandleMessage (PeerId id, Extended.MetadataMessage message);
    }
}
