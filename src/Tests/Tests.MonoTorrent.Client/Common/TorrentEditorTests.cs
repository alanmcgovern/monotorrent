using System;
using System.Collections.Generic;

using MonoTorrent.BEncoding;

using NUnit.Framework;

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
            var tier = new List<string> { "http://test.com/announce" };
            var editor = new TorrentEditor (new BEncodedDictionary ());
            editor.Announces.Add (tier);

            var result = editor.ToDictionary ();
            Assert.IsFalse (result.ContainsKey ("announce"), "#1");
            Assert.IsTrue (result.ContainsKey ("announce-list"), "#2");
        }

        [Test]
        public void Announces_OneTierThenRemove ()
        {
            var tier = new List<string> { "http://test.com/announce" };
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
            Assert.AreEqual ("a", d["comment"].ToString (), "#1");
        }

        [Test]
        public void EditComment ()
        {
            var d = Create ("comment", "a");
            var editor = new TorrentEditor (d);
            editor.Comment = "b";
            d = editor.ToDictionary ();
            Assert.AreEqual ("b", d["comment"].ToString (), "#1");
        }

        [Test]
        public void EditComment_null ()
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
            var editor = new TorrentEditor (new BEncodedDictionary ());
            Assert.IsFalse (editor.CanEditSecureMetadata);
            Assert.Throws<InvalidOperationException> (() => editor.SetCustom ("info", new BEncodedDictionary ()));
        }

        [Test]
        public void EditProtectedProperty_NotAllowed ()
        {
            var editor = new TorrentEditor (new BEncodedDictionary ());
            Assert.IsFalse (editor.CanEditSecureMetadata);
            Assert.Throws<InvalidOperationException> (() => editor.PieceLength = 16);
        }

        BEncodedDictionary Create (string key, string value)
        {
            var d = new BEncodedDictionary ();
            d.Add (key, (BEncodedString) value);
            return d;
        }
    }
}

