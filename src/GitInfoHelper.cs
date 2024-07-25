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

namespace MonoTorrent
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static class GitInfoHelper
    {
        internal static string ClientIdentifier { get; } = "MO";

        /// <summary>
        /// The current version of the client in the form "MO1234", which represents a version triplet of '1.2.34'.
        /// </summary>
        internal static string ClientVersion { get; private set; }

        internal static string DhtClientVersion { get; private set; }

        /// <summary>
        /// The full version of this library in the form 'A.B.C'.
        /// 'A' and 'B' are guaranteed to be 1 digit each. 'C' can be one or two digits.
        /// </summary>
        public static readonly Version Version;

        static GitInfoHelper()
        {
            var version = new Version(
                int.Parse(ThisAssembly.Git.BaseVersion.Major),
                int.Parse(ThisAssembly.Git.BaseVersion.Minor),
                int.Parse(ThisAssembly.Git.BaseVersion.Patch)
            );
            ClientVersion = "";
            DhtClientVersion = "";
            Initialize (version);
            Version = version;
        }

        internal static void Initialize(Version version)
        {
            // The scheme for generating the peerid includes the version number using the scheme:
            // ABCC, where A is the major, B is the minor and CC is the build version.
            if (version.Major > 9 || version.Major < 0)
                throw new ArgumentException("The major version should be between 0 and 9 (inclusive)");
            if (version.Minor > 9 || version.Minor < 0)
                throw new ArgumentException("The minor version should be between 0 and 9 (inclusive)");
            if (version.Build > 99 || version.Build < 0)
                throw new ArgumentException("The build version should be between 0 and 99 (inclusive)");

            // 'MO' for MonoTorrent then four digit version number
            var versionString = $"{version.Major}{version.Minor}{version.Build:00}";
            ClientVersion = $"{ClientIdentifier}{versionString}";

            // The DHT spec calls for a 2 char version identifier... urgh. I'm just going to
            // generate a 4 character version identifier anyway as, hopefully, anyone using
            // this field treats it like the opaque identifier it is supposed to be.
            //
            // If this causes issues then we should just trim the first 2 digits from this
            // and accept that it's not a unique identifier anymore. If we keep the last
            // two then we're more likely to be able to disambiguate.
            DhtClientVersion = $"{ClientIdentifier}{versionString}";
        }
    }
}
