using System;

using NUnit.Framework;
using MonoTorrent.BEncoding;
using System.Collections.Generic;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentEditorTests
    {
        [Test]
        public void Announces_None ()
        {
            var editor = new TorrentEditor (new BEncodedDictionary ());
            var result = editor.ToDictionary ();
            Assert.IsFalse (result.ContainsKey ("announce"), "#1");
            Assert.IsFalse (result.ContainsKey ("announce-list"), "#2");
        }

        [Test]
        public void Announces_OneTier ()
        {
            var tier = new RawTrackerTier { "http://test.com/announce" };
            var editor = new TorrentEditor (new BEncodedDictionary ());
            editor.Announces.Add (tier);

            var result = editor.ToDictionary ();
            Assert.IsFalse (result.ContainsKey ("announce"), "#1");
            Assert.IsTrue (result.ContainsKey ("announce-list"), "#2");
        }

        [Test]
        public void Announces_OneTierThenRemove ()
        {
            var tier = new RawTrackerTier { "http://test.com/announce" };
            var editor = new TorrentEditor (new BEncodedDictionary ());
            editor.Announces.Add (tier);

            editor = new TorrentEditor (editor.ToDictionary ());
            editor.Announces.Clear ();

            var result = editor.ToDictionary ();
            Assert.IsFalse (result.ContainsKey ("announce"), "#1");
            Assert.IsFalse (result.ContainsKey ("announce-list"), "#2");
        }

        [Test]
        public void Announces_Single ()
        {
            var editor = new TorrentEditor (new BEncodedDictionary ()) {
                Announce = "udp://test.com/announce"
            };
            var result = editor.ToDictionary ();
            Assert.IsTrue (result.ContainsKey ("announce"), "#1");
            Assert.IsFalse (result.ContainsKey ("announce-list"), "#2");
        }

        [Test]
        public void EditingCreatesCopy ()
        {
            var d = Create ("comment", "a");
            var editor = new TorrentEditor (d);
            editor.Comment = "b";
            Assert.AreEqual ("a", d ["comment"].ToString (), "#1");
        }

        [Test]
        public void EditComment ()
        {
            var d = Create ("comment", "a");
            var editor = new TorrentEditor (d);
            editor.Comment = "b";
            d = editor.ToDictionary ();
            Assert.AreEqual ("b", d ["comment"].ToString (), "#1");
        }

        [Test]
        public void EditComment_null()
        {
            var d = Create ("comment", "a");
            var editor = new TorrentEditor (d) {
                Comment = null
            };
            d = editor.ToDictionary ();
            Assert.IsFalse (d.ContainsKey ("comment"), "#1");
            Assert.IsNull (editor.Comment, "#2");
        }

        [Test]
        public void ReplaceInfoDict ()
        {
            Assert.Throws<InvalidOperationException>(() =>
           {
               var editor = new TorrentEditor(new BEncodedDictionary()) { CanEditSecureMetadata = false };
               editor.SetCustom("info", new BEncodedDictionary());
           });
        }

        [Test]
        public void EditProtectedProperty_NotAllowed ()
        {
            Assert.Throws<InvalidOperationException>(() =>
           {
               var editor = new TorrentEditor(new BEncodedDictionary()) { CanEditSecureMetadata = false };
               editor.PieceLength = 16;
           });
        }

        BEncodedDictionary Create (string key, string value)
        {
            var d = new BEncodedDictionary ();
            d.Add (key, (BEncodedString) value);
            return d;
        }
    }
}

