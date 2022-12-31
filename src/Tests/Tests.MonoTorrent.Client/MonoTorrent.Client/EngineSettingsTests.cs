//
// EngineSettingsTests.cs
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


using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class EngineSettingsTests
    {
        [Test]
        public void EncodeDecode ()
        {
            var value = Serializer.DeserializeEngineSettings (Serializer.Serialize (new EngineSettings ()));
            Assert.AreEqual (value, new EngineSettings ());
        }

        [Test]
        public void UriPrefix ()
        {
            var modified = new EngineSettingsBuilder { HttpStreamingPrefix = "http://test.com/" };
            Assert.AreEqual (new EngineSettingsBuilder ().HttpStreamingPrefix, new EngineSettings ().HttpStreamingPrefix);
            Assert.AreEqual (modified.ToSettings ().HttpStreamingPrefix, modified.HttpStreamingPrefix);

            Assert.AreNotEqual (modified.HttpStreamingPrefix, new EngineSettings ().HttpStreamingPrefix);
            Assert.AreNotEqual (modified.ToSettings ().HttpStreamingPrefix, new EngineSettings ().HttpStreamingPrefix);
        }

        [Test]
        public void WithReportedAddress ()
        {
            var settings = new EngineSettingsBuilder {
                ReportedListenEndPoints = new System.Collections.Generic.Dictionary<string, System.Net.IPEndPoint> {
                    { "custom", new System.Net.IPEndPoint (System.Net.IPAddress.Any, 12345) },
                    { "ipv6", new System.Net.IPEndPoint (System.Net.IPAddress.IPv6Any, 3456) },
                    { "ipv4", new System.Net.IPEndPoint (System.Net.IPAddress.Loopback, 6798) },
                }
            }.ToSettings ();

            Assert.AreEqual (settings, settings);

            var deserialised = Serializer.DeserializeEngineSettings (Serializer.Serialize (settings));
            Assert.AreEqual (deserialised, settings);

        }
    }
}
