using System;
using Xunit;
using MonoTorrent.BEncoding;

namespace MonoTorrent.Common
{
    public class TorrentEditorTests
    {
        [Fact]
        public void EditingCreatesCopy()
        {
            var d = Create("comment", "a");
            var editor = new TorrentEditor(d);
            editor.Comment = "b";
            Assert.Equal("a", d["comment"].ToString());
        }

        [Fact]
        public void EditComment()
        {
            var d = Create("comment", "a");
            var editor = new TorrentEditor(d);
            editor.Comment = "b";
            d = editor.ToDictionary();
            Assert.Equal("b", d["comment"].ToString());
        }

        [Fact]
        public void ReplaceInfoDict()
        {
            var editor = new TorrentEditor(new BEncodedDictionary()) {CanEditSecureMetadata = false};
            Assert.Throws<InvalidOperationException>(() => editor.SetCustom("info", new BEncodedDictionary()));
        }

        [Fact]
        public void EditProtectedProperty_NotAllowed()
        {
            var editor = new TorrentEditor(new BEncodedDictionary()) {CanEditSecureMetadata = false};
            Assert.Throws<InvalidOperationException>(() => editor.PieceLength = 16);
        }

        private BEncodedDictionary Create(string key, string value)
        {
            var d = new BEncodedDictionary();
            d.Add(key, (BEncodedString) value);
            return d;
        }
    }
}