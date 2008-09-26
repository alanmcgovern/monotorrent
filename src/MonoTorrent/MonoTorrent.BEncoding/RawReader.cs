using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MonoTorrent.BEncoding
{
    public class RawReader : BinaryReader
    {
        private bool strictDecoding;

        public bool StrictDecoding
        {
            get { return strictDecoding; }
        }

        public RawReader(Stream input)
            : this(input, true)
        {

        }

        public RawReader(Stream input, bool strictDecoding)
            : base(input)
        {
            this.strictDecoding = strictDecoding;
        }
    }
}
