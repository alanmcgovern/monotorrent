//
// ChokeUnchokeManagerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
using MonoTorrent.Client.Messages.Standard;

using NUnit.Framework;

namespace MonoTorrent.Client.Unchoking
{
    [TestFixture]
    public class ChokeUnchokeManagerTests
    {
        class Unchokeable : IUnchokeable
        {
            public bool Complete { get; set; }

            public long DownloadSpeed { get; set; }

            public long UploadSpeed { get; set; }

            public long MaximumDownloadSpeed { get; set; }

            public long MaximumUploadSpeed { get; set; }

            public int UploadSlots { get; set; }

            public int UploadingTo { get; set; }

            public List<PeerId> Peers { get; } = new List<PeerId> ();

            public Unchokeable (params PeerId[] peers)
            {
                Peers.AddRange (peers);
            }
        }

        [Test]
        public void UnchokeOneWithOneSlot ()
        {
            var unchokeable = new Unchokeable (PeerId.CreateInterested (10)) {
                UploadSlots = 1
            };
            Assert.IsTrue (unchokeable.Peers[0].AmChoking);
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.IsFalse (unchokeable.Peers[0].AmChoking);
            Assert.AreEqual (1, unchokeable.UploadingTo);
        }
        [Test]
        public void UnchokeThreeWithOneSlot ()
        {
            var unchokeable = new Unchokeable (
                PeerId.CreateInterested (10),
                PeerId.CreateInterested (10),
                PeerId.CreateInterested (10)) {
                UploadSlots = 1
            };
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.AreEqual (1, unchokeable.UploadingTo);
        }

        [Test]
        public void UnchokeOneWithUnlimitedSlots()
        {
            var unchokeable = new Unchokeable (PeerId.CreateInterested (10)) {
                UploadSlots = 0
            };
            Assert.IsTrue (unchokeable.Peers[0].AmChoking);
            new ChokeUnchokeManager (unchokeable).UnchokeReview ();
            Assert.IsFalse (unchokeable.Peers[0].AmChoking);
            Assert.AreEqual (1, unchokeable.UploadingTo);
        }
    }
}
