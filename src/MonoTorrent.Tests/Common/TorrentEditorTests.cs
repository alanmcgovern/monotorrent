using System;

using NUnit.Framework;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Common
{
    [TestFixture]
    public class TorrentEditorTests
    {
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

