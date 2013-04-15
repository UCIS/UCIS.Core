using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using UCIS.VNCServer;
using ThreadingTimer = System.Threading.Timer;

namespace UCIS.FBGUI {
	public interface IFBGControl {
		Rectangle Bounds { get; set; }
		Boolean Visible { get; set; }
		void Paint(Graphics g);
		void MouseMove(Point position, MouseButtons buttons);
		void MouseDown(Point position, MouseButtons buttons);
		void MouseUp(Point position, MouseButtons buttons);
		void KeyDown(Keys key);
		void KeyPress(Char keyChar);
		void KeyUp(Keys key);
		void LostKeyboardCapture();
		void Orphaned();
	}
	public interface IFBGContainerControl {
		Size Size { get; } //Todo: really necessary? Probably not.
		void Invalidate(IFBGControl control, Rectangle rect);
		void AddControl(IFBGControl control);
		void RemoveControl(IFBGControl control);
		Boolean CaptureMouse(IFBGControl control, Boolean capture);
		Boolean CaptureKeyboard(IFBGControl control, Boolean capture);
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
				Parent.Invalidate(this, Rectangle.Union(new Rectangle(Point.Empty, value.Size), new Rectangle(old.X - value.X, old.Y - value.Y, old.Width, old.Height)));
				if (value.Location != old.Location) RaiseEvent(OnMove);
				if (value.Size != old.Size) RaiseEvent(OnResize);
			}
		}
		public virtual Boolean Visible {
			get { return visible; }
			set {
				visible = value;
				Invalidate();
			}
		}
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
			Parent.Invalidate(this, rect);
		}
		void IFBGControl.Paint(Graphics g) { Paint(g); }
		void IFBGControl.MouseMove(Point position, MouseButtons buttons) { MouseMove(position, buttons); }
		void IFBGControl.MouseDown(Point position, MouseButtons buttons) { MouseDown(position, buttons); }
		void IFBGControl.MouseUp(Point position, MouseButtons buttons) { MouseUp(position, buttons); }
		void IFBGControl.KeyDown(Keys g) { KeyDown(g); }
		void IFBGControl.KeyPress(Char g) { KeyPress(g); }
		void IFBGControl.KeyUp(Keys g) { KeyUp(g); }
		void IFBGControl.LostKeyboardCapture() { LostKeyboardCapture(); }
		void IFBGControl.Orphaned() { Orphaned(); }
		protected virtual void Paint(Graphics g) {
			if (!visible) return;
			if (backColor.A != 0) g.Clear(backColor);
			RaiseEvent(OnPaint, new PaintEventArgs(g, Rectangle.Round(g.ClipBounds)));
		}
		protected virtual void MouseMove(Point position, MouseButtons buttons) { RaiseEvent(OnMouseMove, new MouseEventArgs(buttons, 0, position.X, position.Y, 0)); }
		protected virtual void MouseDown(Point position, MouseButtons buttons) { RaiseEvent(OnMouseDown, new MouseEventArgs(buttons, 1, position.X, position.Y, 0)); }
		protected virtual void MouseUp(Point position, MouseButtons buttons) { RaiseEvent(OnMouseUp, new MouseEventArgs(buttons, 1, position.X, position.Y, 0)); }
		protected virtual Boolean CaptureMouse(Boolean capture) {
			return Parent.CaptureMouse(this, capture);
		}
		protected virtual void KeyDown(Keys key) { }
		protected virtual void KeyPress(Char keyChar) { }
		protected virtual void KeyUp(Keys key) { }
		protected virtual Boolean CaptureKeyboard(Boolean capture) {
			return Parent.CaptureKeyboard(this, capture);
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
	public class FBGContainerControl : FBGControl, IFBGContainerControl {
		protected List<IFBGControl> controls = new List<IFBGControl>();
		protected IFBGControl mouseCaptureControl = null;
		protected IFBGControl keyboardCaptureControl = null;
		private Rectangle childarea = Rectangle.Empty;
		public Rectangle ClientRectangle { get { return childarea; } protected set { childarea = value; Invalidate(); } }
		public FBGContainerControl(IFBGContainerControl parent) : base(parent) { }
		Size IFBGContainerControl.Size { get { return childarea.IsEmpty ? Bounds.Size : childarea.Size; } }
		void IFBGContainerControl.AddControl(IFBGControl control) { AddControl(control); }
		protected virtual void AddControl(IFBGControl control) {
			controls.Add(control);
			if (control.Visible) Invalidate(control);
		}
		public virtual void RemoveControl(IFBGControl control) {
			if (controls.Remove(control)) {
				if (control.Visible) Invalidate(control);
				CaptureMouse(control, false);
				CaptureKeyboard(control, false);
				control.Orphaned();
			}
		}
		public virtual Point PointToChild(IFBGControl child, Point point) {
			return point - (Size)child.Bounds.Location - (Size)ClientRectangle.Location;
		}
		public virtual Point PointFromChild(IFBGControl child, Point point) {
			return point + (Size)child.Bounds.Location + (Size)ClientRectangle.Location;
		}
		public virtual void BringControlToFront(IFBGControl control) {
			if (controls.Count == 0) return;
			if (ReferenceEquals(controls[controls.Count - 1], control)) return;
			if (!controls.Remove(control)) return;
			controls.Add(control);
			if (control.Visible) Invalidate(control);
		}
		public virtual void Invalidate(IFBGControl control) {
			Invalidate(new Rectangle(PointFromChild(control, Point.Empty), control.Bounds.Size));
		}
		public virtual void Invalidate(IFBGControl control, Rectangle rect) {
			Invalidate(new Rectangle(PointFromChild(control, rect.Location), rect.Size));
		}
		protected override void Paint(Graphics g) {
			base.Paint(g);
			if (controls == null) return;
			GraphicsState state2 = null;
			if (!childarea.IsEmpty) {
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
				control.Paint(g);
				g.Restore(state);
			}
			if (state2 != null) g.Restore(state2);
		}
		public IFBGControl FindControlAtPosition(Point p) {
			if (!childarea.IsEmpty && !childarea.Contains(p)) return null;
			p.Offset(-childarea.X, -childarea.Y);
			return ((List<IFBGControl>)controls).FindLast(delegate(IFBGControl control) { return control.Visible && control.Bounds.Contains(p); });
		}
		protected override void MouseMove(Point position, MouseButtons buttons) {
			IFBGControl control = mouseCaptureControl != null ? mouseCaptureControl : FindControlAtPosition(position);
			if (control == null) {
				base.MouseMove(position, buttons);
			} else {
				control.MouseMove(PointToChild(control, position), buttons);
			}
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
			IFBGControl control = mouseCaptureControl != null ? mouseCaptureControl : FindControlAtPosition(position);
			if (control == null) {
				base.MouseDown(position, buttons);
			} else {
				control.MouseDown(PointToChild(control, position), buttons);
			}
		}
		protected override void MouseUp(Point position, MouseButtons buttons) {
			IFBGControl control = mouseCaptureControl != null ? mouseCaptureControl : FindControlAtPosition(position);
			if (control == null) {
				base.MouseUp(position, buttons);
			} else {
				control.MouseUp(PointToChild(control, position), buttons);
			}
		}
		Boolean IFBGContainerControl.CaptureMouse(IFBGControl control, Boolean capture) { return CaptureMouse(control, capture); }
		protected Boolean CaptureMouse(IFBGControl control, Boolean capture) {
			if (capture && !ReferenceEquals(mouseCaptureControl, null)) return false;
			if (!capture && !ReferenceEquals(mouseCaptureControl, control)) return false;
			if (!CaptureMouse(capture)) return false;
			mouseCaptureControl = capture ? control : null;
			return true;
		}
		protected override void KeyDown(Keys key) {
			if (ReferenceEquals(keyboardCaptureControl, null)) base.KeyDown(key);
			else keyboardCaptureControl.KeyDown(key);
		}
		protected override void KeyPress(Char keyChar) {
			if (ReferenceEquals(keyboardCaptureControl, null)) base.KeyPress(keyChar);
			else keyboardCaptureControl.KeyPress(keyChar);
		}
		protected override void KeyUp(Keys key) {
			if (ReferenceEquals(keyboardCaptureControl, null)) base.KeyUp(key);
			else keyboardCaptureControl.KeyUp(key);
		}
		Boolean IFBGContainerControl.CaptureKeyboard(IFBGControl control, Boolean capture) { return CaptureKeyboard(control, capture); }
		protected Boolean CaptureKeyboard(IFBGControl control, Boolean capture) {
			if (!capture && !ReferenceEquals(keyboardCaptureControl, control)) return false;
			if (!CaptureKeyboard(capture)) return false;
			IFBGControl prev = keyboardCaptureControl;
			keyboardCaptureControl = capture ? control : null;
			if (prev != null) LostKeyboardCapture();
			return true;
		}
		protected override void LostKeyboardCapture() {
			base.LostKeyboardCapture();
			if (keyboardCaptureControl != null) keyboardCaptureControl.LostKeyboardCapture();
		}
		protected override void Orphaned() {
			base.Orphaned();
			IFBGControl[] c = controls.ToArray();
			controls.Clear();
			foreach (IFBGControl control in c) control.Orphaned();
			mouseCaptureControl = null;
			keyboardCaptureControl = null;
		}
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
		public override void RemoveControl(IFBGControl control) {
			base.RemoveControl(control);
			if (dockStyles.Remove(control)) DoLayout();
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
			} else if (controls.Contains(control)) {
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
			} else if (controls.Contains(control)) {
				dockStyles.Remove(control);
				anchorStyles[control] = style;
			}
		}
		public void SetAnchor(IFBGControl control, AnchorStyles style, int value) {
			if (controls.Contains(control)) {
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
		public FBGGroupBox(IFBGContainerControl parent)
			: base(parent) {
			ClientRectangle = new Rectangle(1, 15, Bounds.Width - 2, Bounds.Height - 17);
		}
		public override Rectangle Bounds {
			get { return base.Bounds; }
			set {
				ClientRectangle = new Rectangle(1, 15, value.Width - 2, value.Height - 17);
				base.Bounds = value;
			}
		}
		public String Text { get { return text; } set { if (text == value) return; text = value; Invalidate(new Rectangle(0, 0, Bounds.Width, 15)); } }
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawRectangle(Pens.Gray, 0, 6, Bounds.Width - 1, Bounds.Height - 7);
			SizeF ss = g.MeasureString(Text, SystemFonts.DefaultFont, new Size(Bounds.Width - 10 - 9, 15));
			g.FillRectangle(SystemBrushes.Control, 9, 0, ss.Width, ss.Height);
			g.DrawString(Text, SystemFonts.DefaultFont, Brushes.DarkBlue, new Rectangle(9, 0, Bounds.Width - 10 - 9, 15));
		}
	}
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

		Size IFBGContainerControl.Size { get { return ClientSize; } }
		void IFBGContainerControl.Invalidate(IFBGControl control, Rectangle rect) {
			Invalidate(new Rectangle(PointFromChild(control, rect.Location), rect.Size));
		}
		void IFBGContainerControl.AddControl(IFBGControl control) {
			if (!ReferenceEquals(childControl, null)) throw new InvalidOperationException("This container can have only one child control");
			childControl = control;
			control.Bounds = new Rectangle(Point.Empty, ClientSize);
			Invalidate(control.Bounds);
		}
		void IFBGContainerControl.RemoveControl(IFBGControl control) {
			if (!ReferenceEquals(childControl, control)) return;
			childControl = null;
			Invalidate(control.Bounds);
			if (mouseCaptureControl == control) mouseCaptureControl = null;
			if (keyboardCaptureControl == control) control = null;
			control.Orphaned();
		}
		Boolean IFBGContainerControl.CaptureMouse(IFBGControl control, Boolean capture) {
			if (capture && !ReferenceEquals(mouseCaptureControl, null)) return false;
			if (!capture && !ReferenceEquals(mouseCaptureControl, control)) return false;
			mouseCaptureControl = capture ? control : null;
			return true;
		}
		Boolean IFBGContainerControl.CaptureKeyboard(IFBGControl control, Boolean capture) {
			if (!capture && !ReferenceEquals(keyboardCaptureControl, control)) return false;
			keyboardCaptureControl = capture ? control : null;
			return true;
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
			childControl.Paint(g);
			g.Restore(state);
		}
		protected override void OnResize(EventArgs e) {
			if (!ReferenceEquals(childControl, null)) childControl.Bounds = new Rectangle(Point.Empty, ClientSize);
			base.OnResize(e);
		}
		protected override void OnMouseDown(MouseEventArgs e) {
			base.OnMouseDown(e);
			IFBGControl control = mouseCaptureControl != null ? mouseCaptureControl : childControl;
			if (control != null) control.MouseDown(PointToChild(control, e.Location), e.Button);
		}
		protected override void OnMouseUp(MouseEventArgs e) {
			base.OnMouseUp(e);
			IFBGControl control = mouseCaptureControl != null ? mouseCaptureControl : childControl;
			if (control != null) control.MouseUp(PointToChild(control, e.Location), e.Button);
		}
		protected override void OnMouseMove(MouseEventArgs e) {
			IFBGControl control = mouseCaptureControl != null ? mouseCaptureControl : childControl;
			if (control != null) control.MouseMove(PointToChild(control, e.Location), e.Button);
		}
		protected override bool IsInputChar(char charCode) {
			return true;
		}
		protected override bool IsInputKey(Keys keyData) {
			return true;
		}
		protected override void OnKeyDown(KeyEventArgs e) {
			//base.OnKeyDown(e);
			if (!ReferenceEquals(keyboardCaptureControl, null)) keyboardCaptureControl.KeyDown(e.KeyData);
		}
		protected override void OnKeyPress(KeyPressEventArgs e) {
			//base.OnKeyPress(e);
			if (!ReferenceEquals(keyboardCaptureControl, null)) keyboardCaptureControl.KeyPress(e.KeyChar);
		}
		protected override void OnKeyUp(KeyEventArgs e) {
			//base.OnKeyUp(e);
			if (!ReferenceEquals(keyboardCaptureControl, null)) keyboardCaptureControl.KeyUp(e.KeyData);
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
			this.Size = image.Size;
		}

		private const String ArrowCursorImageData = "iVBORw0KGgoAAAANSUhEUgAAAAwAAAAVCAYAAAByrA+0AAAAkUlEQVR42pWTORbEMAhDJb3cYNqUuf+JUk6bM2iKLM9DvGAqG/MFxpgAfKwbkTQBwOe7ewqwnYZ0L7KQyk0GUnSMINWcPUgtpRakXr01SKOuREiZ3pcQz329KeR7YpZaUCkQ50wjxWYGko8aSduGbZD8m2bF4NQsxeBj3XiX92rrzOfpvkMrizBpS+/wyuLynj9U+GDtLEEVuQAAAABJRU5ErkJggg==";
		public static readonly FBGCursor ArrowCursor = new FBGCursor(Image.FromStream(new MemoryStream(Convert.FromBase64String(ArrowCursorImageData))), Point.Empty);
	}
	public class FBGRenderer : FBGContainerControl, IDisposable {
		private FBGCursor cursor = null;
		private Point cursorposition = Point.Empty;
		public IFramebuffer Framebuffer { get; private set; }
		private Bitmap Frontbuffer = null;
		protected Object RenderLock = new object();
		public event EventHandler<InvalidateEventArgs> Painted;
		protected Size size = Size.Empty;
		private ThreadingTimer PaintTimer = null;
		private Boolean PaintScheduled = false;
		private int PaintDelay = 0;
		public Boolean SuspendDrawing {
			get { return suspenddrawing; }
			set {
				lock (RenderLock) {
					suspenddrawing = value;
					if (!value) {
						Refresh(DirtyRectangle);
						DirtyRectangle = Rectangle.Empty;
					}
				}
			}
		}
		public int RedrawDelay {
			get { return PaintDelay; }
			set {
				lock (RenderLock) {
					if (value < 0) throw new ArgumentOutOfRangeException("value");
					PaintDelay = value;
					if (PaintDelay == 0) {
						if (PaintTimer != null) PaintTimer.Dispose();
						PaintTimer = null;
						if (PaintScheduled) PaintTimerCallback(null);
					} else {
						PaintTimer = new ThreadingTimer(PaintTimerCallback);
					}
				}
			}
		}
		private Boolean suspenddrawing = false;
		private Rectangle DirtyRectangle;

		public FBGCursor Cursor {
			get { return cursor; }
			set {
				cursor = value;
				Invalidate();
			}
		}
		public Point CursorPosition {
			get { return cursorposition; }
			set {
				if (cursorposition == value) return;
				Point oldposition = cursorposition;
				cursorposition = value;
				if (cursor == null) return;
				Size s = cursor.Size;
				if (s.Width == 0 && s.Height == 0) s = new Size(32, 32);
				Rectangle r = Rectangle.Union(new Rectangle(oldposition, s), new Rectangle(cursorposition, s));
				r.Offset(-cursor.Hotspot.X, -cursor.Hotspot.Y);
				if (Environment.OSVersion.Platform == PlatformID.Unix) {
					r = Rectangle.Union(r, Rectangle.Union(new Rectangle(oldposition.X - 2, oldposition.Y - 2, 4, 4), new Rectangle(cursorposition.X - 2, cursorposition.Y - 2, 4, 4)));
				}
				Invalidate(r);
			}
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
		public override void Invalidate(Rectangle rect) {
			if (rect.Width == 0 || rect.Height == 0) return;
			lock (RenderLock) {
				if (SuspendDrawing || PaintTimer != null) {
					DirtyRectangle = DirtyRectangle.IsEmpty ? rect : Rectangle.Union(DirtyRectangle, rect);
					if (!SuspendDrawing && !PaintScheduled) {
						PaintScheduled = true;
						PaintTimer.Change(PaintDelay, Timeout.Infinite);
					}
				} else {
					Refresh(rect);
				}
			}
		}
		private void PaintTimerCallback(Object state) {
			try {
				lock (RenderLock) {
					PaintScheduled = false;
					Refresh(DirtyRectangle);
					DirtyRectangle = Rectangle.Empty;
				}
			} catch { }
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
		protected override void Paint(Graphics g) {
			base.Paint(g);
			if (Cursor != null) {
				Point r = CursorPosition;
				r.Offset(-cursor.Hotspot.X, -cursor.Hotspot.Y);
				g.DrawImageUnscaled(Cursor.Image, r);
			}
		}
		protected override Boolean CaptureMouse(Boolean capture) {
			return true;
		}
		protected override Boolean CaptureKeyboard(bool capture) {
			return true;
		}
		public virtual void Dispose() {
			lock (RenderLock) {
				if (PaintTimer != null) PaintTimer.Dispose();
				PaintTimer = null;
			}
			Orphaned();
			if (Frontbuffer != null) Frontbuffer.Dispose();
		}
		public Bitmap LockBitmapBuffer() {
			Monitor.Enter(RenderLock);
			return Frontbuffer;
		}
		public void UnlockBitmapBuffer() {
			Monitor.Exit(RenderLock);
		}
		public new void MouseMove(Point position, MouseButtons buttons) { base.MouseMove(position, buttons); }
		public new void MouseDown(Point position, MouseButtons buttons) { base.MouseDown(position, buttons); }
		public new void MouseUp(Point position, MouseButtons buttons) { base.MouseUp(position, buttons); }
		public new void KeyDown(Keys key) { base.KeyDown(key); }
		public new void KeyPress(Char key) { base.KeyPress(key); }
		public new void KeyUp(Keys key) { base.KeyUp(key); }
	}
	public class FBGForm : FBGDockContainer {
		private Point prevPosition = Point.Empty;
		private NonClientOps moveresize = 0;
		private String text = String.Empty;
		public event EventHandler TextChanged;
		public Boolean Sizable { get; set; }
		public Boolean Movable { get; set; }
		public Boolean Closable { get; set; }
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
		public FBGForm(IFBGContainerControl parent) : base(parent) {
			BackColor = SystemColors.Control;
			ClientRectangle = new Rectangle(4, 22, Bounds.Width - 8, Bounds.Height - 26);
			Sizable = true;
			Movable = true;
			Closable = false;
		}
		public override Rectangle Bounds {
			get { return base.Bounds; }
			set {
				ClientRectangle = new Rectangle(4, 22, value.Width - 8, value.Height - 26);
				base.Bounds = value;
			}
		}
		public String Text { get { return text; } set { if (text == value) return; text = value; Invalidate(new Rectangle(0, 0, Bounds.Width, 20)); RaiseEvent(TextChanged); } }
		protected override void MouseDown(Point p, MouseButtons buttons) {
			NonClientOps mr = 0;
			if ((buttons & MouseButtons.Left) != 0) {
				if ((new Rectangle(Bounds.Width - 5 - 14, 4, 14, 14)).Contains(p)) {
					mr = NonClientOps.ButtonClose;
				} else {
					if (Sizable) {
						if (Movable) {
							if (p.X < 4) mr |= NonClientOps.ResizeLeft;
							if (p.Y < 4) mr |= NonClientOps.ResizeTop;
						}
						if (p.X >= Bounds.Width - 4) mr |= NonClientOps.ResizeRight;
						if (p.Y >= Bounds.Height - 4) mr |= NonClientOps.ResizeBottom;
					}
					if (mr == 0 && Movable && p.Y < 20) mr = NonClientOps.Move;
				}
			}
			if (mr != 0) {
				moveresize = mr;
				prevPosition = p;
				CaptureMouse(true);
			} else {
				base.MouseDown(p, buttons);
			}
		}
		protected override void MouseMove(Point position, MouseButtons buttons) {
			if (moveresize == 0) {
				base.MouseMove(position, buttons);
			} else if ((moveresize & NonClientOps.MoveResize) != 0) {
				Rectangle b = Bounds;
				int dx = position.X - prevPosition.X;
				int dy = position.Y - prevPosition.Y;
				if (moveresize == NonClientOps.Move) b.Offset(dx, dy);
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
				if (b.Width < 55) b.Width = 55;
				if (b.Height < 25) b.Height = 25;
				Bounds = b;
			}
		}
		protected override void MouseUp(Point position, MouseButtons buttons) {
			if (moveresize == 0) {
				base.MouseUp(position, buttons);
			} else if ((buttons & MouseButtons.Left) != 0) {
				MouseMove(position, buttons);
				CaptureMouse(false);
				if (moveresize == NonClientOps.ButtonClose && (new Rectangle(Bounds.Width - 5 - 14, 4, 14, 14)).Contains(position) && Closable) Close();
				moveresize = 0;
			}
		}
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawRectangle(Pens.Gray, 0, 0, Bounds.Width - 1, Bounds.Height - 1);
			g.DrawRectangle(Pens.LightGray, 1, 1, Bounds.Width - 3, Bounds.Height - 3);
			g.DrawRectangle(Pens.DarkGray, 2, 20, Bounds.Width - 5, Bounds.Height - 23);
			g.DrawRectangle(Pens.Gray, 3, 21, Bounds.Width - 7, Bounds.Height - 25);
			using (Brush b = new LinearGradientBrush(new Rectangle(0, 1, 1, 18), Color.Gray, Color.LightGray, LinearGradientMode.Vertical))
				g.FillRectangle(b, 2, 2, Bounds.Width - 4, 18);

			g.DrawString(Text, SystemFonts.CaptionFont, Brushes.Black, 4, 1);

			g.DrawRectangle(Pens.Gray, Bounds.Width - 5 - 14, 4, 14, 14);
			g.DrawRectangle(Pens.Gray, Bounds.Width - 5 - 14 - 16, 4, 14, 14);
			g.DrawRectangle(Pens.Gray, Bounds.Width - 5 - 14 - 32, 4, 14, 14);
			using (Brush b = new LinearGradientBrush(new Rectangle(0, 5, 1, 13), Color.LightGray, Color.DarkGray, LinearGradientMode.Vertical)) {
				g.FillRectangle(b, Bounds.Width - 5 - 14 + 1, 5, 13, 13);
				g.FillRectangle(b, Bounds.Width - 5 - 14 + 1 - 16, 5, 13, 13);
				g.FillRectangle(b, Bounds.Width - 5 - 14 + 1 - 32, 5, 13, 13);
			}
			if (Closable) {
				g.DrawLine(Pens.Black, Bounds.Width - 5 - 14 + 3, 4 + 3, Bounds.Width - 5 - 14 + 14 - 3, 4 + 14 - 3);
				g.DrawLine(Pens.Black, Bounds.Width - 5 - 14 + 3, 4 + 14 - 3, Bounds.Width - 5 - 14 + 14 - 3, 4 + 3);
			}
		}
		public void Close() {
			Parent.RemoveControl(this);
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
		public override void RemoveControl(IFBGControl control) {
			windowcontainer.RemoveControl(control);
			windowstates.Remove(control);
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
		}
		private String text = String.Empty;
		private Char passwordChar = (Char)0;
		private Font font = new Font(FontFamily.GenericMonospace, 10);
		private Brush brush = SystemBrushes.ControlText;
		private Boolean hasKeyboardFocus = false;
		public String Text { get { return text; } set { text = value; if (CaretPosition > text.Length) CaretPosition = text.Length; Invalidate(); RaiseEvent(TextChanged); } }
		public Font Font { get { return font; } set { font = value; Invalidate(); } }
		public Brush Brush { get { return brush; } set { brush = value; Invalidate(); } }
		public Color Color { set { Brush = new SolidBrush(value); } }
		public Int32 CaretPosition { get; private set; }
		public Char PasswordChar { get { return passwordChar; } set { passwordChar = value; Invalidate(); } }
		public event EventHandler TextChanged;
		public event KeyPressEventHandler OnKeyPress;
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawRectangle(Pens.Gray, 0, 0, Bounds.Width - 1, Bounds.Height - 1);
			using (StringFormat sf_nonprinting = new StringFormat(StringFormat.GenericTypographic)) {
				sf_nonprinting.Trimming = StringTrimming.None;
				sf_nonprinting.FormatFlags = StringFormatFlags.DisplayFormatControl | StringFormatFlags.MeasureTrailingSpaces;
				sf_nonprinting.HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None;

				float x = 1;
				float y = 1;
				float w = Width - 2;
				if (hasKeyboardFocus && CaretPosition == 0) {
					g.DrawLine(Pens.Black, x + 2, 2, x + 2, Height - 4);
				}
				String c = passwordChar == 0 ? null : new String(passwordChar, 1);
				for (int i = 0; i < text.Length; i++) {
					if (passwordChar == 0) c = text.Substring(i, 1);
					SizeF s = g.MeasureString(c, font, (int)Math.Ceiling(w), sf_nonprinting);
					g.DrawString(c, font, brush, x, y);
					x += (float)Math.Ceiling(s.Width);
					w -= (float)Math.Ceiling(s.Width);

					if (hasKeyboardFocus && i == CaretPosition - 1) {
						g.DrawLine(Pens.Black, x + 2, 2, x + 2, Height - 4);
					}
				}
			}
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
			hasKeyboardFocus = CaptureKeyboard(true);
			CaretPosition = text.Length;
			float x = 1;
			String c = passwordChar == 0 ? null : new String(passwordChar, 1);
			for (int i = 0; i < text.Length; i++) {
				if (passwordChar == 0) c = text.Substring(i, 1);
				Size s;
				try {
					s = TextRenderer.MeasureText(c, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
				} catch (Exception) {
					break;
				}
				x += s.Width;
				if (position.X < x) {
					CaretPosition = i;
					break;
				}
			}
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
				//Hackish and not completely right...
				//Todo: borrowed from mono... possible licencing issues!?
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
					// sadly enough, the rectangle gets always filled with a hatchbrush
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
		PictureBoxSizeMode sizeMode = PictureBoxSizeMode.Normal;
		Rectangle imageRect;
		public Image Image { get { return image; } set { image = value; UpdateImageRect(false); } }
		public FBGImageBox(IFBGContainerControl parent) : base(parent) { }
		public PictureBoxSizeMode SizeMode { get { return sizeMode; } set { sizeMode = value; UpdateImageRect(false); } }
		public override Rectangle Bounds {
			get {
				return base.Bounds;
			}
			set {
				UpdateImageRect(true);
				base.Bounds = value;
			}
		}
		private void UpdateImageRect(Boolean boundsset) {
			if (image == null) return;
			if (!boundsset && sizeMode == PictureBoxSizeMode.AutoSize) {
				Size = Image.Size;
				return;
			}
			switch (sizeMode) {
				case PictureBoxSizeMode.AutoSize:
				case PictureBoxSizeMode.Normal:
					imageRect = new Rectangle(Point.Empty, image.Size);
					break;
				case PictureBoxSizeMode.CenterImage:
					imageRect = new Rectangle(Width / 2 - image.Width / 2, Height / 2 - Image.Height / 2, Width, Height);
					break;
				case PictureBoxSizeMode.StretchImage:
					imageRect = new Rectangle(Point.Empty, Size);
					break;
				case PictureBoxSizeMode.Zoom:
					float xrat = (float)Width / (float)image.Width;
					float yrat = (float)Height / (float)image.Height;
					float rat = Math.Min(xrat, yrat);
					SizeF dispsize = new SizeF(image.Width * rat, image.Height * rat);
					imageRect = Rectangle.Round(new RectangleF(Width / 2f - dispsize.Width / 2f, Height / 2f - dispsize.Height / 2f, dispsize.Width, dispsize.Height));
					break;
			}
			if (!boundsset) Invalidate();
		}
		protected override void Paint(Graphics g) {
			if (!Visible) return;
			base.Paint(g);
			if (image != null) g.DrawImage(image, imageRect);
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
		public void AddItem(Object item) {
			items.Add(item);
			Invalidate();
		}
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawRectangle(Pens.DarkBlue, 0, 0, Bounds.Width - 1, Bounds.Height - 1);
			int lh = (int)Math.Ceiling(SystemFonts.DefaultFont.GetHeight());
			int th = lh * items.Count;
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
			if (y < th) hasScrollBar = true;
			if (hasScrollBar) {
				int xoff = Bounds.Width - 17;
				using (Brush b = new LinearGradientBrush(new Rectangle(xoff, 0, 17, 1), Color.LightGray, Color.White, LinearGradientMode.Horizontal))
					g.FillRectangle(b, xoff, 17, 16, Bounds.Height - 17 - 17);
				ControlPaint.DrawScrollButton(g, xoff, 1, 16, 16, ScrollButton.Up, buttonUpState);
				ControlPaint.DrawScrollButton(g, xoff, Bounds.Height - 17, 16, 16, ScrollButton.Down, buttonDownState);
				g.DrawRectangle(Pens.Black, new Rectangle(xoff, 17 + offset * (Bounds.Height - 17 - 17 - 20) / (items.Count - 1), 15, 20));
			}
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
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
						offset = (int)Math.Round((position.Y - 17) * (items.Count - 1) / (double)(Bounds.Height - 17 - 17 - 10));
					}
					if (offset < 0) offset = 0;
					if (offset >= items.Count) offset = items.Count - 1;
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
					offset = (int)Math.Round((position.Y - 17) * (items.Count - 1) / (double)(Bounds.Height - 17 - 17 - 10));
					if (offset < 0) offset = 0;
					if (offset >= items.Count) offset = items.Count - 1;
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
	public class FBGDomainUpDown : FBGControl {
		private List<Object> items = new List<object>();
		private int selectedIndex = -1;
		private ButtonState buttonUpState = ButtonState.Normal;
		private ButtonState buttonDownState = ButtonState.Normal;
		private Converter<Object, String> itemFormatter = null;
		public Boolean AllowSelectEmpty { get; set; }
		public Converter<Object, String> ItemFormatter { get { return itemFormatter; } set { itemFormatter = value; Invalidate(); } }
		public event EventHandler SelectedIndexChanged;
		public FBGDomainUpDown(IFBGContainerControl parent)
			: base(parent) {
			BackColor = Color.White;
		}
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
		private void FixSelectedIndex(int change) {
			int value = selectedIndex;
			if (value == 0 && change == -1 && !AllowSelectEmpty) change = 0;
			value += change;
			if (value < -1) value = -1;
			if (value >= items.Count) value = items.Count - 1;
			SelectedIndex = value;
		}
		protected override void Paint(Graphics g) {
			base.Paint(g);
			g.DrawRectangle(Pens.DarkBlue, 0, 0, Bounds.Width - 1, Bounds.Height - 1);
			int lh = (int)Math.Ceiling(SystemFonts.DefaultFont.GetHeight());
			if (selectedIndex == -1) {
				g.FillRectangle(Brushes.DarkGray, 2, 2, Bounds.Width - 4 - 16, Bounds.Height - 4);
			} else {
				using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap)) {
					sf.LineAlignment = StringAlignment.Center;
					Object item = items[selectedIndex];
					String text = itemFormatter == null ? (item == null ? String.Empty : item.ToString()) : itemFormatter(item);
					g.FillRectangle(Brushes.LightGray, 2, 2, Bounds.Width - 4 - 16, Bounds.Height - 4);
					g.DrawString(text, SystemFonts.DefaultFont, SystemBrushes.WindowText, new Rectangle(3, 2, Bounds.Width - 6 - 16, Bounds.Height - 4), sf);
				}
			}
			int xoff = Bounds.Width - 17;
			int he = (Bounds.Height - 2) / 2;
			ControlPaint.DrawScrollButton(g, xoff, 1, 16, he, ScrollButton.Up, buttonUpState);
			ControlPaint.DrawScrollButton(g, xoff, Bounds.Height - he - 1, 16, he, ScrollButton.Down, buttonDownState);
		}
		protected override void MouseDown(Point position, MouseButtons buttons) {
			CaptureKeyboard(true);
			if ((buttons & MouseButtons.Left) != 0) {
				CaptureMouse(true);
				if (position.X > Bounds.Width - 17) {
					if (position.Y < Bounds.Height / 2) {
						buttonUpState = ButtonState.Pushed;
						FixSelectedIndex(-1);
					} else {
						buttonDownState = ButtonState.Pushed;
						FixSelectedIndex(1);
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
			if (key == Keys.Down) {
				FixSelectedIndex(1);
			} else if (key == Keys.Up) {
				FixSelectedIndex(-1);
			}
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
