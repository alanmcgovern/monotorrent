//
// VersionInfo.cs
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
using System.Reflection;
using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public static class VersionInfo
    {
        /// <summary>
        /// Protocol string for version 1.0 of Bittorrent Protocol
        /// </summary>
        internal static readonly string ProtocolStringV100 = "BitTorrent protocol";

        static readonly string ClientIdentifier = "MO";

        /// <summary>
        /// The current version of the client in the form "MO1234", which represents 'MonoTorrent version 1.2.3.4'.
        /// </summary>
        internal static readonly string ClientVersion;

        internal static readonly string DhtClientVersion = $"{ClientIdentifier}06";

        /// <summary>
        /// The full version of this library.
        /// </summary>
        public static readonly Version Version;

        static VersionInfo ()
        {
            Version  = System.Version.Parse(typeof(VersionInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version);

            // 'MO' for MonoTorrent then four digit version number
            string version = string.Format ("{0}{1}{2}{3}", Math.Max (Version.Major, 0),
                                                            Math.Max (Version.Minor, 0),
                                                            Math.Max (Version.Build, 0),
                                                            Math.Max (Version.Revision, 0));
            if (version.Length > 4)
                version = version.Substring (0, 4);
            else
                version = version.PadRight (4, '0');
            ClientVersion = $"{ClientIdentifier}{version}";
        }
    }
}
