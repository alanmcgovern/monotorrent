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


using System;
using System.Collections.Immutable;
using System.Linq;

using MonoTorrent.Connections.Peer;

using NUnit.Framework;

namespace MonoTorrent.Client
{
    [TestFixture]
    public class EngineSettingsTests
    {
        [Test]
        public void ChangeCacheDirectory ()
        {
            var settings = new EngineSettings ();
            var origDht = settings.DhtNodeCacheFilePath;
            var origFast = settings.FastResumeCacheDirectory;

            var newSettings = settings with { CacheDirectory = "foo" };
            Assert.AreNotEqual (origDht, newSettings.DhtNodeCacheFilePath);
            Assert.AreNotEqual (origFast, newSettings.FastResumeCacheDirectory);
        }

        [Test]
        public void UpdateEncryptionTypes ()
        {
            var settings = new EngineSettings { AllowedEncryption = ImmutableArray.Create (Connections.EncryptionType.RC4Header) };
            Assert.AreEqual (1, settings.OutgoingConnectionEncryptionTiers.Length);
            Assert.AreEqual (Connections.EncryptionType.RC4Header, settings.OutgoingConnectionEncryptionTiers[0].Single ());

            settings = settings with { AllowedEncryption = ImmutableArray.Create (Connections.EncryptionType.PlainText, Connections.EncryptionType.RC4Header) };
            Assert.AreEqual (2, settings.OutgoingConnectionEncryptionTiers.Length);
            Assert.AreEqual (Connections.EncryptionType.PlainText, settings.OutgoingConnectionEncryptionTiers[0].Single ());
            Assert.AreEqual (Connections.EncryptionType.RC4Header, settings.OutgoingConnectionEncryptionTiers[1].Single ());
        }

        [Test]
        public void AllowedPeerTransports_DefaultsToTcpThenUtp ()
        {
            CollectionAssert.AreEqual (new[] { PeerTransport.Tcp, PeerTransport.Utp }, new EngineSettings ().AllowedTransports);
        }

        [Test]
        public void AllowedPeerTransports_InvalidValues ()
        {
            Assert.Throws<ArgumentException> (() => EngineSettings.Create (new EngineSettings () with {
                AllowedTransports = ImmutableArray<PeerTransport>.Empty
            }));
            Assert.Throws<ArgumentException> (() => EngineSettings.Create (new EngineSettings () with {
                AllowedTransports = ImmutableArray.Create (PeerTransport.Tcp, PeerTransport.Tcp)
            }));
        }

        [Test]
        public void EncodeDecode ()
        {
            var value = Serializer.DeserializeEngineSettings (Serializer.Serialize (new EngineSettings ()));
            Assert.AreEqual (Serializer.Serialize (value), Serializer.Serialize (new EngineSettings ()));
        }

        [Test]
        public void UriPrefix ()
        {
            var modified = new EngineSettings () with { HttpStreamingPrefix = "http://test.com/" };
            Assert.AreEqual (new EngineSettings ().HttpStreamingPrefix, new EngineSettings ().HttpStreamingPrefix);
            Assert.AreEqual (modified.HttpStreamingPrefix, "http://test.com/");

            Assert.AreNotEqual (modified.HttpStreamingPrefix, new EngineSettings ().HttpStreamingPrefix);
        }

        [Test]
        public void WithReportedAddress ()
        {
            var settings = new EngineSettings () with {
                ReportedListenEndPoints = new System.Collections.Generic.Dictionary<string, System.Net.IPEndPoint> {
                    { "custom", new System.Net.IPEndPoint (System.Net.IPAddress.Any, 12345) },
                    { "ipv6", new System.Net.IPEndPoint (System.Net.IPAddress.IPv6Any, 3456) },
                    { "ipv4", new System.Net.IPEndPoint (System.Net.IPAddress.Loopback, 6798) },
                }.ToImmutableDictionary ()
            };

            Assert.AreEqual (settings, settings);

            var deserialised = Serializer.DeserializeEngineSettings (Serializer.Serialize (settings));
            Assert.AreEqual (Serializer.Serialize (deserialised), Serializer.Serialize (settings));
            Assert.AreEqual (3, deserialised.ReportedListenEndPoints.Count);
            Assert.IsTrue (deserialised.ReportedListenEndPoints.ContainsKey ("custom"));
            Assert.IsTrue (deserialised.ReportedListenEndPoints["custom"].Equals (new System.Net.IPEndPoint (System.Net.IPAddress.Any, 12345)));
        }

        [Test]
        public void WithAllowedPeerTransports ()
        {
            var settings = new EngineSettings () with {
                AllowedTransports = ImmutableArray.Create (PeerTransport.Tcp, PeerTransport.Utp)
            };

            var deserialised = Serializer.DeserializeEngineSettings (Serializer.Serialize (settings));
            Assert.AreEqual (Serializer.Serialize (deserialised), Serializer.Serialize (settings));
            CollectionAssert.AreEqual (settings.AllowedTransports, deserialised.AllowedTransports);
        }
    }
}
