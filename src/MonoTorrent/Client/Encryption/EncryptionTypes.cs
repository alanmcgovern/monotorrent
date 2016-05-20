using System;

namespace MonoTorrent.Client.Encryption
{
    [Flags]
    public enum EncryptionTypes
    {
        None = 0,
        PlainText = 1 << 0,
        RC4Header = 1 << 1,
        RC4Full = 1 << 2,
        All = PlainText | RC4Full | RC4Header
    }
}