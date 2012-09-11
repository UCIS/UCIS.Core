using System;
using System.Collections.Generic;

namespace UCIS.Radio {
	public struct TunerFrequencyRange {
		public TunerFrequencyRange(ulong Begin, ulong End) {
			this.Begin = Begin;
			this.End = End;
		}

		public ulong Begin;
		public ulong End;
	}
	public struct TunerMode {
		public TunerMode(int Index, string Name) {
			this.Index = Index;
			this.Name = Name;
		}

		public int Index;
		public string Name;
	}
	public struct TunerFilter {
		public TunerFilter(int Index, string Name) {
			this.Index = Index;
			this.Name = Name;
		}

		public int Index;
		public string Name;
	}

	public class TunerCapabilities {
		public TunerFrequencyRange[] Bands = new TunerFrequencyRange[0];
		public IDictionary<byte, string> Modes = new Dictionary<byte, string>();
		public IDictionary<byte, string> Filters = new Dictionary<byte, string>();
		public IList<TunerOption> Options = new List<TunerOption>();
		public byte[] AvailableModes;
		public byte[] AvailableFilters;
	}

	public enum TunerOption : byte {
		//R/W 0-255 value
		Volume = 1,
		Squelch = 2,
		IfShift = 3,

		//Not accessible (feature indication only)
		Frequency = 50,
		Mode = 51,
		Filter = 52,

		//Read only
		Signal = 70,
		Online = 71,

		//R/W On/Off
		Attenuator = 100,
		AGC = 101,
		NoiseBlanker = 102,
		VSC = 103,
		StereoMono = 104,

		//W Execute
		Poll = 151,
		RadioText = 200,

		//200+: reserved for personal use
	}

	public class TunerOptionChangedEventArgs : EventArgs {
		public TunerOptionChangedEventArgs(TunerOption option) { Option = option; }
		public TunerOption Option;
	}

	public interface ITuner {
		event EventHandler TuningChanged;
		event EventHandler<TunerOptionChangedEventArgs> OptionChanged;
		void Open();
		void Close();
		void Poll();
		TunerCapabilities Capabilities { get; }
		ulong Frequency { get; set; }
		byte Mode { get; set; }
		byte Filter { get; set; }
		bool SetTuning(ulong frequency, byte mode, byte filter);
		bool SetOption(TunerOption Option, byte Value);
		byte GetOption(TunerOption Option);
	}

	public abstract class Tuner : ITuner {
		private ulong _frequency;
		private byte _mode, _filter;
		private TunerCapabilities _capabilities = new TunerCapabilities();
		private byte[] _options = new byte[256];

		public event EventHandler TuningChanged;
		public event EventHandler AvailableModesChanged;
		public event EventHandler<TunerOptionChangedEventArgs> OptionChanged;

		protected Tuner() {
			//AcceptOption(ReceiverOptions.IfShift, 127);
			//AcceptOption(ReceiverOptions.Online, 1);
		}

		public virtual void Open() { }
		public virtual void Close() { }
		public virtual void Poll() { }

		public TunerCapabilities Capabilities { get { return _capabilities; } }

		public ulong Frequency {
			get { return _frequency; }
			set {SetTuning(value, 0, 0); }
		}
		public byte Mode {
			get { return _mode; }
			set { SetTuning(0, value, 0); }
		}
		public byte Filter {
			get { return _mode; }
			set { SetTuning(0, 0, value); }
		}
		public virtual bool SetTuning(ulong frequency, byte mode, byte filter) {
			return SetTuning(ref frequency, ref mode, ref filter);
		}
		public virtual bool SetTuning(ref ulong frequency, ref byte mode, ref byte filter) {
			bool notexact = false;
			CheckFrequency(ref frequency, ref notexact);
			CheckMode(ref mode, ref notexact);
			CheckFilter(ref filter, ref notexact);
			AcceptTuning(frequency, mode, filter);
			return !notexact;
		}

		public virtual bool SetOption(TunerOption Option, byte Value) {
			return SetOption(Option, ref Value);
		}
		public virtual bool SetOption(TunerOption Option, ref byte Value) {
			bool notexact = false;
			CheckOption(Option, ref Value, ref notexact);
			AcceptOption(Option, Value);
			return !notexact;
		}
		public virtual byte GetOption(TunerOption Option) {
			return _options[(int)Option];
		}
		protected bool CheckOption(TunerOption Option, ref byte Value, ref bool notexact) {
			switch (Option) {
				case TunerOption.Filter:
				case TunerOption.Frequency:
				case TunerOption.Mode:
				case TunerOption.Signal:
					Value = 0;
					notexact = true;
					return false;
				case TunerOption.AGC:
				case TunerOption.Attenuator:
				case TunerOption.NoiseBlanker:
				case TunerOption.StereoMono:
				case TunerOption.VSC:
					if (Value > 1) Value = 1;
					return (Value != _options[(int)Option]);
				case TunerOption.IfShift:
				case TunerOption.Squelch:
				case TunerOption.Volume:
					return (Value != _options[(int)Option]);
				case TunerOption.RadioText:
					Value = 1;
					return true;
				default:
					return true;
			}
		}
		protected void AcceptOption(TunerOption Option, byte Value) {
			if (_options[(int)Option] != Value) {
				_options[(int)Option] = Value;
				if (OptionChanged != null) OptionChanged(this, new TunerOptionChangedEventArgs(Option));
			}
		}

		protected bool CheckFrequency(ref ulong frequency, ref bool notexact) {
			ulong closest = 0;
			ulong diff = ulong.MaxValue;
			if (frequency == 0 || frequency == _frequency) { //Frequency was not changed, return current mode
				frequency = _mode;
				return false;
			}
			foreach (TunerFrequencyRange f in _capabilities.Bands) {
				if (frequency >= f.Begin && frequency <= f.End) return true;
				if (frequency < f.Begin && diff > f.Begin - frequency) {
					closest = f.Begin;
					diff = closest - frequency;
				} else if (frequency > f.End && diff > frequency - f.End) {
					closest = f.End;
					diff = frequency - closest;
				}
			}
			if (closest != 0 && closest != _frequency) {
				notexact = true;
				frequency = closest;
				return true;
			} else {
				return false;
			}
		}
		protected bool CheckMode(ref byte mode, ref bool notexact) {
			if (mode == 0 || mode == _mode) { //Mode was not changed, return current mode
				mode = _mode;
				return false;
			}
			foreach (byte b in _capabilities.AvailableModes) { //Mode was changed, accept if allowed
				if (mode == b) return true;
			}
			mode = _mode; //Mode was changed but not allowed, keep current mode
			return false;
		}
		protected bool CheckFilter(ref byte filter, ref bool notexact) {
			byte closest = 0;
			int diff = int.MaxValue;
			//Filter has to be checked against AvailableFilters, in case AvailableFilters was changed
			/*if (filter == 0 || filter == _filter) { //Filter was not changed, return current filter
				filter = _filter;
				return false;
			}*/
			foreach (byte b in _capabilities.AvailableFilters) { //Filter was changed, find closest one
				if (filter == b) return true;
				if (filter < b && diff > b - filter) {
					closest = b;
					diff = closest - filter;
				} else if (filter > b && diff > filter - b) {
					closest = b;
					diff = filter - closest;
				}
			}
			if (closest != 0 && closest != _filter) {
				notexact = true;
				filter = closest;
				return true;
			} else {
				return false;
			}
		}

		protected void AcceptTuning(ulong frequency, byte mode, byte filter) {
			bool changed = false;
			if (frequency != 0 && frequency != _frequency) { changed = true; _frequency = frequency; }
			if (mode != 0 && mode != _mode) { changed = true; _mode = mode; }
			if (filter != 0 && filter != _filter) { changed = true; _filter = filter; }
			if (changed && TuningChanged != null) TuningChanged(this, new EventArgs());
		}

		protected void AcceptAvailableFilters(byte[] Filters) {
			AcceptAvailableModes(null, Filters);
		}
		protected void AcceptAvailableModes(byte[] Modes, byte[] Filters) {
			if (Modes != null) _capabilities.AvailableModes = Modes;
			if (Filters != null) _capabilities.AvailableFilters = Filters;
			if (AvailableModesChanged != null) AvailableModesChanged(this, new EventArgs());
		}
	}
}
