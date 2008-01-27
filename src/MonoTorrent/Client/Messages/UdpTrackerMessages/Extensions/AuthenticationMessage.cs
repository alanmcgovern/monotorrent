using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Tracker.UdpTrackerMessages.Extensions
{
    class AuthenticationMessage : Message
    {
        byte usernameLength;
        string username;
        byte[] password;

        public override int ByteLength
        {
            get { return 4 + usernameLength + 8;  }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            usernameLength = buffer[offset]; offset++;
            username = Encoding.ASCII.GetString(buffer, offset, usernameLength); offset += usernameLength;
            password = new byte[8];
            Buffer.BlockCopy(buffer, offset, password, 0, password.Length);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            offset += Write(buffer, offset, usernameLength);
            offset += Write(buffer, offset, Encoding.ASCII.GetBytes(username));
            offset += Write(buffer, offset, password);
            
            return ByteLength;
        }
    }
}
