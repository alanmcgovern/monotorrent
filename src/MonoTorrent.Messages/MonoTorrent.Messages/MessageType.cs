namespace MonoTorrent.Messages
{
    public enum MessageType : byte
    {
        // bittorrent v1 (bep3)
        Choke = 0,
        Unchoke = 1,
        Interested = 2,
        NotInterested = 3,

        Have = 4,
        Bitfield = 5,
        Request = 6,
        Piece = 7,
        Cancel = 8,

        // DHT (bep5)
        Port = 9,

        // fast extensions (bep6)
        Suggest = 13,
        HaveAll = 14,
        HaveNone = 15,
        RejectRequest = 16, // also in bep52
        AllowedFast = 17,

        // libtorrent extension protocol (bep1)
        Extended = 20,

        // bittorrent v2 (bep52)
        HashRequest = 21,
        Hashes = 22,
        HashReject = 23
    }

    public static class MessageTypeExtensions
    {
        public static bool IsFastExtension (this MessageType type) => type switch {
            MessageType.Suggest => true,
            MessageType.HaveAll => true,
            MessageType.HaveNone => true,
            MessageType.RejectRequest => true,
            MessageType.AllowedFast => true,
            _ => false
        };
    }
}
