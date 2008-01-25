// IConnection.cs created with MonoDevelop
// User: alan at 22:58 22/01/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

using System;
using MonoTorrent.Client;
using System.Net.Sockets;
using System.Net;

namespace MonoTorrent.Client
{
	public interface IConnection : IDisposable
	{
		byte[] AddressBytes { get; }

		bool Connected { get; }

        bool CanReconnect { get; }

        bool IsIncoming { get; }

        EndPoint EndPoint { get; }

		IAsyncResult BeginConnect(AsyncCallback callback, object state);
		void EndConnect(IAsyncResult result);
		
		IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state);
        int EndReceive(IAsyncResult result);
		
		IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state);
		int EndSend(IAsyncResult result);
	}
}
