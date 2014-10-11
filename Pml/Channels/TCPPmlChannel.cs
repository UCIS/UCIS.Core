using System;
using System.IO;
using System.Net.Sockets;
using UCIS.Net;

namespace UCIS.Pml {
	public class TCPPmlChannel : PassivePmlChannel {
		private TCPStream _socket;
		private IPmlRW _rw;
		private bool _open = false;

		public TCPPmlChannel(Socket socket) : this(new TCPStream(socket)) { }
		public TCPPmlChannel(TCPStream socket) {
			if (socket == null) throw new ArgumentNullException("socket");
			_socket = socket;
			_rw = new PmlBinaryRW(_socket);
			_open = true;
		}

		public override void WriteMessage(PmlElement message) {
			if (!_open) throw new InvalidOperationException("The channel is not open");
			lock (_rw) _rw.WriteMessage(message);
		}

		public override void Close() {
			if (!_open) return;
			_open = false;
			if (_socket != null) try { _socket.Close(); } catch { }
			base.Close();
		}

		public override PmlElement ReadMessage() {
			return _rw.ReadMessage();
		}
	}
}
