//
// ExtensionSupport.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MonoTorrent.Messages.Peer.Libtorrent
{
    public struct ExtensionSupport : IEquatable<ExtensionSupport>
    {
        ReadOnlyMemory<byte> nameUtf8;

        public byte MessageId { get; }
        public ReadOnlySpan<byte> NameUtf8 => nameUtf8.Span;
        public string Name { get; }

        internal ExtensionSupport(string name, byte messageId)
        {
            nameUtf8 = Encoding.UTF8.GetBytes (name);
            Name = name;
            MessageId = messageId;
        }

        public ExtensionSupport (ReadOnlySpan<byte> name, byte messageId)
        {
            MessageId = messageId;

            foreach (var v in MessageEncoder.Extended.SupportedMessages) {
                if (v.NameUtf8.SequenceEqual (name)) {
                    Name = v.Name;
                    nameUtf8 = v.nameUtf8;
                    break;
                }
            }

            if (String.IsNullOrEmpty (Name)) {
                Name = Encoding.UTF8.GetString (name);
                nameUtf8 = name.ToArray ();
            }
        }

        public override bool Equals ([NotNullWhen (true)] object? obj)
            => obj is ExtensionSupport o && this == o;

        public bool Equals (ExtensionSupport other)
            => this == other;

        public override int GetHashCode ()
            => Name.GetHashCode ();

        public override string ToString ()
            => string.Format ("{1}: {0}", Name, MessageId);

        public static bool operator == (ExtensionSupport left, ExtensionSupport right)
            => left.Name == right.Name && left.MessageId == right.MessageId;

        public static bool operator != (ExtensionSupport left, ExtensionSupport right)
            => !(left == right);
    }
}
