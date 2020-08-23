﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using UCIS.Util;
using UCIS.VNCServer;
using ThreadingTimer = System.Threading.Timer;

namespace UCIS.FBGUI {
	public abstract class FBGEvent {
	}
	public enum FBGPointingEventType {
		Move,
		ButtonDown,
		ButtonUp,
		VerticalScroll,
	}
	public class FBGPointingEvent : FBGEvent {
		private Point position;
		public Point Position { get { return position; } set { position = value; } }
		public int X { get { return position.X; } set { position.X = value; } }
		public int Y { get { return position.Y; } set { position.Y = value; } }
		public int Delta { get; set; }
		public MouseButtons Buttons { get; private set; }
		public FBGPointingEventType Type { get; private set; }
		public FBGCursor Cursor { get; set; }
		public FBGPointingEvent(Point position, MouseButtons buttons, FBGPointingEventType type) {
			this.Position = position;
			this.Buttons = buttons;
			this.Type = type;
		}
	}
	public class FBGKeyboardEvent : FBGEvent {
		public Keys KeyData { get; private set; }
		public Keys KeyCode { get { return KeyData & Keys.KeyCode; } }
		public Keys Modifiers { get { return KeyData & Keys.Modifiers; } }
		public Boolean Shift { get { return (KeyData & Keys.Shift) != 0; } }
		public Boolean Control { get { return (KeyData & Keys.Control) != 0; } }
		public Boolean Alt { get { return (KeyData & Keys.Alt) != 0; } }
		public Boolean IsDown { get; private set; }
		public Char KeyChar { get; private set; }
		public FBGKeyboardEvent(Keys keyData, Char keyChar, Boolean isDown) {
			this.KeyData = keyData;
			this.KeyChar = keyChar;
			this.IsDown = isDown;
		}
	}
	public class FBGPaintEvent : FBGEvent {
		public Graphics Canvas { get; private set; }
		public FBGPaintEvent(Graphics canvas) {
			this.Canvas = canvas;
		}
	}
	public class FBGKeyboardCaptureEvent : FBGEvent {
		public Boolean Capture { get; set; }
		public FBGKeyboardCaptureEvent(Boolean capture) {
			this.Capture = capture;
		}
	}
	public abstract class FBGMessage {
		public IFBGControl Source { get; private set; }
		protected FBGMessage(IFBGControl source) {
			this.Source = source;
		}
	}
	public class FBGInvalidateMessage : FBGMessage {
		public Rectangle Area { get; set; }
		public FBGInvalidateMessage(IFBGControl source, Rectangle area) : base(source) {
			this.Area = area;
		}
	}
	public class FBGPointingCaptureMessage : FBGMessage {
		public Boolean Capture { get; set; }
		public FBGPointingCaptureMessage(IFBGControl source, Boolean capture) : base(source) {
			this.Capture = capture;
		}
	}
	public class FBGKeyboardCaptureMessage : FBGMessage {
		public Boolean Capture { get; set; }
		public FBGKeyboardCaptureMessage(IFBGControl source, Boolean capture) : base(source) {
			this.Capture = capture;
		}
	}

	public interface IFBGControl {
		Rectangle Bounds { get; set; }
		Boolean Visible { get; set; }
		void HandleEvent(FBGEvent e);
		void Orphaned();
	}
	public interface IFBGContainerControl {
		void AddControl(IFBGControl control);
		Boolean RemoveControl(IFBGControl control);
		void HandleMessage(IFBGControl sender, FBGMessage e);
		Size ClientSize { get; }
	}

	public class FBGControl : IFBGControl {
		private Rectangle bounds = new Rectangle(0, 0, 100, 100);
		private Color backColor = Color.Transparent;
		private Boolean visible = true;
		public virtual IFBGContainerControl Parent { get; private set; }
		public event MouseEventHandler OnMouseDown;
		public event MouseEventHandler OnMouseMove;
		public event MouseEventHandler OnMouseUp;
		public event PaintEventHandler OnPaint;
		public event EventHandler OnResize;
		public event EventHandler OnMove;
		public FBGControl(IFBGContainerControl parent) {
			this.Parent = parent;
			if (Parent != null) Parent.AddControl(this);
		}
		public virtual Rectangle Bounds {
			get { return bounds; }
			set {
				if (bounds == value) return;
				Rectangle old = bounds;
				bounds = value;
				Invalidate(Rectangle.Union(new Rectangle(Point.Empty, value.Size), new Rectangle(old.X - value.X, old.Y - value.Y, old.Width, old.Height)));
				if (value.Location != old.Location) RaiseEvent(OnMove);
				if (value.Size != old.Size) RaiseEvent(OnResize);
			}
		}
		public virtual Boolean Visible {
			get { return visible; }
			set {
				visible = value;
				Parent.HandleMessage(this, new FBGInvalidateMessage(this, new Rectangle(Point.Empty, Bounds.Size)));
			}
		}
		public virtual FBGCursor Cursor { get; set; }
		public Size Size { get { return Bounds.Size; } set { Rectangle r = Bounds; r.Size = value; Bounds = r; } }
		public Point Location { get { return Bounds.Location; } set { Rectangle r = Bounds; r.Location = value; Bounds = r; } }
		public int Left { get { return Bounds.Left; } set { Rectangle r = Bounds; r.X = value; Bounds = r; } }
		public int Top { get { return Bounds.Top; } set { Rectangle r = Bounds; r.Y = value; Bounds = r; } }
		public int Width { get { return Bounds.Width; } set { Rectangle r = Bounds; r.Width = value; Bounds = r; } }
		public int Height { get { return Bounds.Height; } set { Rectangle r = Bounds; r.Height = value; Bounds = r; } }
		public virtual Color BackColor { get { return backColor; } set { if (backColor == value) return; backColor = value; Invalidate(); } }
		public virtual void Invalidate() {
			Invalidate(new Rectangle(Point.Empty, Bounds.Size));
		}
		public virtual void Invalidate(Rectangle rect) {
			if (!Visible) return;
			Parent.HandleMessage(this, new FBGInvalidateMessage(this, rect));
		}
		void IFBGControl.HandleEvent(FBGEvent e) {
			HandleEvent(e);
		}
		protected virtual void HandleEvent(FBGEvent e) {
			if (e is FBGPaintEvent) HandlePaintEvent((FBGPaintEvent)e);
			else if (e is FBGPointingEvent) HandlePointingEvent((FBGPointingEvent)e);
			else if (e is FBGKeyboardEvent) HandleKeyboardEvent((FBGKeyboardEvent)e);
			else if (e is FBGKeyboardCaptureEvent) HandleKeyboardCaptureEvent((FBGKeyboardCaptureEvent)e);
		}
		protected virtual void HandlePaintEvent(FBGPaintEvent e) {
			Paint(e.Canvas);
		}
		protected virtual void HandlePointingEvent(FBGPointingEvent e) {
			if (Cursor != null) e.Cursor = Cursor;
			switch (e.Type) {
				case FBGPointingEventType.Move: MouseMove(e.Position, e.Buttons); break;
				case FBGPointingEventType.ButtonDown: MouseDown(e.Position, e.Buttons); break;
				case FBGPointingEventType.ButtonUp: MouseUp(e.Position, e.Buttons); break;
			}
		}
		protected virtual void HandleKeyboardEvent(FBGKeyboardEvent e) {
			if (e.IsDown) {
				if (e.KeyData != Keys.None) KeyDown(e.KeyData);
				if (e.KeyChar != Char.MinValue) KeyPress(e.KeyChar);
			} else {
				if (e.KeyData != Keys.None) KeyUp(e.KeyData);
			}
		}
		protected virtual void HandleKeyboardCaptureEvent(FBGKeyboardCaptureEvent e) {
			if (!e.Capture) LostKeyboardCapture();
		}
		void IFBGControl.Orphaned() { Orphaned(); }
		protected virtual void Paint(Graphics g) {
			if (!visible) return;
			if (backColor.A == 0xff) g.Clear(backColor);
			else if (backColor.A != 0) using (Brush brush = new SolidBrush(backColor)) g.FillRectangle(brush, g.ClipBounds);
			RaiseEvent(OnPaint, new PaintEventArgs(g, Rectangle.Round(g.ClipBounds)));
		}
		protected virtual void MouseMove(Point position, MouseButtons buttons) { RaiseEvent(OnMouseMove, new MouseEventArgs(buttons, 0, position.X, position.Y, 0)); }
		protected virtual void MouseDown(Point position, MouseButtons buttons) { RaiseEvent(OnMouseDown, new MouseEventArgs(buttons, 1, position.X, position.Y, 0)); }
		protected virtual void MouseUp(Point position, MouseButtons buttons) { RaiseEvent(OnMouseUp, new MouseEventArgs(buttons, 1, position.X, position.Y, 0)); }
		protected virtual Boolean CaptureMouse(Boolean capture) {
			FBGPointingCaptureMessage m = new FBGPointingCaptureMessage(this, capture);
			Parent.HandleMessage(this, m);
			return capture == m.Capture;
		}
		protected virtual void KeyDown(Keys key) { }
		protected virtual void KeyPress(Char keyChar) { }
		protected virtual void KeyUp(Keys key) { }
		protected virtual Boolean CaptureKeyboard(Boolean capture) {
			FBGKeyboardCaptureMessage m = new FBGKeyboardCaptureMessage(this, capture);
			Parent.HandleMessage(this, m);
			return capture == m.Capture;
		}
		protected virtual void LostKeyboardCapture() { }
		protected virtual void Orphaned() {
			//IDisposable disp = this as IDisposable;
			//if (!ReferenceEquals(disp, null)) disp.Dispose();
		}
		protected void RaiseEvent(KeyEventHandler eh, KeyEventArgs ea) { if (eh != null) eh(this, ea); }
		protected void RaiseEvent(KeyPressEventHandler eh, KeyPressEventArgs ea) { if (eh != null) eh(this, ea); }
		protected void RaiseEvent(MouseEventHandler eh, MouseEventArgs ea) { if (eh != null) eh(this, ea); }
		protected void RaiseEvent(PaintEventHandler eh, PaintEventArgs ea) { if (eh != null) eh(this, ea); }
		protected void RaiseEvent<T>(EventHandler<T> eh, T ea) where T : EventArgs { if (eh != null) eh(this, ea); }
		protected void RaiseEvent(EventHandler eh, EventArgs ea) { if (eh != null) eh(this, ea); }
		protected void RaiseEvent(EventHandler eh) { if (eh != null) eh(this, new EventArgs()); }
	}
	public class FBGContainerControl : FBGControl, IFBGContainerControl, IList<IFBGControl> {
		protected IFBGControl[] controls = new IFBGControl[0];
		protected IFBGControl mouseCaptureControl = null;
		protected IFBGControl keyboardCaptureControl = null;
		public virtual Rectangle ClientRectangle { get { return new Rectangle(Point.Empty, Bounds.Size); } }
		public virtual Size ClientSize { get { return ClientRectangle.Size; } set { Bounds = new Rectangle(Bounds.Location, Bounds.Size - ClientRectangle.Size + value); } }
		public FBGContainerControl(IFBGContainerControl parent) : base(parent) { }
		void IFBGContainerControl.AddControl(IFBGControl control) { AddControl(control); }
		protected virtual void AddControl(IFBGControl control) {
			ArrayUtil.Add(ref controls, control);
			if (control.Visible) Invalidate(control);
		}
		public virtual Boolean RemoveControl(IFBGControl control) {
			if (!ArrayUtil.Remove(ref controls, control)) return false;
			if (control.Visible) Invalidate(control);
			HandleMessage(control, new FBGPointingCaptureMessage(control, false));
			HandleMessage(control, new FBGKeyboardCaptureMessage(control, false));
			control.Orphaned();
			return true;
		}
		public virtual void RemoveAllControls() {
			IFBGControl[] c = Interlocked.Exchange(ref controls, new IFBGControl[0]);
			foreach (IFBGControl control in c) {
				HandleMessage(control, new FBGPointingCaptureMessage(control, false));
				HandleMessage(control, new FBGKeyboardCaptureMessage(control, false));
				control.Orphaned();
			}
			Invalidate();
		}
		public IList<IFBGControl> Controls { get { return this; } }
		public virtual Point PointToChild(IFBGControl child, Point point) {
			return point - (Size)child.Bounds.Location - (Size)ClientRectangle.Location;
		}
		public virtual Point PointFromChild(IFBGControl child, Point point) {
			return point + (Size)child.Bounds.Location + (Size)ClientRectangle.Location;
		}
		public virtual void BringControlToFront(IFBGControl control) {
			if (controls.Length == 0) return;
			int oldindex = Array.IndexOf(controls, control);
			if (oldindex == -1 || oldindex == controls.Length - 1) return;
			for (int i = oldindex; i < controls.Length - 1; i++) controls[i] = controls[i + 1];
			controls[controls.Length - 1] = control;
			if (control.Visible) Invalidate(control);
		}
		public virtual void Invalidate(IFBGControl control) {
			Invalidate(new Rectangle(PointFromChild(control, Point.Empty), control.Bounds.Size));
		}
		public virtual void Invalidate(IFBGControl control, Rectangle rect) {
			Invalidate(new Rectangle(PointFromChild(control, rect.Location), rect.Size));
		}
		protected override void HandlePaintEvent(FBGPaintEvent e) {
			base.HandlePaintEvent(e);
			GraphicsState state2 = null;
			Graphics g = e.Canvas;
			Rectangle childarea = ClientRectangle;
			if (childarea.X != 0 || childarea.Y != 0 || childarea.Width != Bounds.Width || childarea.Height != Bounds.Height) {
				state2 = g.Save();
				g.TranslateTransform(childarea.X, childarea.Y, MatrixOrder.Append);
				g.IntersectClip(new Rectangle(Point.Empty, childarea.Size));
			}
			foreach (IFBGControl control in controls) {
				if (!control.Visible) continue;
				if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0) continue;
				if (!g.ClipBounds.IntersectsWith((RectangleF)control.Bounds)) continue;
				GraphicsState state = g.Save();
				g.TranslateTransform(control.Bounds.X, control.Bounds.Y, MatrixOrder.Append);
				g.IntersectClip(new Rectangle(Point.Empty, control.Bounds.Size));
				control.HandleEvent(e);
				g.Restore(state);
			}
			if (state2 != null) g.Restore(state2);
		}
		public IFBGControl FindControlAtPosition(Point p) {
			Rectangle childarea = ClientRectangle;
			if (!childarea.Contains(p)) return null;
			p.Offset(-childarea.X, -childarea.Y);
			return Array.FindLast(controls, delegate(IFBGControl control) { return control.Visible && control.Bounds.Contains(p); });
		}
		protected override void HandlePointingEvent(FBGPointingEvent e) {
			IFBGControl control = mouseCaptureControl != null ? mouseCaptureControl : FindControlAtPosition(e.Position);
			if (control == null) {
				base.HandlePointingEvent(e);
			} else {
				if (Cursor != null) e.Cursor = Cursor;
				e.Position = PointToChild(control, e.Position);
				control.HandleEvent(e);
			}
		}
		void IFBGContainerControl.HandleMessage(IFBGControl sender, FBGMessage e) {
			HandleMessage(sender, e);
		}
		protected virtual void HandleMessage(IFBGControl sender, FBGMessage e) {
			if (e is FBGPointingCaptureMessage) HandlePointingCaptureMessage(sender, (FBGPointingCaptureMessage)e);
			else if (e is FBGKeyboardCaptureMessage) HandleKeyboardCaptureMessage(sender, (FBGKeyboardCaptureMessage)e);
			else if (e is FBGInvalidateMessage) HandleInvalidateMessage(sender, (FBGInvalidateMessage)e);
		}
		protected virtual void HandleInvalidateMessage(IFBGControl sender, FBGInvalidateMessage e) {
			e.Area = new Rectangle(PointFromChild(sender, e.Area.Location), e.Area.Size);
			Parent.HandleMessage(this, e);
		}
		protected virtual void HandlePointingCaptureMessage(IFBGControl sender, FBGPointingCaptureMessage e) {
			if (e.Capture && !(ReferenceEquals(mouseCaptureControl, null) || ReferenceEquals(mouseCaptureControl, sender))) e.Capture = false;
			else if (!e.Capture && !ReferenceEquals(mouseCaptureControl, sender)) e.Capture = false;
			else {
				Parent.HandleMessage(this, e);
				mouseCaptureControl = e.Capture ? sender : null;
			}
		}
		protected override void HandleKeyboardEvent(FBGKeyboardEvent e) {
			if (ReferenceEquals(keyboardCaptureControl, null)) base.HandleKeyboardEvent(e);
			else keyboardCaptureControl.HandleEvent(e);
		}
		protected virtual void HandleKeyboardCaptureMessage(IFBGControl sender, FBGKeyboardCaptureMessage e) {
			if (!e.Capture && !(ReferenceEquals(keyboardCaptureControl, null) || ReferenceEquals(keyboardCaptureControl, sender))) e.Capture = false;
			else {
				Parent.HandleMessage(this, e);
				IFBGControl prev = keyboardCaptureControl;
				keyboardCaptureControl = e.Capture ? sender : null;
				if (prev != null && prev != sender) prev.HandleEvent(new FBGKeyboardCaptureEvent(false));
			}
		}
		protected override void HandleKeyboardCaptureEvent(FBGKeyboardCaptureEvent e) {
			if (keyboardCaptureControl != null) keyboardCaptureControl.HandleEvent(new FBGKeyboardCaptureEvent(false));
			base.HandleKeyboardCaptureEvent(e);
		}
		protected override void Orphaned() {
			IFBGControl[] c = Interlocked.Exchange(ref controls, new IFBGControl[0]);
			foreach (IFBGControl control in c) control.Orphaned();
			mouseCaptureControl = null;
			keyboardCaptureControl = null;
			base.Orphaned();
		}

		#region IList<IFBGControl> Members
		int IList<IFBGControl>.IndexOf(IFBGControl item) { return Array.IndexOf(controls, item); }
		void IList<IFBGControl>.Insert(int index, IFBGControl item) { throw new NotSupportedException(); }
		void IList<IFBGControl>.RemoveAt(int index) { RemoveControl(controls[index]); }
		IFBGControl IList<IFBGControl>.this[int index] {
			get { return controls[index]; }
			set { throw new NotSupportedException(); }
		}
		void ICollection<IFBGControl>.Add(IFBGControl item) { throw new NotSupportedException(); }
		void ICollection<IFBGControl>.Clear() { RemoveAllControls(); }
		bool ICollection<IFBGControl>.Contains(IFBGControl item) { return Array.IndexOf(controls, item) != -1; }
		void ICollection<IFBGControl>.CopyTo(IFBGControl[] array, int arrayIndex) { controls.CopyTo(array, arrayIndex); }
		int ICollection<IFBGControl>.Count { get { return controls.Length; } }
		bool ICollection<IFBGControl>.IsReadOnly { get { return false; } }
		bool ICollection<IFBGControl>.Remove(IFBGControl item) { return RemoveControl(item); }
		IEnumerator<IFBGControl> IEnumerable<IFBGControl>.GetEnumerator() { return ((IEnumerable<IFBGControl>)controls).GetEnumerator(); }
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return controls.GetEnumerator(); }
		#endregion
	}
	public class FBGDockContainer : FBGContainerControl {
		private Dictionary<IFBGControl, DockStyle> dockStyles = new Dictionary<IFBGControl, DockStyle>();
		private Dictionary<IFBGControl, AnchorStyles> anchorStyles = new Dictionary<IFBGControl, AnchorStyles>();
		private Rectangle oldBounds;
		public FBGDockContainer(IFBGContainerControl parent)
			: base(parent) {
			oldBounds = ClientRectangle.IsEmpty ? Bounds : new Rectangle(Bounds.Location + (Size)ClientRectangle.Location, ClientRectangle.Size);
		}
		protected override void AddControl(IFBGControl control) {
			base.AddControl(control);
		}
		public override void BringControlToFront(IFBGControl control) {
			base.BringControlToFront(control);
			if (dockStyles.ContainsKey(control)) DoLayout();
		}
		public override Boolean RemoveControl(IFBGControl control) {
			if (!base.RemoveControl(control)) return false;
			if (dockStyles.Remove(control)) DoLayout();
			return true;
		}
		public override void RemoveAllControls() {
			base.RemoveAllControls();
			dockStyles.Clear();
		}
		public override Rectangle Bounds {
			get { return base.Bounds; }
			set {
				base.Bounds = value;
				DoLayout();
				Rectangle newBounds = ClientRectangle.IsEmpty ? Bounds : new Rectangle(Bounds.Location + (Size)ClientRectangle.Location, ClientRectangle.Size);
				foreach (KeyValuePair<IFBGControl, AnchorStyles> c in anchorStyles) {
					Rectangle b = c.Key.Bounds;
					if ((c.Value & AnchorStyles.Right) != 0) {
						if ((c.Value & AnchorStyles.Left) == 0) b.X += newBounds.Width - oldBounds.Width;
						else b.Width += newBounds.Width - oldBounds.Width;
					} else if ((c.Value & AnchorStyles.Left) == 0) b.X += newBounds.X - oldBounds.X;
					if ((c.Value & AnchorStyles.Bottom) != 0) {
						if ((c.Value & AnchorStyles.Top) == 0) b.Y += newBounds.Height - oldBounds.Height;
						else b.Height += newBounds.Height - oldBounds.Height;
					} else if ((c.Value & AnchorStyles.Top) == 0) b.Y += newBounds.Y - oldBounds.Y;
					c.Key.Bounds = b;
				}
				oldBounds = newBounds;
			}
		}
		public DockStyle GetDockStyle(IFBGControl control) {
			DockStyle ds;
			if (!dockStyles.TryGetValue(control, out ds)) ds = DockStyle.None;
			return ds;
		}
		public void SetDockStyle(IFBGControl control, DockStyle style) {
			if (style == DockStyle.None) {
				if (dockStyles.Remove(control)) DoLayout();
			} else if (Array.IndexOf(controls, control) != -1) {
				anchorStyles.Remove(control);
				dockStyles[control] = style;
				DoLayout();
			}
		}
		public AnchorStyles GetAnchorStyle(IFBGControl control) {
			AnchorStyles ds;
			if (!anchorStyles.TryGetValue(control, out ds)) ds = AnchorStyles.Left | AnchorStyles.Top;
			return ds;
		}
		public void SetAnchorStyle(IFBGControl control, AnchorStyles style) {
			if (style == (AnchorStyles.Left | AnchorStyles.Top)) {
				anchorStyles.Remove(control);
			} else if (Array.IndexOf(controls, control) != -1) {
				dockStyles.Remove(control);
				anchorStyles[control] = style;
			}
		}
		public void SetAnchor(IFBGControl control, AnchorStyles style, int value) {
			if (Array.IndexOf(controls, control) != -1) {
				AnchorStyles oldstyle;
				if (!anchorStyles.TryGetValue(control, out oldstyle)) oldstyle = AnchorStyles.Left | AnchorStyles.Top;
				Rectangle b = control.Bounds;
				switch (style) {
					case AnchorStyles.None: throw new ArgumentException("style", "Anchor style can not be None");
					case AnchorStyles.Left: b.X = value; break;
					case AnchorStyles.Top: b.Y = value; break;
					case AnchorStyles.Right:
						if ((oldstyle & AnchorStyles.Left) == 0) b.X = ClientRectangle.Width - b.Width - value;
						else b.Width = ClientRectangle.Width - b.X - value;
						break;
					case AnchorStyles.Bottom:
						if ((oldstyle & AnchorStyles.Top) == 0) b.Y = ClientRectangle.Height - b.Height - value;
						else b.Height = ClientRectangle.Height - b.Y - value; 
						break;
					default: throw new ArgumentOutOfRangeException("style", "The value vor the style argument is invalid");
				}
				control.Bounds = b;
				dockStyles.Remove(control);
				anchorStyles[control] = oldstyle | style;
			}
		}
		private void DoLayout() {
			Rectangle a = new Rectangle(Point.Empty, ClientRectangle.IsEmpty ? Bounds.Size : ClientRectangle.Size);
			foreach (KeyValuePair<IFBGControl, DockStyle> c in dockStyles) {
				Rectangle b = c.Key.Bounds;
				if (c.Value == DockStyle.Left) {
					b.Location = a.Location;
					b.Height = a.Height;
					a.X += b.Width;
					a.Width -= b.Width;
				} else if (c.Value == DockStyle.Top) {
					b.Location = a.Location;
					b.Width = a.Width;
					a.Y += b.Height;
					a.Height -= b.Height;
				} else if (c.Value == DockStyle.Right) {
					b.X = a.X + a.Width - b.Width;
					b.Y = a.Y;
					b.Height = a.Height;
					a.Width -= b.Width;
				} else if (c.Value == DockStyle.Bottom) {
					b.X = a.X;
					b.Y = a.Y + a.Height - b.Height;
					b.Width = a.Width;
					a.Height -= b.Height;
				} else if (c.Value == DockStyle.Fill) {
					b = a;
				}
				c.Key.Bounds = b;
				if (a.Width < 0) a.Width = 0;
				if (a.Height < 0) a.Height = 0;
			}
		}
	}
	public class FBGGroupBox : FBGDockContainer {
		private String text = String.Empty;
		public override Rectangle ClientRectangle { get { return new Rectangle(1, 15, Bounds.Width - 2, Bounds.Height - 17); } }
		public FBGGroupBox(IFBGContainerControl parent) : base(parent) { }
		public String Text { get { return text; } set { if (text == value) return; text = value; Invalidate(new Rectangle(0, 0, Bounds.Width, 15)); } }
		protected override void Paint(Graphics g) {
			base.Paint(g);
			SizeF ss = g.MeasureString(Text, SystemFonts.DefaultFont, new Size(Bounds.Width - 10 - 9, 15));
			g.DrawLines(Pens.Gray, new PointF[] { new PointF(8 + ss.Width + 4, 6), new Point(Bounds.Width - 1, 6), new Point(Bounds.Width - 1, Bounds.Height - 1), new Point(0, Bounds.Height - 1), new Point(0, 6), new Point(8, 6) });
			g.DrawString(Text, SystemFonts.DefaultFont, Brushes.DarkBlue, new Rectangle(9, 0, Bounds.Width - 10 - 9, 15));
		}
	}
	[System.ComponentModel.DesignerCategory("")]
	public class WinFormsFBGHost : Control, IFBGContainerControl {
		private IFBGControl childControl = null;
		private IFBGControl mouseCaptureControl = null;
		private IFBGControl keyboardCaptureControl = null;
		public IFBGControl ChildControl { get { return childControl; } }

		public WinFormsFBGHost() {
			DoubleBuffered = true;
		}
		
		public virtual Point PointToChild(IFBGControl child, Point point) {
			return point - (Size)child.Bounds.Location;
		}
		public virtual Point PointFromChild(IFBGControl child, Point point) {
			return point + (Size)child.Bounds.Location;
		}

		void IFBGContainerControl.AddControl(IFBGControl control) {
			if (!ReferenceEquals(childControl, null)) throw new InvalidOperationException("This container can have only one child control");
			childControl = control;
			control.Bounds = new Rectangle(Point.Empty, ClientSize);
			Invalidate(control.Bounds);
		}
		Boolean IFBGContainerControl.RemoveControl(IFBGControl control) {
			if (!ReferenceEquals(childControl, control)) return false;
			childControl = null;
			Invalidate(control.Bounds);
			if (mouseCaptureControl == control) mouseCaptureControl = null;
			if (keyboardCaptureControl == control) control = null;
			control.Orphaned();
			return true;
		}
		void IFBGContainerControl.HandleMessage(IFBGControl sender, FBGMessage e) {
			if (e is FBGInvalidateMessage) {
				FBGInvalidateMessage p = (FBGInvalidateMessage)e;
				Invalidate(new Rectangle(PointFromChild(sender, p.Area.Location), p.Area.Size));
			} else if (e is FBGPointingCaptureMessage) {
				FBGPointingCaptureMessage p = (FBGPointingCaptureMessage)e;
				if (p.Capture && !(ReferenceEquals(mouseCaptureControl, null) || ReferenceEquals(mouseCaptureControl, sender))) p.Capture = false;
				else if (!p.Capture && !ReferenceEquals(mouseCaptureControl, sender)) p.Capture = false;
				else mouseCaptureControl = p.Capture ? sender : null;
			} else if (e is FBGKeyboardCaptureMessage) {
				FBGKeyboardCaptureMessage p = (FBGKeyboardCaptureMessage)e;
				if (!p.Capture && !ReferenceEquals(keyboardCaptureControl, sender)) p.Capture = false;
				else keyboardCaptureControl = p.Capture ? sender : null;
			}
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			Graphics g = e.Graphics;
			GraphicsState state = g.Save();
			g.SetClip(e.ClipRectangle);
			if (ReferenceEquals(childControl, null)) return;
			if (childControl.Bounds.Width <= 0 || childControl.Bounds.Height <= 0) return;
			if (!g.ClipBounds.IntersectsWith((RectangleF)childControl.Bounds)) return;
			g.TranslateTransform(childControl.Bounds.X, childControl.Bounds.Y, MatrixOrder.Append);
			g.IntersectClip(new Rectangle(Point.Empty, childControl.Bounds.Size));
			childControl.HandleEvent(new FBGPaintEvent(g));
			g.Restore(state);
		}
		protected override void OnResize(EventArgs e) {
			if (!ReferenceEquals(childControl, null)) childControl.Bounds = new Rectangle(Point.Empty, ClientSize);
			base.OnResize(e);
		}
		void DispatchMouseEvent(MouseEventArgs e, FBGPointingEventType type) {
			IFBGControl control = mouseCaptureControl != null ? mouseCaptureControl : childControl;
			if (control != null) control.HandleEvent(new FBGPointingEvent(e.Location, e.Button, type));
		}
		protected override void OnMouseDown(MouseEventArgs e) {
			base.OnMouseDown(e);
			DispatchMouseEvent(e, FBGPointingEventType.ButtonDown);
		}
		protected override void OnMouseUp(MouseEventArgs e) {
			base.OnMouseUp(e);
			DispatchMouseEvent(e, FBGPointingEventType.ButtonUp);
		}
		protected override void OnMouseMove(MouseEventArgs e) {
			DispatchMouseEvent(e, FBGPointingEventType.Move);
		}
		protected override bool IsInputChar(char charCode) {
			return true;
		}
		protected override bool IsInputKey(Keys keyData) {
			return true;
		}
		protected override void OnKeyDown(KeyEventArgs e) {
			//base.OnKeyDown(e);
			if (!ReferenceEquals(keyboardCaptureControl, null)) keyboardCaptureControl.HandleEvent(new FBGKeyboardEvent(e.KeyData, Char.MinValue, true));
		}
		protected override void OnKeyPress(KeyPressEventArgs e) {
			//base.OnKeyPress(e);
			if (!ReferenceEquals(keyboardCaptureControl, null)) keyboardCaptureControl.HandleEvent(new FBGKeyboardEvent(Keys.None, e.KeyChar, true));
		}
		protected override void OnKeyUp(KeyEventArgs e) {
			//base.OnKeyUp(e);
			if (!ReferenceEquals(keyboardCaptureControl, null)) keyboardCaptureControl.HandleEvent(new FBGKeyboardEvent(e.KeyData, Char.MinValue, false));
		}
		protected override void OnHandleDestroyed(EventArgs e) {
			if (!ReferenceEquals(childControl, null)) childControl.Orphaned();
			base.OnHandleDestroyed(e);
		}
	}
	public class FBGCursor {
		public Image Image { get; private set; }
		public Point Hotspot { get; private set; }
		public Size Size { get; private set; }
		public FBGCursor(Image image, Point hotspot) {
			this.Image = image;
			this.Hotspot = hotspot;
			this.Size = image == null ? Size.Empty : image.Size;
		}
		public Rectangle Area { get { return new Rectangle(-Hotspot.X, -Hotspot.Y, Size.Width, Size.Height); } }
		public FBGCursor RotateFlip(RotateFlipType type) {
			if (Image == null) return this;
			Image img = new Bitmap(Image);
			img.RotateFlip(type);
			Point hs = Hotspot;
			switch (type) {
				case RotateFlipType.RotateNoneFlipNone: break;
				case RotateFlipType.Rotate90FlipNone: hs = new Point(img.Width - hs.Y, hs.X); break;
				case RotateFlipType.Rotate180FlipNone: hs = new Point(img.Width - hs.X, img.Height - hs.Y); break;
				case RotateFlipType.Rotate270FlipNone: hs = new Point(hs.Y, img.Height - hs.X); break;
				case RotateFlipType.RotateNoneFlipX: hs.X = img.Width - hs.X; break;
				case RotateFlipType.Rotate90FlipX: hs = new Point(hs.Y, hs.X); break;
				case RotateFlipType.RotateNoneFlipY: hs.Y = img.Height - hs.Y; break;
				case RotateFlipType.Rotate90FlipY: hs = new Point(img.Width - hs.Y, img.Height - hs.X); break;
			}
			return new FBGCursor(img, hs);
		}
		public static FBGCursor FromBase64Image(String data, Point hotspot) {
			return new FBGCursor(Image.FromStream(new MemoryStream(Convert.FromBase64String(data))), hotspot);
		}
		private static FBGCursor LoadFromResource(String name, int hotX, int hotY) {
			using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("UCIS.FBGUI." + name + ".png")) {
				try {
					return new FBGCursor(Image.FromStream(s), new Point(hotX, hotY));
				} catch (ArgumentException) {
					Console.Error.WriteLine("FBGUI: Failed to load cursor " + name);
					return new FBGCursor(null, Point.Empty);
				}
			}
		}

		public static readonly FBGCursor None = new FBGCursor(null, Point.Empty);
		public static readonly FBGCursor Arrow = LoadFromResource("cursor_arrow", 0, 0);
		public static readonly FBGCursor Move = LoadFromResource("cursor_move", 8, 8);
		public static readonly FBGCursor SizeLeft = LoadFromResource("cursor_left", 1, 10);
		public static readonly FBGCursor SizeRight = SizeLeft.RotateFlip(RotateFlipType.RotateNoneFlipX);
		public static readonly FBGCursor SizeTop = SizeLeft.RotateFlip(RotateFlipType.Rotate90FlipNone);
		public static readonly FBGCursor SizeBottom = SizeLeft.RotateFlip(RotateFlipType.Rotate90FlipY);
		public static readonly FBGCursor SizeTopLeft = LoadFromResource("cursor_topleft", 1, 1);
		public static readonly FBGCursor SizeTopRight = SizeTopLeft.RotateFlip(RotateFlipType.RotateNoneFlipX);
		public static readonly FBGCursor SizeBottomLeft = SizeTopLeft.RotateFlip(RotateFlipType.RotateNoneFlipY);
		public static readonly FBGCursor SizeBottomRight = SizeTopLeft.RotateFlip(RotateFlipType.RotateNoneFlipXY);
		public static readonly FBGCursor Hand = LoadFromResource("cursor_hand", 5, 0);
		public static readonly FBGCursor IBeam = LoadFromResource("cursor_ibeam", 3, 7);
		public static FBGCursor ArrowCursor { get { return Arrow; } }
	}
	public class FBGRenderer : FBGContainerControl, IDisposable {
		private FBGCursor cursor = null;
		private Point cursorposition = Point.Empty;
		public IFramebuffer Framebuffer { get; private set; }
		private Bitmap Frontbuffer = null;
		protected Object RenderLock = new object();
		private Object StatusLock = new object();
		public event EventHandler<InvalidateEventArgs> Painted;
		protected Size size = Size.Empty;
		private ThreadingTimer PaintTimer = null;
		private Boolean PaintScheduled = false;
		private int PaintDelay = 0;
		public Boolean SuspendDrawing {
			get { return suspenddrawing; }
			set {
				lock (StatusLock) suspenddrawing = value;
				if (value) return;
				lock (RenderLock) {
					Refresh(DirtyRectangle);
					DirtyRectangle = Rectangle.Empty;
				}
			}
		}
		public int RedrawDelay {
			get { return PaintDelay; }
			set {
				lock (StatusLock) {
					if (value < 0) throw new ArgumentOutOfRangeException("value");
					PaintDelay = value;
					if (PaintDelay == 0) {
						if (PaintTimer != null) PaintTimer.Dispose();
						PaintTimer = null;
					} else {
						PaintTimer = new ThreadingTimer(PaintTimerCallback);
					}
				}
				if (PaintScheduled) PaintTimerCallback(null);
			}
		}
		private Boolean suspenddrawing = false;
		private Rectangle DirtyRectangle;

		public Point CursorPosition {
			get { return cursorposition; }
			set { UpdateCursor(value, cursor); }
		}
		protected void UpdateCursor(Point position, FBGCursor cursor) {
			if (cursorposition == position && cursor == this.cursor) return;
			Rectangle r1 = Rectangle.Empty;
			if (this.cursor != null) {
				r1 = this.cursor.Area;
				r1.Offset(cursorposition);
			}
			if (cursor != null) {
				Rectangle r2 = cursor.Area;
				r2.Offset(position);
				r1 = r1.IsEmpty ? r2 : Rectangle.Union(r1, r2);
			}
			this.cursor = cursor;
			cursorposition = position;
			Invalidate(r1);
		}
		public override Rectangle Bounds {
			get { return new Rectangle(Point.Empty, size); }
			set { throw new NotSupportedException("Can not change the top control bounds"); }
		}
		public FBGRenderer(IFramebuffer fb) : this(fb.Width, fb.Height) {
			Framebuffer = fb;
		}
		public FBGRenderer(Size fbsize) : this(fbsize.Width, fbsize.Height) { }
		public FBGRenderer(int width, int height) : this(new Bitmap(width, height, PixelFormat.Format32bppRgb)) { }
		public FBGRenderer(Bitmap bmp) : base(null) {
			Frontbuffer = bmp;
			BackColor = SystemColors.Control;
			size = Frontbuffer.Size;
		}
		protected FBGRenderer() : base(null) {
			BackColor = SystemColors.Control;
		}
		public void Resize(Size fbsize) {
			if (fbsize == size) return;
			lock (RenderLock) {
				Frontbuffer = new Bitmap(fbsize.Width, fbsize.Height, PixelFormat.Format32bppRgb);
				size = Frontbuffer.Size;
			}
			base.Bounds = new Rectangle(Point.Empty, size);
		}
		protected override void HandleInvalidateMessage(IFBGControl sender, FBGInvalidateMessage e) {
			e.Area = new Rectangle(PointFromChild(sender, e.Area.Location), e.Area.Size);
			Invalidate(e.Area);
		}
		public override void Invalidate(Rectangle rect) {
			if (rect.Width == 0 || rect.Height == 0) return;
			Boolean drawDirect = false;
			lock (StatusLock) {
				if (SuspendDrawing || PaintTimer != null) {
					DirtyRectangle = DirtyRectangle.IsEmpty ? rect : Rectangle.Union(DirtyRectangle, rect);
					if (!SuspendDrawing && !PaintScheduled) {
						PaintScheduled = true;
						PaintTimer.Change(PaintDelay, Timeout.Infinite);
					}
				} else {
					drawDirect = true;
				}
			}
			if (drawDirect) Refresh(rect);
		}
		private void PaintTimerCallback(Object state) {
			try {
				Rectangle rect;
				lock (StatusLock) {
					PaintScheduled = false;
					rect = DirtyRectangle;
					DirtyRectangle = Rectangle.Empty;
				}
				Refresh(rect);
			} catch (Exception ex) {
				Debug.WriteLine(ex);
				Console.Error.WriteLine(ex);
			}
		}
		protected virtual void Refresh(Rectangle rect) {
			lock (RenderLock) {
				rect.Intersect(Bounds);
				if (rect.Width == 0 || rect.Height == 0) return;
				if (Frontbuffer != null) {
					using (Graphics g = Graphics.FromImage(Frontbuffer)) {
						g.SetClip(rect);
						Paint(g);
					}
					if (Framebuffer != null) Framebuffer.DrawImage(Frontbuffer, rect, rect.Location);
				}
				RaiseEvent(Painted, new InvalidateEventArgs(rect));
			}
		}
		public virtual new void Paint(Graphics g) {
			HandleEvent(new FBGPaintEvent(g));
			if (cursor != null && cursor.Image != null) {
				Rectangle r = cursor.Area;
				r.Offset(cursorposition);
				g.DrawImage(cursor.Image, r);
			}
		}
		protected override void HandlePointingCaptureMessage(IFBGControl sender, FBGPointingCaptureMessage e) {
			if (e.Capture && !(ReferenceEquals(mouseCaptureControl, null) || ReferenceEquals(mouseCaptureControl, sender))) e.Capture = false;
			else if (!e.Capture && !ReferenceEquals(mouseCaptureControl, sender)) e.Capture = false;
			else mouseCaptureControl = e.Capture ? sender : null;
		}
		protected override void HandleKeyboardCaptureMessage(IFBGControl sender, FBGKeyboardCaptureMessage e) {
			if (!e.Capture && !ReferenceEquals(keyboardCaptureControl, sender)) e.Capture = false;
			else {
				IFBGControl prev = keyboardCaptureControl;
				keyboardCaptureControl = e.Capture ? sender : null;
				if (prev != null && prev != sender) prev.HandleEvent(new FBGKeyboardCaptureEvent(false));
			}
		}

		protected override Boolean CaptureMouse(Boolean capture) {
			return true;
		}
		protected override Boolean CaptureKeyboard(bool capture) {
			return true;
		}
		public virtual void Dispose() {
			lock (StatusLock) {
				if (PaintTimer != null) PaintTimer.Dispose();
				PaintTimer = null;
			}
			Orphaned();
			lock (RenderLock) {
				if (Frontbuffer != null) Frontbuffer.Dispose();
				Frontbuffer = null;
			}
		}
		public Bitmap LockBitmapBuffer() {
			Monitor.Enter(RenderLock);
			return Frontbuffer;
		}
		public void UnlockBitmapBuffer() {
			Monitor.Exit(RenderLock);
		}
		void DispatchPointingEvent(Point position, MouseButtons buttons, FBGPointingEventType type, int delta) {
			FBGPointingEvent e = new FBGPointingEvent(position, buttons, type) { Delta = delta };
			HandleEvent(e);
			UpdateCursor(cursorposition, e.Cursor);
		}
		public new void MouseMove(Point position, MouseButtons buttons) { DispatchPointingEvent(position, buttons, FBGPointingEventType.Move, 0); }
		public new void MouseDown(Point position, MouseButtons buttons) { DispatchPointingEvent(position, buttons, FBGPointingEventType.ButtonDown, 0); }
		public new void MouseUp(Point position, MouseButtons buttons) { DispatchPointingEvent(position, buttons, FBGPointingEventType.ButtonUp, 0); }
		public new void MouseWheel(Point position, MouseButtons buttons, int delta) { DispatchPointingEvent(position, buttons, FBGPointingEventType.VerticalScroll, delta); }
		public new void KeyDown(Keys key) { KeyDown(key, Char.MinValue); }
		public new void KeyPress(Char key) { KeyDown(Keys.None, key); }
		public new void KeyUp(Keys key) { KeyUp(key, Char.MinValue); }
		public void KeyDown(Keys key, Char keyChar) { HandleEvent(new FBGKeyboardEvent(key, keyChar, true)); }
		public void KeyUp(Keys key, Char keyChar) { HandleEvent(new FBGKeyboardEvent(key, keyChar, false)); }
	}
	public class FBGForm : FBGDockContainer {
		private Point prevPosition = Point.Empty;
		private NonClientOps moveresize = 0;
		private String text = String.Empty;
		public event EventHandler TextChanged;
		public event EventHandler Closed;
		public Boolean Sizable { get; set; }
		public Boolean Movable { get; set; }
		public Boolean Closable { get; set; }
		const int menubar_height = 30;
		const int menubar_btnsize = 24;
		NonClientOps selectedNonClientOp = NonClientOps.None;
		[Flags]
		private enum NonClientOps : int {
			None = 0,
			Move = 1,
			ResizeLeft = 2,
			ResizeRight = 4,
			ResizeTop = 8,
			ResizeBottom = 16,
			ButtonClose = 32,
			MoveResize = ResizeLeft | ResizeRight | ResizeBottom | ResizeTop | Move,
		}
		public override Rectangle ClientRectangle { get { return new Rectangle(4, menubar_height + 2, Bounds.Width - 8, Bounds.Height - menubar_height - 4); } }
		public FBGForm(IFBGContainerControl parent) : base(parent) {
			BackColor = SystemColors.Control;
			Sizable = true;
			Movable = true;
			Closable = false;
		}
		public String Text { get { return text; } set { if (text == value) return; text = value; Invalidate(new Rectangle(0, 0, Bounds.Width, 20)); RaiseEvent(TextChanged); } }
		private NonClientOps GetNonClientOperation(Point p) {
			if ((new Rectangle(Bounds.Width - 5 - menubar_btnsize, 4, menubar_btnsize, menubar_btnsize)).Contains(p)) return NonClientOps.ButtonClose;
			NonClientOps mr = 0;
			if (Sizable) {
				if (Movable) {
					if (p.X < 4) mr |= NonClientOps.ResizeLeft;
					if (p.Y < 4) mr |= NonClientOps.ResizeTop;
				}
				if (p.X >= Bounds.Width - 4) mr |= NonClientOps.ResizeRight;
				if (p.Y >= Bounds.Height - 4) mr |= NonClientOps.ResizeBottom;
			}
			if (mr == 0 && Movable && p.Y < menubar_height) mr = NonClientOps.Move;
			return mr;
		}
		private void SetCursorForNonClientOperation(NonClientOps op, FBGPointingEvent e) {
			switch (op & NonClientOps.MoveResize) {
				case NonClientOps.Move: e.Cursor = FBGCursor.Move; break;
				case NonClientOps.ResizeLeft: e.Cursor = FBGCursor.SizeLeft; break;
				case NonClientOps.ResizeRight: e.Cursor = FBGCursor.SizeRight; break;
				case NonClientOps.ResizeBottom: e.Cursor = FBGCursor.SizeBottom; break;
				case NonClientOps.ResizeTop: e.Cursor = FBGCursor.SizeTop; break;
				case NonClientOps.ResizeTop | NonClientOps.ResizeLeft: e.Cursor = FBGCursor.SizeTopLeft; break;
				case NonClientOps.ResizeTop | NonClientOps.ResizeRight: e.Cursor = FBGCursor.SizeTopRight; break;
				case NonClientOps.ResizeBottom | NonClientOps.ResizeLeft: e.Cursor = FBGCursor.SizeBottomLeft; break;
				case NonClientOps.ResizeBottom | NonClientOps.ResizeRight: e.Cursor = FBGCursor.SizeBottomRight; break;
			}
		}
		protected override void HandlePointingEvent(FBGPointingEvent e) {
			if (Cursor != null) e.Cursor = Cursor;
			if (HandlePointingEventA(e)) base.HandlePointingEvent(e);
		}
		Boolean HandlePointingEventA(FBGPointingEvent e) {
			switch (e.Type) {
				case FBGPointingEventType.Move: return MouseMove(e.Position, e.Buttons, e);
				case FBGPointingEventType.ButtonDown: return MouseDown(e.Position, e.Buttons, e);
				case FBGPointingEventType.ButtonUp: return MouseUp(e.Position, e.Buttons, e);
				default: return true;
			}
		}
		Boolean MouseDown(Point p, MouseButtons buttons, FBGPointingEvent e) {
			NonClientOps mr = 0;
			if ((buttons & MouseButtons.Left) != 0) mr = GetNonClientOperation(p);
			if (mr != 0) {
				moveresize = mr;
				prevPosition = p;
				SetCursorForNonClientOperation(moveresize, e);
				CaptureMouse(true);
				return false;
			} else {
				return true;
			}
		}
		Boolean MouseMove(Point position, MouseButtons buttons, FBGPointingEvent e) {
			NonClientOps mr = moveresize;
			if (mr == 0) mr = GetNonClientOperation(position);
			if (mr == 0) return true;
			if (selectedNonClientOp != mr) {
				selectedNonClientOp = mr;
				Invalidate(new Rectangle(1, 1, Width - 2, menubar_height));
			}
			SetCursorForNonClientOperation(mr, e);
			if ((moveresize & NonClientOps.MoveResize) != 0) {
				Rectangle b = Bounds;
				int dx = position.X - prevPosition.X;
				int dy = position.Y - prevPosition.Y;
				if (moveresize == NonClientOps.Move) {
					b.Offset(dx, dy);
				}
				if ((moveresize & NonClientOps.ResizeLeft) != 0) {
					b.X += dx;
					b.Width -= dx;
				} else if ((moveresize & NonClientOps.ResizeRight) != 0) {
					b.Width += dx;
					prevPosition.X = position.X;
				}
				if ((moveresize & NonClientOps.ResizeTop) != 0) {
					b.Y += dy;
					b.Height -= dy;
				} else if ((moveresize & NonClientOps.ResizeBottom) != 0) {
					b.Height += dy;
					prevPosition.Y = position.Y;
				}
				b.Width = Math.Max(b.Width, 3 * menubar_btnsize + 5);
				b.Height = Math.Max(b.Height, menubar_height + 5);
				Bounds = b;
			}
			return false;
		}
		Boolean MouseUp(Point position, MouseButtons buttons, FBGPointingEvent e) {
			if (moveresize == 0) {
				SetCursorForNonClientOperation(GetNonClientOperation(position), e);
				return true;
			} else if ((buttons & MouseButtons.Left) != 0) {
				MouseMove(position, buttons);
				CaptureMouse(false);
				if (moveresize == NonClientOps.ButtonClose && (new Rectangle(Bounds.Width - 5 - menubar_btnsize, 4, menubar_btnsize, menubar_btnsize)).Contains(position) && Closable) Close();
				moveresize = 0;
				SetCursorForNonClientOperation(GetNonClientOperation(position), e);
			}
			return false;
		}
		protected override void Paint(Graphics g) {
			base.Paint(g);
			//Window frame
			g.DrawRectangle(Pens.Gray, 0, 0, Bounds.Width - 1, Bounds.Height - 1);
			g.DrawRectangle(Pens.LightGray, 1, 1, Bounds.Width - 3, Bounds.Height - 3);
			g.DrawRectangle(Pens.DarkGray, 2, menubar_height, Bounds.Width - 5, Bounds.Height - menubar_height - 3);
			g.DrawRectangle(Pens.Gray, 3, menubar_height + 1, Bounds.Width - 7, Bounds.Height - menubar_height - 5);
			using (Brush b = new LinearGradientBrush(new Rectangle(0, 1, 1, menubar_height - 2), Color.Gray, Color.LightGray, LinearGradientMode.Vertical))
				g.FillRectangle(b, 2, 2, Bounds.Width - 4, menubar_height - 2);

			g.DrawString(Text, SystemFonts.CaptionFont, Brushes.Black, 4, 8);

			g.SmoothingMode = SmoothingMode.AntiAlias;
			//PaintMenuButton(g, Bounds.Width - 9 - menubar_btnsize * 3, 4, menubar_btnsize, menubar_btnsize, NonClientOps.None);
			//PaintMenuButton(g, Bounds.Width - 7 - menubar_btnsize * 2, 4, menubar_btnsize, menubar_btnsize, NonClientOps.None);
			if (Closable)
				PaintMenuButton(g, Bounds.Width - 5 - menubar_btnsize * 1, 4, menubar_btnsize, menubar_btnsize, NonClientOps.ButtonClose);
		}
		private void PaintMenuButton(Graphics g, int x, int y, int w, int h, NonClientOps btn) {
			g.DrawRectangle(Pens.Gray, x, y, w, h);
			Color color2 = Color.DarkGray;
			if (btn != NonClientOps.None && btn == selectedNonClientOp) color2 = Color.Brown;
			using (Brush b = new LinearGradientBrush(new Rectangle(0, y + 1, 1, h - 1), Color.LightGray, color2, LinearGradientMode.Vertical)) {
				g.FillRectangle(b, x + 1, y + 2, w - 2, h - 2);
			}
			if (btn == NonClientOps.ButtonClose) {
				using (Pen pen = new Pen(Brushes.Black, 2)) {
					g.DrawLine(pen, x + 3.5f, y + 4.5f, x + w - 4, y + h - 4);
					g.DrawLine(pen, x + w - 4, y + 4.5f, x + 3.5f, y + h - 4);
				}
			}
		}
		public void Close() {
			Parent.RemoveControl(this);
			RaiseEvent(Closed);
		}
	}
	public class FBGDesktop : FBGContainerControl {
		FBGContainerControl windowcontainer;
		FBGContainerControl menucontainer;
		Boolean startup = true;
		class FBGWindowState {
			public IFBGControl Control;
			public FBGButton MenuButton;
			public Boolean Visible;
			public Rectangle OldBounds;
			public FBGWindowState(IFBGControl cntrl, FBGButton btn) {
				Control = cntrl;
				MenuButton = btn;
				Visible = true;
			}
		}
		Dictionary<IFBGControl, FBGWindowState> windowstates = new Dictionary<IFBGControl, FBGWindowState>();
		public FBGDesktop(IFBGContainerControl parent) : base(parent) {
			menucontainer = new FBGContainerControl(this);
			menucontainer.Bounds = new Rectangle(0, Bounds.Height - 25, Bounds.Width, 25);
			windowcontainer = new FBGContainerControl(this);
			windowcontainer.Bounds = new Rectangle(0, 0, Bounds.Width, Bounds.Height - 25);
			startup = false;
		}
		public override Rectangle Bounds {
			get { return base.Bounds; }
			set {
				if (Bounds == value) return;
				base.Bounds = value;
				if (startup) return;
				menucontainer.Bounds = new Rectangle(0, value.Height - 25, value.Width, 25);
				windowcontainer.Bounds = new Rectangle(0, 0, value.Width, value.Height - 25);
				ScaleMenuButtons();
			}
		}
		protected override void AddControl(IFBGControl control) {
			if (startup) {
				base.AddControl(control);
				return;
			}
			((IFBGContainerControl)windowcontainer).AddControl(control);
			FBGButton btn = new FBGButton(menucontainer);
			FBGForm formcontrol = control as FBGForm;
			if (formcontrol == null) {
				btn.Text = "Untitled";
			} else {
				formcontrol.TextChanged += delegate(Object sender, EventArgs e) {
					btn.Text = formcontrol.Text;
				};
				btn.Text = formcontrol.Text;
			}
			FBGWindowState ws = new FBGWindowState(control, btn);
			windowstates.Add(control, ws);
			ScaleMenuButtons();
			btn.Click += delegate(Object sender, EventArgs e) {
				if (ws.Visible) {
					if (ws.MenuButton.BackColor == Color.DarkGray) {
						ws.OldBounds = ws.Control.Bounds;
						ws.Visible = false;
						ws.Control.Bounds = Rectangle.Empty;
					} else {
						windowcontainer.BringControlToFront(ws.Control);
						foreach (FBGWindowState wsa in windowstates.Values) if (!ReferenceEquals(ws, wsa)) wsa.MenuButton.BackColor = SystemColors.ButtonFace;
						ws.MenuButton.BackColor = Color.DarkGray;
					}
				} else {
					ws.Control.Bounds = ws.OldBounds;
					ws.Visible = true;
					windowcontainer.BringControlToFront(ws.Control);
					foreach (FBGWindowState wsa in windowstates.Values) if (!ReferenceEquals(ws, wsa)) wsa.MenuButton.BackColor = SystemColors.ButtonFace;
					ws.MenuButton.BackColor = Color.DarkGray;
				}
			};
		}
		public override Boolean RemoveControl(IFBGControl control) {
			if (!windowcontainer.RemoveControl(control)) return false;
			FBGWindowState ws = windowstates[control];
			menucontainer.RemoveControl(ws.MenuButton);
			windowstates.Remove(control);
			ScaleMenuButtons();
			return true;
		}
		public override void RemoveAllControls() {
			windowcontainer.RemoveAllControls();
			menucontainer.RemoveAllControls();
			windowstates.Clear();
			ScaleMenuButtons();
		}
		private void ScaleMenuButtons() {
			int bcount = windowstates.Count;
			int bwidth = 200;
			int twidth = bwidth * bcount;
			if (twidth > Bounds.Width) bwidth = menucontainer.Bounds.Width / bcount;
			int i = 0;
			foreach (FBGWindowState ws in windowstates.Values) {
				ws.MenuButton.Bounds = new Rectangle(i * bwidth, 0, bwidth, 25);
				i++;
			}
		}
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawLine(Pens.Black, 0, Bounds.Height - 25, Bounds.Width, Bounds.Height - 25);
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
			IFBGControl control = FindControlAtPosition(position);
			if (ReferenceEquals(control, windowcontainer)) {
				control = windowcontainer.FindControlAtPosition(PointToChild(windowcontainer, position));
				if (!ReferenceEquals(control, null)) {
					windowcontainer.BringControlToFront(control);
					foreach (FBGWindowState ws in windowstates.Values) 
						ws.MenuButton.BackColor = ReferenceEquals(ws.Control, control) ? Color.DarkGray : SystemColors.ButtonFace;
				}
			}
			base.MouseDown(position, buttons);
		}
	}

	public class FBGLabel : FBGControl, IDisposable {
		public FBGLabel(IFBGContainerControl parent) : base(parent) {
			Size = new Size(200, 16);
		}
		private String text = String.Empty;
		private Font font = SystemFonts.DefaultFont;
		private Brush brush = SystemBrushes.ControlText;
		private StringFormat stringformat = new StringFormat();
		public String Text { get { return text; } set { text = value; Invalidate(); } }
		public Font Font { get { return font; } set { font = value; Invalidate(); } }
		public Brush Brush { get { return brush; } set { brush = value; Invalidate(); } }
		public Color Color { set { Brush = new SolidBrush(value); } }
		public StringAlignment Alignment { get { return stringformat.Alignment; } set { stringformat.Alignment = value; Invalidate(); } }
		public StringFormatFlags FormatFlags { get { return stringformat.FormatFlags; } set { stringformat.FormatFlags = value; Invalidate(); } }
		public StringAlignment LineAlignment { get { return stringformat.LineAlignment; } set { stringformat.LineAlignment = value; Invalidate(); } }
		public StringTrimming Trimming { get { return stringformat.Trimming; } set { stringformat.Trimming = value; Invalidate(); } }
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawString(text, font, brush, new Rectangle(Point.Empty, Bounds.Size), stringformat);
		}
		public void Dispose() {
			stringformat.Dispose();
		}
		protected override void Orphaned() {
			base.Orphaned();
			Dispose();
		}
	}
	public class FBGTextBox : FBGControl {
		public FBGTextBox(IFBGContainerControl parent) : base(parent) { 
			BackColor = Color.White;
			Size = new Size(200, 20);
			Cursor = FBGCursor.IBeam;
		}
		private String text = String.Empty;
		private Char passwordChar = (Char)0;
		private Font font = new Font(FontFamily.GenericMonospace, 10);
		private Brush brush = SystemBrushes.ControlText;
		private Boolean hasKeyboardFocus = false;
		private float[] characterPositions = null;
		public String Text { get { return text; } set { text = value; if (CaretPosition > text.Length) CaretPosition = text.Length; characterPositions = null; Invalidate(); RaiseEvent(TextChanged); } }
		public Font Font { get { return font; } set { font = value; characterPositions = null; Invalidate(); } }
		public Brush Brush { get { return brush; } set { brush = value; Invalidate(); } }
		public Color Color { set { Brush = new SolidBrush(value); } }
		public Int32 CaretPosition { get; private set; }
		public Char PasswordChar { get { return passwordChar; } set { passwordChar = value; Invalidate(); } }
		public event EventHandler TextChanged;
		public event KeyPressEventHandler OnKeyPress;
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawRectangle(Pens.Gray, 0, 0, Bounds.Width - 1, Bounds.Height - 1);
			float[] positions = characterPositions;
			String textbuffer = text;
			int textlength = textbuffer == null ? 0 : textbuffer.Length;
			if (positions == null || positions.Length != textlength) characterPositions = positions = new float[text.Length];
			using (StringFormat sf_nonprinting = new StringFormat(StringFormat.GenericTypographic)) {
				sf_nonprinting.Trimming = StringTrimming.None;
				sf_nonprinting.FormatFlags = StringFormatFlags.DisplayFormatControl | StringFormatFlags.MeasureTrailingSpaces;
				sf_nonprinting.HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None;

				float x = 1;
				float y = 1;
				if (hasKeyboardFocus && CaretPosition == 0) g.DrawLine(Pens.Black, x + 2, 2, x + 2, Height - 4);
				String c = passwordChar == 0 ? null : new String(passwordChar, 1);
				for (int i = 0; i < textlength; i++) {
					if (passwordChar == 0) c = textbuffer.Substring(i, 1);
					g.DrawString(c, font, brush, x, y);
					SizeF s = g.MeasureString(c, font, int.MaxValue, sf_nonprinting);
					x += (float)Math.Ceiling(s.Width);
					if (positions.Length > i) positions[i] = x;

					if (hasKeyboardFocus && i == CaretPosition - 1) g.DrawLine(Pens.Black, x + 2, 2, x + 2, Height - 4);
				}
			}
		}
		int GetCharacterIndexAtPosition(float x) {
			float[] positions = characterPositions;
			if (positions == null) return -1;
			for (int i = 0; i < positions.Length; i++) if (x < characterPositions[i]) return i;
			return characterPositions.Length;
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
			hasKeyboardFocus = CaptureKeyboard(true);
			CaretPosition = GetCharacterIndexAtPosition(position.X);
			Invalidate();
		}
		protected override void KeyDown(Keys key) {
			if ((key & Keys.KeyCode) == Keys.Left) {
				CaretPosition--;
				if (CaretPosition < 0) CaretPosition = 0;
				Invalidate();
			} else if ((key & Keys.KeyCode) == Keys.Right) {
				CaretPosition++;
				if (CaretPosition > text.Length) CaretPosition = text.Length;
				Invalidate();
			} else if ((key & Keys.KeyCode) == Keys.Home) {
				CaretPosition = 0;
				Invalidate();
			} else if ((key & Keys.KeyCode) == Keys.End) {
				CaretPosition = text.Length;
				Invalidate();
			} else if ((key & Keys.Control) != 0 && (key & Keys.KeyCode) == Keys.V) {
				String cbtext = Clipboard.GetText(TextDataFormat.UnicodeText);
				CaretPosition += cbtext.Length;
				Text = Text.Insert(CaretPosition - cbtext.Length, cbtext);
			}
		}
		protected override void KeyPress(char keyChar) {
			KeyPressEventArgs e = new KeyPressEventArgs(keyChar);
			RaiseEvent(OnKeyPress, e);
			if (e.Handled) return;
			hasKeyboardFocus = true;
			if (keyChar == 8) {
				if (CaretPosition > 0) {
					CaretPosition--;
					Text = Text.Remove(CaretPosition, 1);
				}
			} else if (keyChar == 127) {
				if (CaretPosition < Text.Length) {
					Text = Text.Remove(CaretPosition, 1);
				}
			} else if (keyChar < 32) {
			} else {
				CaretPosition++;
				Text = Text.Insert(CaretPosition - 1, new String(keyChar, 1));
			}
		}
		public void Focus() {
			hasKeyboardFocus = CaptureKeyboard(true);
			Invalidate();
		}
		protected override void LostKeyboardCapture() {
			base.LostKeyboardCapture();
			hasKeyboardFocus = false;
			Invalidate();
		}
	}
	public class FBGButton : FBGControl {
		public FBGButton(IFBGContainerControl parent) : base(parent) {
			BackColor = SystemColors.ButtonFace;
		}
		private String text = String.Empty;
		private Font font = SystemFonts.DefaultFont;
		private Color color = SystemColors.ControlText;
		private Boolean pressed = false;
		private Boolean enabled = true;
		public String Text { get { return text; } set { text = value; Invalidate(); } }
		public Font Font { get { return font; } set { font = value; Invalidate(); } }
		public Color Color { get { return color; } set { color = value; Invalidate(); } }
		public Boolean Enabled { get { return enabled; } set { enabled = value; Invalidate(); } }
		public event EventHandler Click;
		protected override void Paint(Graphics g) {
			base.Paint(g);
			if (Bounds.Width == 0 || Bounds.Height == 0) return;
			if (BackColor == SystemColors.ButtonFace) {
				ControlPaint.DrawButton(g, new Rectangle(0, 0, Bounds.Width, Bounds.Height), enabled ? (pressed ? ButtonState.Pushed : ButtonState.Normal) : ButtonState.Inactive);
			} else {
				g.DrawLine(new Pen(ControlPaint.LightLight(BackColor)), 0, 0, Bounds.Width, 0);
				g.DrawLine(new Pen(ControlPaint.LightLight(BackColor)), 0, 0, 0, Bounds.Height);
				g.DrawLine(new Pen(ControlPaint.Dark(BackColor)), 1, Bounds.Height - 2, Bounds.Width - 1, Bounds.Height - 2);
				g.DrawLine(new Pen(ControlPaint.Dark(BackColor)), Bounds.Width - 2, 1, Bounds.Width - 2, Bounds.Height - 2);
				g.DrawLine(new Pen(ControlPaint.DarkDark(BackColor)), 0, Bounds.Height - 1, Bounds.Width, Bounds.Height - 1);
				g.DrawLine(new Pen(ControlPaint.DarkDark(BackColor)), Bounds.Width - 1, 1, Bounds.Width - 1, Bounds.Height - 1);
				Graphics dc = g;
				Rectangle rectangle = new Rectangle(0, 0, Bounds.Width - 1, Bounds.Height - 1);
				Color ColorControl = BackColor;
				Color ColorControlLight = ControlPaint.Light(ColorControl);
				ButtonState state = pressed ? ButtonState.Pushed : ButtonState.Normal;
				using (Pen NormalPen = new Pen(BackColor), LightPen = new Pen(ControlPaint.Light(BackColor)), DarkPen = new Pen(ControlPaint.Dark(BackColor))) {
					using (HatchBrush hb = new HatchBrush(HatchStyle.Percent50, Color.FromArgb(Math.Min(255, ColorControl.R + 3), ColorControl.G, ColorControl.B), ColorControl)) {
						dc.FillRectangle(hb, rectangle.X + 1, rectangle.Y + 1, rectangle.Width - 2, rectangle.Height - 2);
					}
					if ((state & ButtonState.All) == ButtonState.All || ((state & ButtonState.Checked) == ButtonState.Checked && (state & ButtonState.Flat) == ButtonState.Flat)) {
						using (HatchBrush hb = new HatchBrush(HatchStyle.Percent50, ColorControlLight, ColorControl)) {
							dc.FillRectangle(hb, rectangle.X + 2, rectangle.Y + 2, rectangle.Width - 4, rectangle.Height - 4);
						}
						dc.DrawRectangle(SystemPens.ControlDark, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
					} else if ((state & ButtonState.Flat) == ButtonState.Flat) {
						dc.DrawRectangle(SystemPens.ControlDark, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
					} else if ((state & ButtonState.Checked) == ButtonState.Checked) {
						using (HatchBrush hb = new HatchBrush(HatchStyle.Percent50, ColorControlLight, ColorControl)) {
							dc.FillRectangle(hb, rectangle.X + 2, rectangle.Y + 2, rectangle.Width - 4, rectangle.Height - 4);
						}
						Pen pen = DarkPen;
						dc.DrawLine(pen, rectangle.X, rectangle.Y, rectangle.X, rectangle.Bottom - 2);
						dc.DrawLine(pen, rectangle.X + 1, rectangle.Y, rectangle.Right - 2, rectangle.Y);

						pen = NormalPen;
						dc.DrawLine(pen, rectangle.X + 1, rectangle.Y + 1, rectangle.X + 1, rectangle.Bottom - 3);
						dc.DrawLine(pen, rectangle.X + 2, rectangle.Y + 1, rectangle.Right - 3, rectangle.Y + 1);

						pen = LightPen;
						dc.DrawLine(pen, rectangle.X, rectangle.Bottom - 1, rectangle.Right - 2, rectangle.Bottom - 1);
						dc.DrawLine(pen, rectangle.Right - 1, rectangle.Y, rectangle.Right - 1, rectangle.Bottom - 1);
					} else if (((state & ButtonState.Pushed) == ButtonState.Pushed) && ((state & ButtonState.Normal) == ButtonState.Normal)) {
						Pen pen = DarkPen;
						dc.DrawLine(pen, rectangle.X, rectangle.Y, rectangle.X, rectangle.Bottom - 2);
						dc.DrawLine(pen, rectangle.X + 1, rectangle.Y, rectangle.Right - 2, rectangle.Y);

						pen = NormalPen;
						dc.DrawLine(pen, rectangle.X + 1, rectangle.Y + 1, rectangle.X + 1, rectangle.Bottom - 3);
						dc.DrawLine(pen, rectangle.X + 2, rectangle.Y + 1, rectangle.Right - 3, rectangle.Y + 1);

						pen = LightPen;
						dc.DrawLine(pen, rectangle.X, rectangle.Bottom - 1, rectangle.Right - 2, rectangle.Bottom - 1);
						dc.DrawLine(pen, rectangle.Right - 1, rectangle.Y, rectangle.Right - 1, rectangle.Bottom - 1);
					} else if (((state & ButtonState.Inactive) == ButtonState.Inactive) || ((state & ButtonState.Normal) == ButtonState.Normal)) {
						Pen pen = LightPen;
						dc.DrawLine(pen, rectangle.X, rectangle.Y, rectangle.Right - 2, rectangle.Y);
						dc.DrawLine(pen, rectangle.X, rectangle.Y, rectangle.X, rectangle.Bottom - 2);

						pen = NormalPen;
						dc.DrawLine(pen, rectangle.X + 1, rectangle.Bottom - 2, rectangle.Right - 2, rectangle.Bottom - 2);
						dc.DrawLine(pen, rectangle.Right - 2, rectangle.Y + 1, rectangle.Right - 2, rectangle.Bottom - 3);

						pen = DarkPen;
						dc.DrawLine(pen, rectangle.X, rectangle.Bottom - 1, rectangle.Right - 1, rectangle.Bottom - 1);
						dc.DrawLine(pen, rectangle.Right - 1, rectangle.Y, rectangle.Right - 1, rectangle.Bottom - 2);
					}
				}
			}
			Rectangle frect = new Rectangle(Point.Empty, Bounds.Size);
			SizeF textsize = g.MeasureString(Text, Font);
			using (Brush b = new SolidBrush(enabled ? Color : Color.DarkGray)) {
				g.DrawString(Text, Font, b, new PointF(Bounds.Width / 2.0f - textsize.Width / 2.0f, Bounds.Height / 2.0f - textsize.Height / 2.0f));
			}
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
			pressed = true;
			Invalidate();
			CaptureMouse(true);
			base.MouseDown(position, buttons);
		}
		protected override void MouseUp(Point position, MouseButtons buttons) {
			pressed = false;
			Invalidate();
			CaptureMouse(false);
			if (position.X >= 0 && position.X <= Bounds.Width && position.Y >= 0 && position.Y <= Bounds.Height && enabled) RaiseEvent(Click);
			base.MouseUp(position, buttons);
		}
		protected override void KeyDown(Keys key) {
			if (key == Keys.Return || key == Keys.Space) {
				pressed = true;
				Invalidate();
			}
			base.KeyDown(key);
		}
		protected override void KeyUp(Keys key) {
			if (key == Keys.Return || key == Keys.Space) {
				if (pressed) RaiseEvent(Click);
				pressed = false;
				Invalidate();
			}
			base.KeyUp(key);
		}
		public void Focus() {
			CaptureKeyboard(true);
		}
	}
	public class FBGCheckBox : FBGControl {
		public FBGCheckBox(IFBGContainerControl parent) : base(parent) { }
		private String text = String.Empty;
		private Font font = SystemFonts.DefaultFont;
		private Color color = SystemColors.ControlText;
		private Boolean _checked = false;
		public String Text { get { return text; } set { text = value; Invalidate(); } }
		public Font Font { get { return font; } set { font = value; Invalidate(); } }
		public Color Color { get { return color; } set { color = value; Invalidate(); } }
		public Boolean Checked { get { return _checked; } set { _checked = value; Invalidate(); } }
		public event EventHandler CheckedChanged;
		protected override void Paint(Graphics g) {
			base.Paint(g);
			ControlPaint.DrawCheckBox(g, 0, 0, 13, 13, _checked ? ButtonState.Checked : ButtonState.Normal);
			g.DrawString(Text, Font, new SolidBrush(Color), 15, 0);
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
			Checked = !Checked;
			RaiseEvent(CheckedChanged);
			base.MouseDown(position, buttons);
		}
	}
	public class FBGImageBox : FBGControl {
		Image image = null;
		Image scaledImage = null;
		Size imageSize;
		Boolean ownsImage = false;
		Boolean shouldScaleImage = false;
		PictureBoxSizeMode sizeMode = PictureBoxSizeMode.Normal;
		Rectangle imageRect;
		public Image Image { get { return image; } set { SetImage(value, false); } }
		public Boolean PreScaleImage { get; set; }
		public void SetOwnedImage(Image img) { SetImage(img, true); }
		private void SetImage(Image img, Boolean owned) {
			image = img;
			imageSize = img == null ? Size.Empty : img.Size;
			ownsImage = owned;
			UpdateImageRect(Size.Empty);
		}
		public FBGImageBox(IFBGContainerControl parent) : base(parent) { }
		public PictureBoxSizeMode SizeMode { get { return sizeMode; } set { sizeMode = value; UpdateImageRect(Size.Empty); } }
		public override Rectangle Bounds {
			get {
				return base.Bounds;
			}
			set {
				if (Bounds.Size != value.Size) UpdateImageRect(value.Size);
				base.Bounds = value;
			}
		}
		private void UpdateImageRect(Size csize) {
			shouldScaleImage = false;
			if (scaledImage != null) scaledImage.Dispose();
			scaledImage = null;
			if (image == null) return;
			Boolean boundsset = !csize.IsEmpty;
			if (!boundsset && sizeMode == PictureBoxSizeMode.AutoSize) {
				Size = imageSize;
				return;
			}
			if (!boundsset) csize = Bounds.Size;
			switch (sizeMode) {
				case PictureBoxSizeMode.AutoSize:
				case PictureBoxSizeMode.Normal:
					imageRect = new Rectangle(Point.Empty, imageSize);
					break;
				case PictureBoxSizeMode.CenterImage:
					imageRect = new Rectangle(csize.Width / 2 - imageSize.Width / 2, csize.Height / 2 - imageSize.Height / 2, imageSize.Width, imageSize.Height);
					break;
				case PictureBoxSizeMode.StretchImage:
					imageRect = new Rectangle(Point.Empty, csize);
					break;
				case PictureBoxSizeMode.Zoom:
					float xrat = (float)csize.Width / (float)imageSize.Width;
					float yrat = (float)csize.Height / (float)imageSize.Height;
					float rat = Math.Min(xrat, yrat);
					SizeF dispsize = new SizeF(imageSize.Width * rat, imageSize.Height * rat);
					imageRect = Rectangle.Round(new RectangleF(csize.Width / 2f - dispsize.Width / 2f, csize.Height / 2f - dispsize.Height / 2f, dispsize.Width, dispsize.Height));
					break;
			}
			shouldScaleImage = imageRect.Size != imageSize;
			if (!boundsset) Invalidate();
		}
		protected override void Paint(Graphics g) {
			if (!Visible) return;
			base.Paint(g);
			if (shouldScaleImage && PreScaleImage && image != null) {
				scaledImage = new Bitmap(image, imageRect.Size);
				shouldScaleImage = false;
			}
			if (scaledImage != null) g.DrawImage(scaledImage, imageRect);
			else if (image != null) g.DrawImage(image, imageRect);
		}
		public Point PointToImage(Point point) {
			switch (sizeMode) {
				case PictureBoxSizeMode.AutoSize:
				case PictureBoxSizeMode.Normal:
					break;
				case PictureBoxSizeMode.CenterImage:
					point.X -= imageRect.X;
					point.Y -= imageRect.Y;
					break;
				case PictureBoxSizeMode.StretchImage:
				case PictureBoxSizeMode.Zoom:
				default:
					point.X = (point.X - imageRect.X) * imageSize.Width / imageRect.Width;
					point.Y = (point.Y - imageRect.Y) * imageSize.Height / imageRect.Height;
					break;
			}
			return point;
		}
		protected override void Orphaned() {
			base.Orphaned();
			if (scaledImage != null && scaledImage != image) scaledImage.Dispose();
			scaledImage = null;
			if (ownsImage) {
				image.Dispose();
				image = null;
			}
		}
	}
	public class FBGListBox : FBGControl {
		private List<Object> items = new List<object>();
		private Object selected = null;
		private Object highlighted = null;
		private Boolean hasScrollBar = false;
		private Boolean hitScrollBar = false;
		private int offset = 0;
		private ButtonState buttonUpState = ButtonState.Normal;
		private ButtonState buttonDownState = ButtonState.Normal;
		private Converter<Object, String> itemFormatter = null;
		public Converter<Object, String> ItemFormatter { get { return itemFormatter; } set { itemFormatter = value; Invalidate(); } }
		public FBGListBox(IFBGContainerControl parent)
			: base(parent) {
			BackColor = Color.White;
		}
		public int AddItem(Object item) {
			int id = ((System.Collections.IList)items).Add(item);
			Invalidate();
			return id;
		}
		public void RemoveItem(Object item) {
			items.Remove(item);
			if (offset >= items.Count) offset = Math.Max(0, items.Count - 1);
			Invalidate();
		}
		public void RemoveItemAt(int index) {
			if (index < 0) index += items.Count;
			items.RemoveAt(index);
			if (offset >= index && offset > 0) offset--;
			Invalidate();
		}
		public void Clear() {
			items.Clear();
			offset = 0;
			Invalidate();
		}
		public System.Collections.IList Items { get { return items.AsReadOnly(); } }
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawRectangle(Pens.DarkBlue, 0, 0, Bounds.Width - 1, Bounds.Height - 1);
			int lh = (int)Math.Ceiling(SystemFonts.DefaultFont.GetHeight());
			int y = 2;
			using (Pen dottedpen = new Pen(Brushes.Black)) {
				dottedpen.DashStyle = DashStyle.Dot;
				using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap)) {
					for (int i = offset; i < items.Count; i++) {
						Object item = items[i];
						String text = itemFormatter == null ? (item == null ? String.Empty : item.ToString()) : itemFormatter(item);
						if (item == selected) g.FillRectangle(Brushes.DarkGray, 2, y, Bounds.Width - 4, lh);
						if (item == highlighted) g.DrawRectangle(dottedpen, 2, y, Bounds.Width - 5, lh - 1);
						g.DrawString(text, SystemFonts.DefaultFont, SystemBrushes.WindowText, new Rectangle(3, y, Bounds.Width - 6, lh), sf);
						y += lh;
						if (y + lh + 2 >= Bounds.Height) break;
					}
				}
			}
			hasScrollBar = (y < 2 + lh * items.Count) || (offset != 0);
			if (hasScrollBar) {
				int xoff = Bounds.Width - 17;
				using (Brush b = new LinearGradientBrush(new Rectangle(xoff, 0, 17, 1), Color.LightGray, Color.White, LinearGradientMode.Horizontal))
					g.FillRectangle(b, xoff, 17, 16, Bounds.Height - 17 - 17);
				ControlPaint.DrawScrollButton(g, xoff, 1, 16, 16, ScrollButton.Up, buttonUpState);
				ControlPaint.DrawScrollButton(g, xoff, Bounds.Height - 17, 16, 16, ScrollButton.Down, buttonDownState);
				if (items.Count > 1) g.DrawRectangle(Pens.Black, new Rectangle(xoff, 17 + offset * (Bounds.Height - 17 - 17 - 20) / (items.Count - 1), 15, 20));
			}
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
			if ((buttons & MouseButtons.Left) != 0) {
				CaptureMouse(true);
				if (hasScrollBar && position.X > Bounds.Width - 17) {
					hitScrollBar = true;
					int off = offset;
					if (position.Y < 17) {
						off--;
						buttonUpState = ButtonState.Pushed;
					} else if (position.Y > Bounds.Height - 17) {
						off++;
						buttonDownState = ButtonState.Pushed;
					} else {
						off = (int)Math.Round((position.Y - 17) * (items.Count - 1) / (double)(Bounds.Height - 17 - 17 - 10));
					}
					offset = Math.Max(0, Math.Min(items.Count - 1, off));
					Invalidate();
				} else {
					MouseHandler(position, buttons);
				}
			}
		}
		protected override void MouseMove(Point position, MouseButtons buttons) {
			if (hitScrollBar) {
				if (position.Y < 17) {
				} else if (position.Y > Bounds.Height - 17) {
				} else {
					offset = Math.Max(0, Math.Min(items.Count - 1,  (int)Math.Round((position.Y - 17) * (items.Count - 1) / (double)(Bounds.Height - 17 - 17 - 10))));
					Invalidate();
				}
				return;
			}
			MouseHandler(position, buttons);
		}
		protected override void MouseUp(Point position, MouseButtons buttons) {
			if ((buttons & MouseButtons.Left) != 0) {
				CaptureMouse(false);
				buttonUpState = buttonDownState = ButtonState.Normal;
				Invalidate();
				if (hitScrollBar) {
					hitScrollBar = false;
					return;
				}
			}
			if (hitScrollBar) return;
			MouseHandler(position, buttons);
		}
		private void MouseHandler(Point position, MouseButtons buttons) {
			if ((buttons & MouseButtons.Left) != 0) {
				int lh = (int)Math.Ceiling(SystemFonts.DefaultFont.GetHeight());
				int i = (position.Y - 2) / lh + offset;
				if (i < 0) i = 0;
				if (i >= items.Count) i = items.Count - 1;
				Boolean changed = false;
				Object current = items[i];
				if (!ReferenceEquals(highlighted, current)) changed = true;
				highlighted = current;
				if ((new Rectangle(Point.Empty, Bounds.Size)).Contains(position)) {
					if (!ReferenceEquals(selected, current)) changed = true;
					selected = current;
				}
				if (changed) Invalidate();
			}
		}
	}
	public abstract class FBGUpDownControlBase : FBGControl {
		private ButtonState buttonUpState = ButtonState.Normal;
		private ButtonState buttonDownState = ButtonState.Normal;
		public FBGUpDownControlBase(IFBGContainerControl parent) : base(parent) {
			BackColor = Color.White;
			Height = 25;
		}
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawRectangle(Pens.DarkBlue, 0, 0, Bounds.Width - 1, Bounds.Height - 1);
			int lh = (int)Math.Ceiling(SystemFonts.DefaultFont.GetHeight());
			String text = Text;
			if (text == null) {
				g.FillRectangle(Brushes.DarkGray, 2, 2, Bounds.Width - 4 - 16, Bounds.Height - 4);
			} else {
				using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap)) {
					sf.LineAlignment = StringAlignment.Center;
					g.FillRectangle(Brushes.LightGray, 2, 2, Bounds.Width - 4 - 16, Bounds.Height - 4);
					g.DrawString(text, SystemFonts.DefaultFont, SystemBrushes.WindowText, new Rectangle(3, 2, Bounds.Width - 6 - 16, Bounds.Height - 4), sf);
				}
			}
			int xoff = Bounds.Width - 17;
			int he = (Bounds.Height - 2) / 2;
			ControlPaint.DrawScrollButton(g, xoff, 1, 16, he, ScrollButton.Up, buttonUpState);
			ControlPaint.DrawScrollButton(g, xoff, Bounds.Height - he - 1, 16, he, ScrollButton.Down, buttonDownState);
		}
		protected abstract String Text { get; }
		protected override void MouseDown(Point position, MouseButtons buttons) {
			CaptureKeyboard(true);
			if ((buttons & MouseButtons.Left) != 0) {
				CaptureMouse(true);
				if (position.X > Bounds.Width - 17) {
					if (position.Y < Bounds.Height / 2) {
						buttonUpState = ButtonState.Pushed;
						ButtonPressUp();
					} else {
						buttonDownState = ButtonState.Pushed;
						ButtonPressDown();
					}
					Invalidate(new Rectangle(Bounds.Width - 16, 0, 16, Bounds.Height));
				}
			}
		}
		protected override void MouseUp(Point position, MouseButtons buttons) {
			if ((buttons & MouseButtons.Left) != 0) {
				CaptureMouse(false);
				buttonUpState = buttonDownState = ButtonState.Normal;
				Invalidate(new Rectangle(Bounds.Width - 16, 0, 16, Bounds.Height));
			}
		}
		protected override void KeyDown(Keys key) {
			base.KeyDown(key);
			if (key == Keys.Down) ButtonPressDown();
			else if (key == Keys.Up) ButtonPressUp();
		}
		protected abstract void ButtonPressUp();
		protected abstract void ButtonPressDown();
	}
	public class FBGDomainUpDown : FBGUpDownControlBase {
		private List<Object> items = new List<object>();
		private int selectedIndex = -1;
		private Converter<Object, String> itemFormatter = null;
		public Boolean AllowSelectEmpty { get; set; }
		public Converter<Object, String> ItemFormatter { get { return itemFormatter; } set { itemFormatter = value; Invalidate(); } }
		public event EventHandler SelectedIndexChanged;
		public FBGDomainUpDown(IFBGContainerControl parent) : base(parent) { }
		public void AddItem(Object item) {
			items.Add(item);
			Invalidate();
		}
		public void RemoveItem(Object item) {
			items.Remove(item);
			FixSelectedIndex(0);
		}
		public void RemoveItem(int index) {
			items.RemoveAt(index);
			FixSelectedIndex(0);
		}
		public int SelectedIndex {
			get { return selectedIndex; }
			set {
				if (value < -2 || value >= items.Count) throw new ArgumentOutOfRangeException("value", "Value must be between -1 and the number of items minus one");
				if (selectedIndex == value) return;
				selectedIndex = value;
				Invalidate();
				RaiseEvent(SelectedIndexChanged);
			}
		}
		public Object SelectedItem {
			get { return selectedIndex == -1 ? null : items[selectedIndex]; }
			set {
				if (value == null) {
					SelectedIndex = -1;
				} else {
					for (int i = 0; i < items.Count; i++) {
						if (items[i] == value) {
							SelectedIndex = i;
							break;
						}
					}
				}
			}
		}
		protected override string Text {
			get {
				if (selectedIndex == -1) return null;
				Object item = items[selectedIndex];
				if (itemFormatter != null) return itemFormatter(item);
				if (item == null) return null;
				return item.ToString();
			}
		}
		private void FixSelectedIndex(int change) {
			int value = selectedIndex;
			if (value == 0 && change == -1 && !AllowSelectEmpty) change = 0;
			value += change;
			if (value < -1) value = -1;
			if (value >= items.Count) value = items.Count - 1;
			SelectedIndex = value;
		}
		protected override void ButtonPressDown() {
			FixSelectedIndex(1);
		}
		protected override void ButtonPressUp() {
			FixSelectedIndex(-1);
		}
	}
	public class FBGNumericUpDown : FBGUpDownControlBase {
		private int minimum = 0;
		private int maximum = 0;
		private int value = 0;
		public event EventHandler SelectedValueChanged;
		public FBGNumericUpDown(IFBGContainerControl parent) : base(parent) { }
		public int Value {
			get { return value; }
			set { if (this.value == value) return; this.value = value; Invalidate(); RaiseEvent(SelectedValueChanged); }
		}
		public int Minimum {
			get { return minimum; }
			set { minimum = value; if (this.value < minimum) this.Value = minimum; }
		}
		public int Maximum {
			get { return maximum; }
			set { maximum = value; if (this.value > maximum) this.Value = maximum; }
		}
		public int Step { get; set; }
		protected override string Text {
			get { return value.ToString(); }
		}
		protected override void ButtonPressDown() {
			Value = Math.Max(minimum, value - Step);
		}
		protected override void ButtonPressUp() {
			Value = Math.Min(maximum, value + Step);
		}
	}
	public interface IFBGTreeParent {
		FBGTreeView TreeView { get; }
		int Depth { get; }
		void AddChild(FBGTreeNode node);
		void RemoveChild(FBGTreeNode node);
	}
	public class FBGTreeNode : IFBGTreeParent {
		private List<FBGTreeNode> children = new List<FBGTreeNode>();
		private Boolean expanded = true;
		private Boolean hasCheckBox = false;
		private Boolean isChecked = false;
		private Object item;

		public FBGTreeView TreeView { get; private set; }
		public int Depth { get; private set; }
		public Object Tag { get; set; }
		public IFBGTreeParent Parent { get; private set; }

		public IList<FBGTreeNode> Children { get { return children.AsReadOnly(); } }

		public Object Item {
			get { return item; }
			set {
				item = value;
				Invalidate();
			}
		}
		public Boolean Expanded {
			get { return expanded; }
			set {
				if (expanded == value) return;
				expanded = value;
				UpdateTree();
			}
		}
		public Boolean HasCheckBox {
			get { return hasCheckBox; }
			set {
				if (hasCheckBox == value) return;
				hasCheckBox = value;
				Invalidate();
			}
		}
		public Boolean Checked {
			get { return isChecked; }
			set {
				if (isChecked == value) return;
				isChecked = value;
				Invalidate();
				if (TreeView != null) TreeView.RaiseNodeCheckedChanged(this);
			}
		}

		public FBGTreeNode(IFBGTreeParent parent, Object item) {
			this.TreeView = parent.TreeView;
			this.Depth = parent.Depth + 1;
			this.Parent = parent;
			this.item = item;
			parent.AddChild(this);
		}

		public void Remove() {
			Parent.RemoveChild(this);
		}
		void IFBGTreeParent.AddChild(FBGTreeNode node) {
			children.Add(node);
			if (Expanded) UpdateTree();
			else Invalidate();
		}
		void IFBGTreeParent.RemoveChild(FBGTreeNode node) {
			children.Remove(node);
			TreeView.ReleaseNodeFromTree(node);
			if (Expanded) UpdateTree();
			else Invalidate();
		}
		public void Invalidate() {
			if (TreeView != null) TreeView.Invalidate(this);
		}
		private void UpdateTree() {
			if (TreeView != null) TreeView.UpdateView();
		}
		public FBGTreeNode AddNode(Object item) {
			return new FBGTreeNode(this, item);
		}
	}
	public class FBGTreeView : FBGControl, IFBGTreeParent {
		private List<FBGTreeNode> items = new List<FBGTreeNode>();
		private List<FBGTreeNode> itemsView = new List<FBGTreeNode>();
		private FBGTreeNode selected = null;
		private FBGTreeNode highlighted = null;
		private Boolean hasScrollBar = false;
		private Boolean hitScrollBar = false;
		private int offset = 0;
		private ButtonState buttonUpState = ButtonState.Normal;
		private ButtonState buttonDownState = ButtonState.Normal;
		private Converter<Object, String> itemFormatter = null;

		public Converter<Object, String> ItemFormatter { get { return itemFormatter; } set { itemFormatter = value; Invalidate(); } }
		public FBGTreeNode SelectedNode { get { return selected; } set { if (selected != value) { selected = value; Invalidate(); RaiseEvent(SelectedNodeChanged); } } }
		public IList<FBGTreeNode> Nodes { get { return items.AsReadOnly(); } }

		public event EventHandler SelectedNodeChanged;
		public event EventHandler NodeCheckedChanged;
		
		public FBGTreeView(IFBGContainerControl parent) : base(parent) {
			BackColor = Color.White;
		}
		FBGTreeView IFBGTreeParent.TreeView { get { return this; } }
		int IFBGTreeParent.Depth { get { return -1; } }
		void IFBGTreeParent.AddChild(FBGTreeNode node) {
			items.Add(node);
			UpdateView();
		}
		void IFBGTreeParent.RemoveChild(FBGTreeNode node) {
			items.Remove(node);
			ReleaseNodeFromTree(node);
			UpdateView();
		}
		public FBGTreeNode AddNode(Object item) { return new FBGTreeNode(this, item); }
		internal void ReleaseNodeFromTree(FBGTreeNode node) {
			if (highlighted == node) highlighted = null;
			if (selected == node) SelectedNode = null;
		}
		internal void RaiseNodeCheckedChanged(FBGTreeNode node) {
			RaiseEvent(NodeCheckedChanged);
		}
		internal void UpdateView() {
			List<FBGTreeNode> newView = new List<FBGTreeNode>();
			Stack<Queue<FBGTreeNode>> stack = new Stack<Queue<FBGTreeNode>>();
			stack.Push(new Queue<FBGTreeNode>(items));
			while (stack.Count > 0) {
				Queue<FBGTreeNode> list = stack.Peek();
				if (list.Count == 0) {
					stack.Pop();
					continue;
				}
				FBGTreeNode item = list.Dequeue();
				newView.Add(item);
				if (item.Expanded) stack.Push(new Queue<FBGTreeNode>(item.Children));
			}
			itemsView = newView;
			Invalidate();
		}
		internal void Invalidate(FBGTreeNode node) {
			int i = itemsView.IndexOf(node);
			if (i == -1) return;
			int lh = (int)Math.Ceiling(SystemFonts.DefaultFont.GetHeight() / 2.0) * 2;
			Invalidate(new Rectangle(1, i * lh, Bounds.Width - 1, lh));
		}
		protected override void Paint(Graphics g) {
			base.Paint(g);

			int lh = (int)Math.Ceiling(SystemFonts.DefaultFont.GetHeight() / 2.0) * 2;
			int th = lh * itemsView.Count;
			hasScrollBar = offset > 0 || th + 2 > Bounds.Height;
			int y = 2;
			using (Pen dottedpen = new Pen(Brushes.Black)) {
				dottedpen.DashStyle = DashStyle.Dot;
				using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap)) {
					int lw = Bounds.Width - 2;
					if (hasScrollBar) lw -= 17;
					for (int i = offset; i < itemsView.Count; i++) {
						FBGTreeNode item = itemsView[i];
						if (y + 2 < Bounds.Height) {
							Object obj = item.Item;
							String text = itemFormatter == null ? (obj == null ? String.Empty : obj.ToString()) : itemFormatter(obj);
							if (item == selected) g.FillRectangle(Brushes.DarkGray, 2, y, lw - 2, lh);
							if (item == highlighted) g.DrawRectangle(dottedpen, 2, y, lw - 3, lh - 1);
							int x = 3 + 19 * item.Depth + 14;
							if (item.HasCheckBox) {
								x += 2;
								ControlPaint.DrawCheckBox(g, x, y, lh, lh, item.Checked ? ButtonState.Checked : ButtonState.Normal);
								x += lh + 1;
							}
							g.DrawString(text, SystemFonts.DefaultFont, SystemBrushes.WindowText, new Rectangle(x, y, lw - x, lh), sf);
						}
						int upto = y + 2 + 4 - 8;
						for (int j = i - 1; j >= 0; j--) {
							if (itemsView[j].Depth < item.Depth) {
								break;
							}
							if (itemsView[j].Depth == item.Depth) {
								if (itemsView[j].Children.Count > 0) {
									upto = 2 + lh * (j - offset) + 10;
								} else {
									upto = 2 + lh * (j - offset) + 6;
								}
								break;
							}
							if (j <= offset) {
								upto = 2 + 2 + 4 - 8;
								break;
							}
						}
						if (item.Children.Count > 0) {
							g.DrawRectangle(Pens.Black, 3 + 19 * item.Depth, y + 2, 8, 8);
							g.DrawLine(Pens.Black, 3 + 19 * item.Depth + 2, y + 2 + 4, 3 + 19 * item.Depth + 6, y + 2 + 4);
							if (!item.Expanded) g.DrawLine(Pens.Black, 3 + 19 * item.Depth + 4, y + 4, 3 + 19 * item.Depth + 4, y + 2 + 6);

							g.DrawLine(dottedpen, 3 + 19 * item.Depth + 8, y + 2 + 4, 3 + 19 * item.Depth + 14, y + 2 + 4);
							g.DrawLine(dottedpen, 3 + 19 * item.Depth + 4, y + 2 + 4 - 6, 3 + 19 * item.Depth + 4, upto);
						} else {
							g.DrawLine(dottedpen, 3 + 19 * item.Depth + 4, y + 2 + 4, 3 + 19 * item.Depth + 14, y + 2 + 4);
							g.DrawLine(dottedpen, 3 + 19 * item.Depth + 4, y + 2 + 4 - 2, 3 + 19 * item.Depth + 4, upto);
						}
						y += lh;
						//if (y + lh + 2 >= Bounds.Height && item.Depth == 0) break;
						if (y + 2 >= Bounds.Height && item.Depth == 0) break;
					}
				}
			}
			//if (y < th) hasScrollBar = true;
			//hasScrollBar = true;
			if (hasScrollBar) {
				int xoff = Bounds.Width - 17;
				using (Brush b = new LinearGradientBrush(new Rectangle(xoff, 0, 17, 1), Color.LightGray, Color.White, LinearGradientMode.Horizontal))
					g.FillRectangle(b, xoff, 17, 16, Bounds.Height - 17 - 17);
				ControlPaint.DrawScrollButton(g, xoff, 1, 16, 16, ScrollButton.Up, buttonUpState);
				ControlPaint.DrawScrollButton(g, xoff, Bounds.Height - 17, 16, 16, ScrollButton.Down, buttonDownState);
				g.DrawRectangle(Pens.Black, new Rectangle(xoff, 17 + offset * (Bounds.Height - 17 - 17 - 20) / (itemsView.Count - 1), 15, 20));
			}

			g.DrawRectangle(Pens.DarkBlue, 0, 0, Bounds.Width - 1, Bounds.Height - 1);
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
			CaptureKeyboard(true);
			if ((buttons & MouseButtons.Left) != 0) {
				CaptureMouse(true);
				if (hasScrollBar && position.X > Bounds.Width - 17) {
					hitScrollBar = true;
					if (position.Y < 17) {
						offset--;
						buttonUpState = ButtonState.Pushed;
					} else if (position.Y > Bounds.Height - 17) {
						offset++;
						buttonDownState = ButtonState.Pushed;
					} else {
						offset = (int)Math.Round((position.Y - 17) * (itemsView.Count - 1) / (double)(Bounds.Height - 17 - 17 - 10));
					}
					if (offset < 0) offset = 0;
					if (offset >= itemsView.Count) offset = itemsView.Count - 1;
					Invalidate();
				} else {
					MouseHandler(position, buttons, true);
				}
			}
		}
		protected override void MouseMove(Point position, MouseButtons buttons) {
			if (hitScrollBar) {
				if (position.Y < 17) {
				} else if (position.Y > Bounds.Height - 17) {
				} else {
					offset = (int)Math.Round((position.Y - 17) * (itemsView.Count - 1) / (double)(Bounds.Height - 17 - 17 - 10));
					if (offset < 0) offset = 0;
					if (offset >= itemsView.Count) offset = itemsView.Count - 1;
					Invalidate();
				}
				return;
			}
			MouseHandler(position, buttons, false);
		}
		protected override void MouseUp(Point position, MouseButtons buttons) {
			if ((buttons & MouseButtons.Left) != 0) {
				CaptureMouse(false);
				buttonUpState = buttonDownState = ButtonState.Normal;
				Invalidate();
				if (hitScrollBar) {
					hitScrollBar = false;
					return;
				}
			}
			if (hitScrollBar) return;
			MouseHandler(position, buttons, false);
		}
		private void MouseHandler(Point position, MouseButtons buttons, Boolean down) {
			if ((buttons & MouseButtons.Left) != 0 && itemsView.Count > 0) {
				int lh = (int)Math.Ceiling(SystemFonts.DefaultFont.GetHeight() / 2.0) * 2;
				int i = (position.Y - 2) / lh + offset;
				if (i < 0) i = 0;
				if (i >= itemsView.Count) i = itemsView.Count - 1;
				Boolean changed = false;
				FBGTreeNode current = itemsView[i];
				if (!ReferenceEquals(highlighted, current)) changed = true;
				highlighted = current;
				if (current.Children.Count > 0 && (new Rectangle(3 + 19 * current.Depth, 2 + lh * (i - offset) + 2, 8, 8)).Contains(position)) {
					if (down) current.Expanded = !current.Expanded;
				} else if (current.HasCheckBox && (new Rectangle(3 + 19 * current.Depth + 14 + 2, 2 + lh * (i - offset), lh, lh)).Contains(position)) {
					if (down) current.Checked = !current.Checked;
				} else if ((new Rectangle(Point.Empty, Bounds.Size)).Contains(position)) {
					SelectedNode = current;
					changed = false;
				}
				if (changed) Invalidate();
			}
		}
		protected override void KeyDown(Keys key) {
			base.KeyDown(key);
			if (key == Keys.Up) {
				int i = itemsView.IndexOf(selected);
				i--;
				if (i >= 0) SelectAndScrollIntoView(itemsView[i]);
			} else if (key == Keys.Down) {
				int i = itemsView.IndexOf(selected);
				i++;
				if (i < itemsView.Count) SelectAndScrollIntoView(itemsView[i]);
			} else if (key == Keys.Left && selected != null) {
				if (selected.Expanded && selected.Children.Count > 0) {
					selected.Expanded = false;
				} else {
					FBGTreeNode tn = selected.Parent as FBGTreeNode;
					if (tn != null) SelectAndScrollIntoView(tn);
				}
			} else if (key == Keys.Right && selected != null) {
				if (!selected.Expanded && selected.Children.Count > 0) {
					selected.Expanded = true;
				} else if (selected.Children.Count > 0) {
					SelectAndScrollIntoView(selected.Children[0]);
				}
			} else if (key == Keys.Space && selected != null) {
				if (selected.HasCheckBox) selected.Checked = !selected.Checked;
			}
		}
		private void SelectAndScrollIntoView(FBGTreeNode tn) {
			int i = itemsView.IndexOf(tn);
			if (i == -1) {
				for (FBGTreeNode tp = tn.Parent as FBGTreeNode; tp != null; tp = tp.Parent as FBGTreeNode) if (!tp.Expanded) tp.Expanded = true;
				i = itemsView.IndexOf(tn);
			}
			if (i != -1) {
				int lh = (int)Math.Ceiling(SystemFonts.DefaultFont.GetHeight() / 2.0) * 2;
				if (i < offset) offset = i;
				offset = Math.Max(offset, i - Bounds.Height / lh + 1);
			}
			highlighted = tn;
			SelectedNode = tn;
		}
	}
}
