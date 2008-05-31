using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Encryption
{
    [Flags]
    public enum EncryptionTypes
    {
        None = 0,
        RC4Header = 1,
        RC4Full = 2,
        All = RC4Full | RC4Header
    }
}
