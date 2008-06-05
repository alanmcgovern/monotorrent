using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Encryption
{
    [Flags]
    public enum EncryptionTypes
    {
        PlainText   = 1 << 0,
        RC4Header   = 1 << 1,
        RC4Full     = 1 << 2,
        All = PlainText | RC4Full | RC4Header
    }
}
