using System;

namespace UCIS.Util {
	public class PipeStream : QueuedPacketStream {
		public override bool CanWrite { get { return !Closed; } }
		public override void Flush() { }
		public override void Write(byte[] buffer, int offset, int count) {
			if (Closed) throw new ObjectDisposedException("PipeStream");
			AddReadBufferCopy(buffer, offset, count);
		}
	}
}
