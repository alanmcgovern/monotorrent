using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PeerMessages
{
    /// <summary>
    /// This class represents the BT_EXTENDED_LST as listed by the Azurues Extended Messaging Protocol
    /// </summary>
    class ExtendedListMessage : IPeerMessage
    {
        public const byte MessageId = (byte)20;

        #region Member Variables
        public BEncodedDictionary Dictionary
        {
            get { return this.dictionary; }
        }
        private BEncodedDictionary dictionary;
        #endregion


        #region Constructors
        public ExtendedListMessage()
        {
        }
        public ExtendedListMessage(BEncodedDictionary supportedMessages)
        {
        }
        #endregion


        #region Methods
#warning Not implemented... temp fix for other clients issue. I should disconnect if someone sends me this and i don't support
        public int Encode(byte[] buffer, int offset)
        {
            return 0;
            throw new Exception("The method or operation is not implemented.");
        }

        public void Decode(byte[] buffer, int offset, int length)
        {
            return;
            this.dictionary = (BEncodedDictionary)BEncode.Decode(buffer, offset, length);
        }

        public void Handle(PeerConnectionID id)
        {
            return;
            throw new Exception("The method or operation is not implemented.");
        }

        public int ByteLength
        {

            get { return 0; }// throw new Exception("The method or operation is not implemented."); }
        }
        #endregion
    }
}
