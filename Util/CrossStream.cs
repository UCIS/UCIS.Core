using System;

namespace UCIS.Util {
	public class CrossStream : QueuedPacketStream {
		protected CrossStream otherPart;
		private Boolean otherClosed = false;

		public CrossStream OtherSide { get { return otherPart; } }

		public static CrossStream CreatePair(out CrossStream stream1, out CrossStream stream2) {
			return stream1 = CreatePair(out stream2);
		}
		public static CrossStream CreatePair(out CrossStream stream2) {
			stream2 = new CrossStream();
			return stream2.otherPart;
		}

		public CrossStream() {
			otherPart = new CrossStream(this);
		}
		protected CrossStream(CrossStream other) {
			otherPart = other;
		}

		public override bool CanRead { get { return true; } }
		public override bool CanWrite { get { return true; } }
		public override void Flush() { }

		public override void Write(byte[] buffer, int offset, int count) {
			if (otherClosed) throw new ObjectDisposedException("CrossStream", "The stream has been closed");
			otherPart.AddReadBufferCopy(buffer, offset, count);
		}

		public override void Close() {
			CloseBase();
			otherPart.CloseBase();
		}
		private void CloseBase() {
			otherClosed = true;
			otherPart = null;
			base.Close();
		}
	}
}
