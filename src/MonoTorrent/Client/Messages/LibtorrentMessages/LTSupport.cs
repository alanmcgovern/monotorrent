namespace MonoTorrent.Client.Messages.Libtorrent
{
    public struct ExtensionSupport
    {
        public byte MessageId { get; }

        public string Name { get; }

        public ExtensionSupport(string name, byte messageId)
        {
            MessageId = messageId;
            Name = name;
        }

        public override string ToString()
        {
            return string.Format("{1}: {0}", Name, MessageId);
        }
    }
}