//
// Mapping.cs
//
// Authors:
//   Alan McGovern <alan.mcgovern@gmail.com>
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

namespace MonoTorrent.PortForwarding
{
    public sealed class Mapping : IEquatable<Mapping>
    {
        /// <summary>
        /// Connections made to the <see cref="PublicPort"/> port will be forwarded to the <see cref="PrivatePort"/>.
        /// </summary>
        public int PublicPort { get; }

        /// <summary>
        /// The internal port bound by a local socket/listener.
        /// </summary>
        public int PrivatePort { get; }

        /// <summary>
        /// The protocol which has been mapped.
        /// </summary>
        public Protocol Protocol { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="protocol"></param>
        /// <param name="port"></param>
        public Mapping (Protocol protocol, int port)
            : this (protocol, port, port)
        {
        }

        public Mapping (Protocol protocol, int privatePort, int publicPort)
        {
            Protocol = protocol;
            PrivatePort = privatePort;
            PublicPort = publicPort;
        }

        public override bool Equals (object? obj)
            => Equals (obj as Mapping);

        public bool Equals (Mapping? mapping)
            => !(mapping is null)
                && mapping.PrivatePort == PrivatePort
                && mapping.PublicPort == PublicPort
                && mapping.Protocol == Protocol;

        public override int GetHashCode ()
            => PrivatePort;
    }
}
