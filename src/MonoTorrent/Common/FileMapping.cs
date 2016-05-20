namespace MonoTorrent.Common
{
    public struct FileMapping
    {
        public string Source { get; }

        public string Destination { get; }

        public FileMapping(string source, string destination)
        {
            Source = source;
            Destination = destination;
        }
    }
}