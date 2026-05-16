//
// Torrent.cache.cs
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


using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    partial class Torrent
    {
        class Cache
        {
            internal static readonly BEncodedString Attr = "attr";
            internal static readonly BEncodedString Source = "source";
            internal static readonly BEncodedString Sha1 = "sha1";
            internal static readonly BEncodedString Ed2k = "ed2k";
            internal static readonly BEncodedString PublisherUrlUtf8 = "publisher-url.utf-8";
            internal static readonly BEncodedString PublisherUrl = "publisher-url";
            internal static readonly BEncodedString PublisherUtf8 = "publisher.utf-8";
            internal static readonly BEncodedString Publisher = "publisher";
            internal static readonly BEncodedString Files = "files";
            internal static readonly BEncodedString FileTree = "file tree";
            internal static readonly BEncodedString NameUtf8 = "name.utf-8";
            internal static readonly BEncodedString Name = "name";
            internal static readonly BEncodedString Pieces = "pieces";
            internal static readonly BEncodedString PiecesRoot = "pieces root";
            internal static readonly BEncodedString PieceLength = "piece length";
            internal static readonly BEncodedString Length = "length";
            internal static readonly BEncodedString Private = "private";
            internal static readonly BEncodedString PathUtf8 = "path.utf-8";
            internal static readonly BEncodedString Path= "path";
            internal static readonly BEncodedString MD5Sum = "md5sum";
            internal static readonly BEncodedString MetaVersion = "meta version";
            internal static readonly BEncodedString Announce = "announce";
            internal static readonly BEncodedString CommentUtf8 = "comment.utf-8";
            internal static readonly BEncodedString Comment = "comment";
            internal static readonly BEncodedString Nodes = "nodes";
            internal static readonly BEncodedString CreationDate = "creation date";
            internal static readonly BEncodedString CreatedBy = "created by";
            internal static readonly BEncodedString AnnounceList = "announce-list";
            internal static readonly BEncodedString Info = "info";
            internal static readonly BEncodedString Encoding = "encoding";
            internal static readonly BEncodedString PieceLayers = "piece layers";
            internal static readonly BEncodedString UrlList = "url-list";
        }
    }
}
