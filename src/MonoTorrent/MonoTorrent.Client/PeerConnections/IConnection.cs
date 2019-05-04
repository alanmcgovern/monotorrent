// IConnection.cs created with MonoDevelop
// User: alan at 22:58 22/01/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

using System;
using MonoTorrent.Client;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;

namespace MonoTorrent.Client.Connections
{
	public interface IConnection : IDisposable
	{
		byte[] AddressBytes { get; }

		bool Connected { get; }

        bool CanReconnect { get; }

        bool IsIncoming { get; }

        EndPoint EndPoint { get; }

        Task ConnectAsync();

        Task<int> ReceiveAsync (byte[] buffer, int offset, int count);

        Task<int> SendAsync (byte[] buffer, int offset, int count);

        Uri Uri { get; }
	}
}
