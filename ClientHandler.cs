using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace networking {

	public struct ClientHandler {
		public TcpClient tcp;
		private long _id;
		public long id => _id;
		public bool IsAlive => tcp != null && tcp.Connected;
		public ClientHandler(TcpClient tcp, long id) {
			this.tcp = tcp;
			_id = id;
		}
	}
}
