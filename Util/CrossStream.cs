using System;

namespace UCIS.Util {
	public class CrossStream : QueuedPacketStream {
		public CrossStream OtherSide { get; private set; }

		public static CrossStream CreatePair(out CrossStream stream1, out CrossStream stream2) {
			return stream1 = CreatePair(out stream2);
		}
		public static CrossStream CreatePair(out CrossStream stream2) {
			stream2 = new CrossStream();
			return stream2.OtherSide;
		}

		public CrossStream() {
			OtherSide = new CrossStream(this);
		}
		protected CrossStream(CrossStream other) {
			OtherSide = other;
		}

		public override bool CanRead { get { return true; } }
		public override bool CanWrite { get { return true; } }
		public override void Flush() { }

		public override void Write(byte[] buffer, int offset, int count) {
			CrossStream other = OtherSide;
			if (other == null) throw new ObjectDisposedException("CrossStream", "The stream has been closed");
			other.AddReadBufferCopy(buffer, offset, count);
		}

		public override void Close() {
			CrossStream other = OtherSide;
			CloseBase();
			if (other != null) other.CloseBase();
		}
		private void CloseBase() {
			OtherSide = null;
			base.Close();
		}
	}
}
