using System;
using MonoTorrent.Client.PieceWriters;

namespace MonoTorrent
{
    internal static class Check
    {
        static void DoCheck(object toCheck, string name)
        {
            if (toCheck == null)
                throw new ArgumentNullException(name);
        }

        static void IsNullOrEmpty(string toCheck, string name)
        {
            DoCheck(toCheck, name);
            if (toCheck.Length == 0)
                throw new ArgumentException("Cannot be empty", name);
        }

        internal static void Address(object address)
        {
            DoCheck(address, "address");
        }

        internal static void AddressRange(object addressRange)
        {
            DoCheck(addressRange, "addressRange");
        }

        internal static void AddressRanges(object addressRanges)
        {
            DoCheck(addressRanges, "addressRanges");
        }

        internal static void BaseDirectory(object baseDirectory)
        {
            DoCheck(baseDirectory, "baseDirectory");
        }

        internal static void Data(object data)
        {
            DoCheck(data, "data");
        }

        internal static void Endpoint(object endpoint)
        {
            DoCheck(endpoint, "endpoint");
        }

        internal static void Listener(object listener)
        {
            DoCheck(listener, "listener");
        }

        internal static void Location(object location)
        {
            DoCheck(location, "location");
        }

        internal static void Manager(object manager)
        {
            DoCheck(manager, "manager");
        }

        internal static void Path(object path)
        {
            DoCheck(path, "path");
        }

        internal static void PathNotEmpty(string path)
        {
            IsNullOrEmpty(path, "path");
        }

        internal static void Picker(object picker)
        {
            DoCheck(picker, "picker");
        }

        internal static void Result(object result)
        {
            DoCheck(result, "result");
        }

        internal static void SavePath(object savePath)
        {
            DoCheck(savePath, "savePath");
        }

        internal static void Settings(object settings)
        {
            DoCheck(settings, "settings");
        }

        internal static void Stream(object stream)
        {
            DoCheck(stream, "stream");
        }

        internal static void Torrent(object torrent)
        {
            DoCheck(torrent, "torrent");
        }

        internal static void TorrentInformation(object torrentInformation)
        {
            DoCheck(torrentInformation, "torrentInformation");
        }

        internal static void Tracker(object tracker)
        {
            DoCheck(tracker, "tracker");
        }

        internal static void Url(object url)
        {
            DoCheck(url, "url");
        }

        internal static void Uri(Uri uri)
        {
            DoCheck(uri, "uri");
        }

        internal static void Value(object value)
        {
            DoCheck(value, "value");
        }

        internal static void Writer(object writer)
        {
            DoCheck(writer, "writer");
        }
    }
}