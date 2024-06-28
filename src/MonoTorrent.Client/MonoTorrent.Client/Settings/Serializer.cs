//
// Serializer.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Net;

using MonoTorrent.BEncoding;
using MonoTorrent.Connections;
using MonoTorrent.PieceWriter;

namespace MonoTorrent.Client
{
    static class Serializer
    {
        internal static EngineSettings DeserializeEngineSettings (BEncodedDictionary dictionary)
            => Deserialize<EngineSettingsBuilder> (dictionary).ToSettings ();

        internal static BEncodedDictionary Serialize (EngineSettings settings)
            => Serialize (new EngineSettingsBuilder (settings));

        internal static TorrentSettings DeserializeTorrentSettings (BEncodedDictionary dictionary)
            => Deserialize<TorrentSettingsBuilder> (dictionary).ToSettings ();

        internal static BEncodedDictionary Serialize (TorrentSettings settings)
            => Serialize (new TorrentSettingsBuilder (settings));

        static T Deserialize<T> (BEncodedDictionary dict)
            where T : new()
        {
            T builder = new T ();
            var props = builder.GetType ().GetProperties ();
            foreach (var property in props) {
                if (!dict.TryGetValue (property.Name, out BEncodedValue? value))
                    continue;

                if (property.PropertyType == typeof (bool)) {
                    property.SetValue (builder, bool.Parse (value.ToString ()!));
                } else if (property.PropertyType == typeof (string)) {
                    property.SetValue (builder, ((BEncodedString) value).Text);
                } else if (property.PropertyType == typeof (FastResumeMode)) {
                    property.SetValue (builder, Enum.Parse (typeof (FastResumeMode), ((BEncodedString) value).Text));
                } else if (property.PropertyType == typeof (TimeSpan)) {
                    property.SetValue (builder, TimeSpan.FromTicks (((BEncodedNumber) value).Number));
                } else if (property.PropertyType == typeof (int)) {
                    property.SetValue (builder, (int) ((BEncodedNumber) value).Number);
                } else if (property.PropertyType == typeof (CachePolicy)) {
                    property.SetValue (builder, Enum.Parse (typeof (CachePolicy), ((BEncodedString) value).Text));
                } else if (property.PropertyType == typeof (Uri)) {
                    property.SetValue (builder, new Uri (((BEncodedString) value).Text));
                } else if (property.PropertyType == typeof (IPAddress)) {
                    property.SetValue (builder, IPAddress.Parse (((BEncodedString) value).Text));
                } else if (property.PropertyType == typeof (List<IPEndPoint>)) {
                    var list = (List<IPEndPoint>) property.GetValue (builder)!;
                    list.Clear ();
                    foreach (var endpoint in (BEncodedList) value)
                        list.Add (DecodeIPEndPoint ((BEncodedString) endpoint)!);
                } else if (property.PropertyType == typeof (IPEndPoint)) {
                    var endPoint = DecodeIPEndPoint ((BEncodedString) value);
                    property.SetValue (builder, endPoint);
                } else if (property.PropertyType == typeof (List<EncryptionType>)) {
                    var list = (IList<EncryptionType>) property.GetValue (builder)!;
                    list.Clear ();
                    foreach (BEncodedString encryptionType in (BEncodedList) value)
                        list.Add ((EncryptionType) Enum.Parse (typeof (EncryptionType), encryptionType.Text));
                } else if (property.PropertyType == typeof (Dictionary<string, IPEndPoint>)) {
                    property.SetValue (builder, ToIPAddressDictionary ((BEncodedDictionary) value));
                } else
                    throw new NotSupportedException ($"{property.Name} => type: ${property.PropertyType}");
            }
            return builder;
        }

        static IPEndPoint? DecodeIPEndPoint (BEncodedString value)
        {
#if NET5_0_OR_GREATER
            return IPEndPoint.Parse (value.Text);
#else
            var text = value.Text;
            var portSeparator = text.LastIndexOf (':');

            if (portSeparator > 0) {
                return new IPEndPoint (IPAddress.Parse (text.Substring(0, portSeparator)), int.Parse (text.Substring(portSeparator + 1)));
            }
            return null;
#endif
        }

        static BEncodedDictionary Serialize (object builder)
        {
            var dict = new BEncodedDictionary ();
            var props = builder.GetType ().GetProperties ();
            foreach (var property in props) {
                BEncodedValue? convertedValue = property.GetValue (builder) switch {
                    bool value => convertedValue = new BEncodedString (value.ToString ()),
                    IList<EncryptionType> value => convertedValue = new BEncodedList (value.Select (v => (BEncodedString) v.ToString ())),
                    string value => new BEncodedString (value),
                    TimeSpan value => new BEncodedNumber (value.Ticks),
                    IPAddress value => new BEncodedString (value?.ToString () ?? ""),
                    IPEndPoint value => new BEncodedString (value?.ToString () ?? ""),
                    int value => new BEncodedNumber (value),
                    FastResumeMode value => new BEncodedString (value.ToString ()),
                    CachePolicy value => new BEncodedString (value.ToString ()),
                    Uri value => new BEncodedString (value.OriginalString),
                    null => null,
                    Dictionary<string, IPEndPoint> value => FromIPAddressDictionary(value),
                    List<IPEndPoint> value => new BEncodedList (value.Select (t => new BEncodedString(t.ToString ()))),
                    _ => throw new NotSupportedException ($"{property.Name} => type: ${property.PropertyType}"),
                };
                // Ensure default values aren't accidentally propagated.
                if (property.PropertyType == typeof (IPEndPoint) && convertedValue == null)
                    convertedValue = new BEncodedList ();
                if (convertedValue != null)
                    dict[property.Name] = convertedValue;
            }

            return dict;
        }

        static BEncodedDictionary FromIPAddressDictionary (Dictionary<string, IPEndPoint> value)
        {
            var result = new BEncodedDictionary ();
            foreach (var kvp in value)
                result[kvp.Key] = new BEncodedList { (BEncodedString) kvp.Value.Address.ToString (), (BEncodedNumber) kvp.Value.Port };
            return result;
        }

        static IDictionary<string, IPEndPoint> ToIPAddressDictionary (BEncodedDictionary value)
        {
            var result = new Dictionary<string, IPEndPoint> ();
            foreach (var kvp in value) {
                var parts = (BEncodedList) kvp.Value;
                result[kvp.Key.Text] = new IPEndPoint (IPAddress.Parse (((BEncodedString) parts[0]).Text), (int) ((BEncodedNumber) parts[1]).Number);
            }
            return result;
        }
    }
}
