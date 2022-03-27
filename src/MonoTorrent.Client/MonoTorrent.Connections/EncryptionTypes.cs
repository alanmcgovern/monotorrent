//
// EncryptionTypes.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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
using System.Linq;

namespace MonoTorrent.Connections
{
    static class EncryptionTypes
    {
        internal static IList<EncryptionType> All { get; } = MakeReadOnly (new[] { EncryptionType.RC4Header, EncryptionType.RC4Full, EncryptionType.PlainText });
        internal static IList<EncryptionType> PlainText { get; } = MakeReadOnly (new[] { EncryptionType.PlainText });
        internal static IList<EncryptionType> None { get; } = MakeReadOnly (Array.Empty<EncryptionType> ());

        static IList<EncryptionType> RC4Full { get; } = Array.AsReadOnly (new[] { EncryptionType.RC4Full });
        static IList<EncryptionType> RC4Header { get; } = Array.AsReadOnly (new[] { EncryptionType.RC4Header });
        static IList<EncryptionType> RC4FullHeader { get; } = Array.AsReadOnly (new[] { EncryptionType.RC4Full, EncryptionType.RC4Header });
        static IList<EncryptionType> RC4HeaderFull { get; } = Array.AsReadOnly (new[] { EncryptionType.RC4Header, EncryptionType.RC4Full });

        internal static IList<EncryptionType> MakeReadOnly (IEnumerable<EncryptionType> types)
            => Array.AsReadOnly (types.ToArray ());

        internal static bool SupportsRC4 (IList<EncryptionType> allowedEncryption)
        {
            return allowedEncryption.Contains (EncryptionType.RC4Full) || allowedEncryption.Contains (EncryptionType.RC4Header);
        }

        internal static IList<EncryptionType> Remove (IList<EncryptionType> allowedEncryption, EncryptionType encryptionType)
        {
            // Never return the value passed into this function as we want to be *guaranteed* that any return value
            // is an empty and immutable IList. An array of length 0 is immutable, but a List<T> of length zero is not.
            if (allowedEncryption.Count == 0)
                return None;

            var result = new EncryptionType[allowedEncryption.Count - 1];
            int j = 0;
            for (int i = 0; i < allowedEncryption.Count; i++)
                if (allowedEncryption[i] != encryptionType)
                    result[j++] = allowedEncryption[i];
            return MakeReadOnly (result);
        }

        internal static EncryptionType? PreferredRC4 (IList<EncryptionType> allowedEncryption)
        {
            for (int i = 0; i < allowedEncryption.Count; i++)
                if (allowedEncryption[i] != EncryptionType.PlainText)
                    return allowedEncryption[i];
            return null;
        }

        internal static IList<EncryptionType> GetPreferredEncryption (IList<EncryptionType> peerEncryption, IList<EncryptionType> allowedEncryption)
        {
            var supported = GetSupportedEncryption (peerEncryption, allowedEncryption);
            if (supported == None)
                return None;

            if (supported[0] == EncryptionType.PlainText)
                return PlainText;
            else if (supported[0] == EncryptionType.RC4Full)
                return RC4Full;
            else
                return RC4Header;
        }

        internal static IList<EncryptionType> GetSupportedEncryption (IList<EncryptionType> peerEncryption, IList<EncryptionType> allowedEncryption)
        {
            List<EncryptionType>? result = null;
            for (int i = 0; i < allowedEncryption.Count; i++) {
                if (peerEncryption.Contains (allowedEncryption[i])) {
                    result ??= new List<EncryptionType> (3 - i);
                    result.Add (allowedEncryption[i]);
                }
            }

            return result == null ? None : result.AsReadOnly ();
        }
    }
}
