using System;
using System.IO;

namespace UCIS.Util {
	public abstract class PacketStream : Stream {
		public abstract Byte[] ReadPacket();
		public abstract IAsyncResult BeginReadPacket(AsyncCallback callback, object state);
		public abstract Byte[] EndReadPacket(IAsyncResult asyncResult);

		public virtual ArraySegment<Byte> ReadPacketFast() {
			Byte[] packet = ReadPacket();
			if (packet == null) throw new EndOfStreamException();
			return new ArraySegment<byte>(packet);
		}
		public virtual IAsyncResult BeginReadPacketFast(AsyncCallback callback, object state) { return BeginReadPacket(callback, state); }
		public virtual ArraySegment<Byte> EndReadPacketFast(IAsyncResult asyncResult) {
			Byte[] packet = EndReadPacket(asyncResult);
			if (packet == null) throw new EndOfStreamException();
			return new ArraySegment<byte>(packet);
		}
		public virtual void WritePacketFast(Byte[] packet, int unusedBefore, int unusedAfter) { Write(packet, unusedBefore, packet.Length - unusedBefore - unusedAfter); }
		public virtual int WriteFastBytesBefore { get { return 0; } }
		public virtual int WriteFastBytesAfter { get { return 0; } }
	}
}
