using System;
using System.Collections.Generic;
using System.Text;
using SampleClient;
using NUnit.Framework;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages;

namespace MonoTorrent.Client.Managers.Tests
{
	[TestFixture]
	public class TorrentManagerTest
	{
		EngineTestRig rig;
		ConnectionPair conn;

		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			rig = new EngineTestRig("");
		}
		[TestFixtureTearDown]
		public void FixtureTeardown()
		{
			rig.Engine.Dispose();
		}

		[SetUp]
		public void Setup()
		{
			conn = new ConnectionPair(51515);
		}
		[TearDown]
		public void Teardown()
		{
			conn.Dispose();
		}

		[Test]
		public void AddConnectionToStoppedManager()
		{
			MessageBundle bundle = new MessageBundle();
			
			// Create the handshake and bitfield message
			bundle.Messages.Add(new HandshakeMessage(rig.Manager.Torrent.InfoHash, "11112222333344445555", VersionInfo.ProtocolStringV100));
			bundle.Messages.Add(new BitfieldMessage(rig.Torrent.Pieces.Count));
			byte[] data = bundle.Encode();

			// Add the 'incoming' connection to the engine and send our payload
			rig.Listener.Add(rig.Manager, conn.Incoming);
			conn.Outgoing.EndSend(conn.Outgoing.BeginSend(data, 0, data.Length, null, null));

			try { conn.Outgoing.EndReceive(conn.Outgoing.BeginReceive(data, 0, data.Length, null, null)); }
			catch { }

			System.Threading.Thread.Sleep(100);
			Assert.IsFalse(conn.Incoming.Connected, "#1");
			Assert.IsFalse(conn.Outgoing.Connected, "#2");
		}
	}
}
