using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UCIS.Util {
	public class TarchiveReader : IDisposable, IEnumerator<TarchiveEntry>, IEnumerable<TarchiveEntry> {
		public Stream Source { get; private set; }
		public TarchiveEntry CurrentEntry { get; private set; }
		private Int64 SourceOffsetBase = 0;
		private Int64 SourceOffset = 0;
		private Boolean CanSeek = false;
		public TarchiveReader(String tarFile) : this(File.OpenRead(tarFile)) { }
		public TarchiveReader(Type type, String resource) : this(type.Assembly.GetManifestResourceStream(type, resource)) { }
		public TarchiveReader(Stream tar) {
			this.Source = tar;
			CanSeek = Source.CanSeek;
			if (CanSeek) SourceOffset = SourceOffsetBase = Source.Position;
		}
		public void Dispose() {
			Source.Dispose();
		}
		private static String ReadString(Byte[] header, int offset, int maxlength) {
			int end = Array.IndexOf<Byte>(header, 0, offset, maxlength);
			int length = (end == -1) ? maxlength : end - offset;
			return Encoding.UTF8.GetString(header, offset, length);
		}
		private void SeekForward(Int64 position) {
			if (CanSeek) {
				Source.Seek(position, SeekOrigin.Begin);
				SourceOffset = position;
			} else {
				if (position < SourceOffset) throw new ArgumentOutOfRangeException("Can not seek backwards");
				Byte[] buffer = new Byte[1024];
				while (position > SourceOffset) {
					int read = Source.Read(buffer, 0, (int)Math.Min(1024, position - SourceOffset));
					if (read <= 0) throw new EndOfStreamException();
					SourceOffset += read;
				}
			}
		}
		private Int32 GetPaddedSize(Int32 size) {
			int padding = size % 512;
			if (padding != 0) padding = 512 - padding;
			return size + padding;
		}
		private Byte[] ReadAll(int length) {
			Byte[] buffer = new Byte[length];
			int offset = 0;
			while (length > 0) {
				int read = Source.Read(buffer, offset, length);
				if (read <= 0) throw new EndOfStreamException();
				offset += read;
				length -= read;
				SourceOffset += read;
			}
			return buffer;
		}
		private Boolean IsAllZero(Byte[] header, int offset, int length) {
			length += offset;
			for (int i = offset; i < length; i++) if (header[i] != 0) return false;
			return true;
		}
		public TarchiveEntry ReadNext() {
			if (CurrentEntry != null) SeekForward(CurrentEntry.Offset + 512 + GetPaddedSize(CurrentEntry.Size));
			CurrentEntry = null;
			Byte[] header = ReadAll(512);
			if (IsAllZero(header, 0, header.Length)) return null;
			Boolean ustar = (ReadString(header, 257, 6) == "ustar");
			String fname = ReadString(header, 0, 100);
			String fsizes = ReadString(header, 124, 11);
			Int32 fsize = Convert.ToInt32(fsizes, 8);
			if (ustar) fname = ReadString(header, 345, 155) + fname;
			String ffname = fname.StartsWith("./") ? (fname.Length == 2 ? "/" : fname.Substring(2)) : fname;
			ffname = ffname.TrimEnd('/');
			TarchiveEntry entry = new TarchiveEntry() { Reader = this, OriginalName = fname, Offset = SourceOffset - 512, Size = fsize, Name = ffname };
			if (ustar) {
				entry.IsDirectory = header[156] == '5';
				entry.IsFile = header[156] == '0' || header[156] == 0;
			} else {
				entry.IsDirectory = fname.EndsWith("/");
				entry.IsFile = !entry.IsDirectory && header[156] == '0';
			}
			return CurrentEntry = entry;
		}
		public int ReadEntryData(TarchiveEntry entry, int fileoffset, Byte[] buffer, int bufferoffset, int count) {
			if (entry.Reader != this) throw new ArgumentException("The specified entry is not part of this archive");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count is negative");
			if (count == 0) return 0;
			if (!entry.IsFile) throw new ArgumentException("entry", "Specified entry is not a file");
			if (fileoffset > entry.Size) throw new ArgumentOutOfRangeException("fileoffset", "File offset exceeds file size");
			if (fileoffset == entry.Size) return 0;
			if (bufferoffset < 0) throw new ArgumentOutOfRangeException("bufferoffset", "Buffer offset is negative");
			if (bufferoffset + count > buffer.Length) throw new ArgumentOutOfRangeException("count", "Buffer offset and count exceed buffer dimensions");
			SeekForward(entry.Offset + 512 + fileoffset);
			int read = Source.Read(buffer, bufferoffset, Math.Min(count, entry.Size - fileoffset));
			if (read > 0) SourceOffset += read;
			return read;
		}
		public void SeekToEntry(TarchiveEntry entry) {
			if (entry.Reader != this) throw new ArgumentException("The specified entry is not part of this archive");
			SeekForward(entry.Offset);
		}
		public Stream GetFileStream(TarchiveEntry entry) {
			if (entry.Reader != this) throw new ArgumentException("The specified entry is not part of this archive");
			return new ReaderStream(this, entry);
		}
		TarchiveEntry IEnumerator<TarchiveEntry>.Current {
			get { return CurrentEntry; }
		}
		object IEnumerator.Current {
			get { return CurrentEntry; }
		}
		bool IEnumerator.MoveNext() {
			return ReadNext() != null;
		}
		void IEnumerator.Reset() {
			SeekForward(SourceOffsetBase);
		}
		IEnumerator<TarchiveEntry> IEnumerable<TarchiveEntry>.GetEnumerator() {
			return this;
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return this;
		}

		class ReaderStream : Stream {
			long position = 0;
			public TarchiveReader Source { get; private set; }
			public TarchiveEntry File { get; private set; }
			public ReaderStream(TarchiveReader source, TarchiveEntry entry) {
				this.Source = source;
				this.File = entry;
			}
			public override bool CanRead { get { return Source != null && Source.Source.CanRead && Position < Length; } }
			public override bool CanSeek { get { return Source != null && Source.Source.CanSeek; } }
			public override bool CanWrite { get { return false; } }
			protected override void Dispose(bool disposing) {
				base.Dispose(disposing);
				Source = null;
				File = null;
			}
			public override long Length { get { return File.Size; } }
			public override long Position {
				get { return position; }
				set {
					if (value < 0 || value > Length) throw new ArgumentOutOfRangeException("value");
					position = value;
				}
			}
			public override long Seek(long offset, SeekOrigin origin) {
				switch (origin) {
					case SeekOrigin.Begin: Position = offset; break;
					case SeekOrigin.Current: Position += offset; break;
					case SeekOrigin.End: Position = Length - offset; break;
				}
				return position;
			}
			public override int Read(byte[] buffer, int offset, int count) {
				int read = Source.ReadEntryData(File, (int)position, buffer, offset, count);
				position += read;
				return read;
			}
			public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
			public override void SetLength(long value) { throw new NotSupportedException(); }
			public override void Flush() { }
		}
	}
	public class TarchiveEntry {
		public TarchiveReader Reader { get; internal set; }
		public String Name { get; internal set; }
		public String OriginalName { get; internal set; }
		public Boolean IsDirectory { get; internal set; }
		public Boolean IsFile { get; internal set; }
		public Int32 Size { get; internal set; }
		public Int64 Offset { get; internal set; }

		public int Read(int fileoffset, Byte[] buffer, int bufferoffset, int count) {
			return Reader.ReadEntryData(this, fileoffset, buffer, bufferoffset, count);
		}

		public Stream GetStream() {
			return Reader.GetFileStream(this);
		}
	}
}
