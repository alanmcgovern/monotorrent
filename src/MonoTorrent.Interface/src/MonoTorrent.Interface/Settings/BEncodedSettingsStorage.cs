//
// BEncodedSettingsStorage.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Text;
using MonoTorrent.Common;
using System.IO;

namespace MonoTorrent.Interface.Settings
{
    public class BEncodedSettingsStorage : ISettingsStorage
    {
        #region Member Variables
        private string baseKey;
        private BEncodedDictionary settings;
        private object flushLocker = new object();
        #endregion


        #region Constructors
        public BEncodedSettingsStorage(string baseKey)
        {
            this.baseKey = baseKey;
            this.settings = new BEncodedDictionary();
            if (File.Exists(baseKey))
                try
                {
                    lock (this.flushLocker)
                        using (BinaryReader reader = new BinaryReader(new FileStream(baseKey, FileMode.Open)))
                            this.settings = (BEncodedDictionary)BEncode.Decode(reader);
                }
                catch (Exception ex)
                {
                }
        }
        #endregion


        #region Interface Members
        public void Store(string key, object val)
        {
            long result;
            IBEncodedValue value = val as IBEncodedValue;
            if (value == null)
            {
                if (val is string || val is bool)
                    value = (BEncodedString)val.ToString();
                else if (long.TryParse(val.ToString(), out result))
                    value = (BEncodedNumber)result;
                else
                    throw new ArgumentException("Value must be a BEcodedType for BEncodedSettingsStorage", "val");

            }

            settings.Add(key, value);
        }

        public object Retrieve(string key)
        {
            if (!settings.ContainsKey(key))
                return null;

            return this.settings[key];
        }


        public void Flush()
        {
            lock (flushLocker)
                using (BinaryWriter writer = new BinaryWriter(new FileStream(baseKey, FileMode.Create)))
                    writer.Write(this.settings.Encode());
        }
        #endregion
    }
}
