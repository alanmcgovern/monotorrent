namespace MonoTorrent.Messages
{
    public enum ExtendedMessageType : byte
    {
        Handshake = 0,  // https://www.bittorrent.org/beps/bep_0010.html
        Metadata,       // https://www.bittorrent.org/beps/bep_0009.html
        Chat,           // https://www.bittorrent.org/beps/bep_0009.html
        PeerExchange    // https://www.bittorrent.org/beps/bep_0009.html
    }
}
