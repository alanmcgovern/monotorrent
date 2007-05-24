using System;
using System.Text;

using MonoTorrent.BEncoding;

namespace MonoTorrent
{
    public class BEncodedKeyValuePair
    {
        private BEncodedString key;
        private BEncodedValue _value;

        public BEncodedString Key
        {
            get { return key; }
            set { key = value; }
        }

        public BEncodedValue Value
        {
            get { return _value; }
            set { _value = value; }
        }
    }
}
