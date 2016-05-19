using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MonoTorrent.Client
{
    public class BanListParser
    {
        public IEnumerable<AddressRange> Parse(Stream stream)
        {
            var reader = new StreamReader(stream);

            string result = null;
            var r = new Regex(@"([0-9]{1,3}\.){3,3}[0-9]{1,3}");

            while ((result = reader.ReadLine()) != null)
            {
                var collection = r.Matches(result);
                if (collection.Count == 1)
                {
                    var range = new AddressRange();
                    var s = collection[0].Captures[0].Value.Split('.');
                    range.Start = (int.Parse(s[0]) << 24) | (int.Parse(s[1]) << 16) | (int.Parse(s[2]) << 8) |
                                  int.Parse(s[3]);
                    range.End = range.Start;
                    yield return range;
                }
                else if (collection.Count == 2)
                {
                    var s = collection[0].Captures[0].Value.Split('.');
                    var start = (int.Parse(s[0]) << 24) | (int.Parse(s[1]) << 16) | (int.Parse(s[2]) << 8) |
                                int.Parse(s[3]);

                    s = collection[1].Captures[0].Value.Split('.');
                    var end = (int.Parse(s[0]) << 24) | (int.Parse(s[1]) << 16) | (int.Parse(s[2]) << 8) |
                              int.Parse(s[3]);

                    var range = new AddressRange();
                    range.Start = start;
                    range.End = end;
                    yield return range;
                }
            }
        }
    }
}