using System;
using System.Collections.Generic;
using System.Text;

namespace UCIS.Pml {
	public class PmlChannel : ActivePmlChannel {
		private IPmlRW _rw;
		private bool _open;

		public PmlChannel(IPmlRW rw) {
			if (rw == null) throw new ArgumentNullException("rw");
			_rw = rw;
			_open = true;
			UThreadPool.RunTask(worker, null);
		}

		public IPmlReader Reader { get { return _rw; } }
		public IPmlWriter Writer { get { return _rw; } }

		public new bool IsOpen { get { return _open; } }

		public override void WriteMessage(PmlElement message) {
			if (!_open) throw new InvalidOperationException("The channel is not open");
			lock (_rw) _rw.WriteMessage(message);
		}

		public override void Close() {
			if (!_open) return;
			_open = false;
			if (_rw != null) _rw = null;
			base.Close();
		}

		private void worker(Object state) {
			try {
				while (_open) {
					base.PushReceivedMessage(_rw.ReadMessage());
				}
			} catch (System.Net.Sockets.SocketException ex) {
				Console.WriteLine("SocketException in PmlChannel.worker: " + ex.Message);
			} catch (System.IO.EndOfStreamException ex) {
				Console.WriteLine("EndOfStreamException in PmlChannel.worker: " + ex.Message);
			} catch (Exception ex) {
				Console.WriteLine(ex.ToString());
			} finally {
				Close();
			}
		}
	}
}
