//
// LibtorrentMessageTests.cs
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
using System.Text;

using MonoTorrent.BEncoding;
using MonoTorrent.Messages;

using NUnit.Framework;

namespace MonoTorrent.Messages.Peer.Libtorrent
{
    [TestFixture]
    public class LibtorrentMessageTests
    {
        [Test]
        public void HandshakeSupportsTest ()
        {
            (var encoded, var releaser) = MessageEncoder.Extended.WriteHandshake(GitInfoHelper.ClientVersionMemory, false, 1234, 5555);

            var m = new Extended.HandshakeMessage (encoded);
            var supports = MessageEncoder.Extended.SupportedMessages.ToList ();
            BEncodeReader reader = new BEncodeReader (m.Mappings.Span);
            reader.ExpectDictionaryBegin ();
            while(reader.TryReadKey (out var key)) {
                var name = Encoding.UTF8.GetString (key);
                Assert.IsTrue (supports.Exists (t => t.Name == name));
                supports.RemoveAll ((t => t.Name == name));
                reader.SkipValue ();
            }
            Assert.IsEmpty (supports);
            Assert.AreEqual (Constants.DefaultMaxPendingRequests, m.MaxRequests, "#5");
        }

        [Test]
        public void HandshakeSupportsTest_Private ()
        {
            (var encoded, var releaser) = MessageEncoder.Extended.WriteHandshake (GitInfoHelper.ClientVersionMemory, true, 123, 5555);

            var m = new Extended.HandshakeMessage (encoded);
            var supports = MessageEncoder.Extended.SupportedMessages.ToList ();
            supports.Remove (MessageEncoder.Extended.PeerExchangeSupport);

            BEncodeReader reader = new BEncodeReader (m.Mappings.Span);
            reader.ExpectDictionaryBegin ();
            while (reader.TryReadKey (out var key)) {
                reader.SkipValue ();
                var name = Encoding.UTF8.GetString (key);
                Assert.IsTrue (supports.Exists (t => t.Name == name));
                supports.RemoveAll ((t => t.Name == name));
            }
            Assert.IsEmpty (supports);
            Assert.AreEqual (Constants.DefaultMaxPendingRequests, m.MaxRequests, "#5");
        }
    }
}
