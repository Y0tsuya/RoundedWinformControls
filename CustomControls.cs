using System.Text;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Timers;
using System.Runtime.InteropServices;

// namespace is needed for controls to display in IDE Toolbox
namespace CustomControls {

	public enum ErrorLevel { LOG_INFO, LOG_ALERT, LOG_WARNING, LOG_ERROR, LOG_EXTENDED };

	public class VisualParams {
		public Color GridBG;
		public Color GridColFG;
		public Color GridColBG;
		public Color GridRowFG;
		public Color GridRowBG;
		public Color GridSelFG;
		public Color GridSelBG;
		public Color ControlFG;
		public Color ControlBG;

		public VisualParams() {
			GridBG = SystemColors.ControlLight;
			GridColFG = SystemColors.ControlText;
			GridColBG = SystemColors.ControlDark;
			GridRowFG = SystemColors.ControlText;
			GridRowBG = SystemColors.ControlLight;
			GridSelFG = SystemColors.ControlText;
			GridSelBG = SystemColors.Control;
			ControlFG = SystemColors.ControlText;
			ControlBG = SystemColors.Control;
		}
	}

	interface IVisualParameters {
		public VisualParams Visuals { set; }
	}

	public class RoundedPanel : Panel {
		private ShapeSR InnerBorderShape;
		private int InnerBorderRadius;
		private Color InnerBorderColor;
		private float InnerBorderWidth;
		private float InnerBorderMargin;

		public RoundedPanel() {
			InnerBorderShape = ShapeSR.ROUNDED;
			InnerBorderRadius = 5;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 1.0f;
		}
		//Properties
		[Browsable(true)]
		public ShapeSR BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Height = Font.Height + 2 + ((InnerBorderShape == ShapeSR.ROUNDED) ? (int)(InnerBorderWidth * 2) : 0);
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Invalidate();
			}
		}
		[Browsable(false)]

		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}

		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			pevent.Graphics.Clear(StyleHelper.GetParentColor(this));
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			GraphicsPath penPath = StyleHelper.GetGraphicsPathSR(this.ClientRectangle, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, penPath);
			}
			e.Graphics.SetClip(penPath);
		}
	}

	public class HyperLogView : Control, IVisualParameters {
		private RoundedVScrollBar VScroll;
		private PictureBox ListPanel;
		public List<HyperLogItem> Items;
		private object IndexLock;
		private int Index;
		private ImageList InnerImageList;
		public SolidBrush InfoBrush, AlertBrush, WarningBrush, ErrorBrush, ExtendedBrush;
		private bool IsPainting;
		private System.Windows.Forms.Timer UiTimer;
		private bool Dirty;
		private bool Deferred;
		private bool Defer;
		private const int RowSpace = 2;
		private int ItemsToPaint;
		private Color InnerForeColor;
		private Color InnerBackColor;
		private System.Timers.Timer SmoothTimer;
		const int SMOOTH_SCROLL_PERIOD = 17;
		const int SMOOTH_SCROLL_STEPS = 6;
		private int SmoothScrollDelta;
		private int CurrentLogPosition;
		private ShapeSR InnerBorderShape;
		private int InnerBorderRadius;
		private Color InnerBorderColor;
		private float InnerBorderWidth;
		private float InnerBorderMargin;
		private const int VSCROLL_WIDTH = 20;
		private const int SPACING = 6;

		public class HyperLogItem {
			public int Index;
			public int Parent;
			public int ImageIndex;
			public string Text;
			public ErrorLevel Level;
			public Color ExtendedColor;
			public bool Sub;

			public HyperLogItem(string text, ErrorLevel level, Color extendedColor, bool sub) {
				Index = 0;
				Parent = -1;
				ImageIndex = 0;
				Text = text;
				Level = level;
				ExtendedColor = extendedColor;
				Sub = sub;
			}
		}

		public HyperLogView() {
			ListPanel = new PictureBox();
			VScroll = new RoundedVScrollBar();
			this.Controls.Add(VScroll);
			this.Controls.Add(ListPanel);
			VScroll.ValueChanged += VScroll_Scrolled;
			ListPanel.Paint += List_Paint;
			IndexLock = new object();
			//Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			InfoBrush = new SolidBrush(Color.Green);
			AlertBrush = new SolidBrush(Color.DarkCyan);
			WarningBrush = new SolidBrush(Color.DarkOrange);
			ErrorBrush = new SolidBrush(Color.Red);
			Items = new List<HyperLogItem>();
			UiTimer = new System.Windows.Forms.Timer();
			UiTimer.Tick += UiTimer_Tick;
			UiTimer.Interval = 250;
			SmoothTimer = new System.Timers.Timer();
			SmoothTimer.Interval = SMOOTH_SCROLL_PERIOD;
			SmoothTimer.Elapsed += SmoothTimer_Tick;
			Dirty = false;
			Defer = false;
			ItemsToPaint = 1;
			InnerForeColor = Color.Black;
			InnerBackColor = Color.WhiteSmoke;
			VScroll.ForeColor = InnerForeColor;
			VScroll.BackColor = InnerBackColor;
			VScroll.Visible = false;
			CurrentLogPosition = 0;
			InnerBorderShape = ShapeSR.ROUNDED;
			InnerBorderRadius = 5;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 1.0f;
			RepositionControls();
		}

		//Properties
		[Browsable(true)]

		public ImageList ImageList {
			get { return InnerImageList; }
			set { InnerImageList = value; }
		}

		public override Color ForeColor {
			get { return InnerForeColor; }
			set {
				InnerForeColor = value;
				VScroll.ForeColor = value;
				this.Invalidate();
			}
		}
		public override Color BackColor {
			get { return InnerBackColor; }
			set {
				InnerBackColor = value;
				this.Invalidate();
			}
		}

		public ShapeSR BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				RepositionControls();
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				RepositionControls();
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				RepositionControls();
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				RepositionControls();
				this.Invalidate();
			}
		}

		[Browsable(false)]

		public VisualParams Visuals {
			set {
				VScroll.BackColor = value.ControlBG;
				VScroll.ThumbColor = value.ControlFG;
				InnerBackColor = value.GridBG;
				this.Invalidate();
			}
		}

		protected override Size DefaultSize {
			get { return new Size(300, 400); }
		}

		public bool DeferredUpdate {
			set {
				Deferred = value;
				if (value) {
					UiTimer.Enabled = true;
					UiTimer.Start();
				} else {
					UiTimer.Stop();
					UiTimer.Enabled = false;
				}
			}
		}

		private void SmoothScrollTo(int line) {
			if (VScroll.Value == CurrentLogPosition) {
				// just do a repaint
				ListPanel.Invalidate();
			}
			SmoothScrollDelta = (VScroll.Value - CurrentLogPosition) / SMOOTH_SCROLL_STEPS;
			if ((VScroll.Value > CurrentLogPosition) && (SmoothScrollDelta == 0)) {
				SmoothScrollDelta = 1;
			} else if ((VScroll.Value < CurrentLogPosition) && (SmoothScrollDelta == 0)) {
				SmoothScrollDelta = -1;
			}
			SmoothTimer.Start();
		}

		// must get this event routed from parent form
		public void OnKeyDown(KeyEventArgs e) {
			switch (e.KeyCode) {
				case Keys.PageUp:
					VScroll.Value -= VScroll.ViewportSize;
					break;
				case Keys.PageDown:
					VScroll.Value += VScroll.ViewportSize;
					break;
				default: break;
			}
			//Debug.WriteLine("KeyDown");
		}

		// must get this event routed from parent form
		public void OnKeyUp(KeyEventArgs e) {
			//Debug.WriteLine("KeyUp");
		}

		protected override void OnMouseWheel(MouseEventArgs e) {
			//base.OnMouseWheel(e);
			if ((e.Delta / 120) >= 1) {
				VScroll.Value--;
			} else if ((e.Delta / 120) <= -1) {
				VScroll.Value++;
			}
			//ListPanel.Invalidate();
		}

		protected override void OnResize(EventArgs e) {
			base.OnResize(e);
			RepositionControls();
		}

		private void RepositionControls() {
			int offset = (int)Math.Round(InnerBorderRadius * 0.4);
			ListPanel.Top = offset;
			ListPanel.Left = offset;
			ListPanel.Width = this.Width - offset * 2 - VSCROLL_WIDTH - SPACING;
			ListPanel.Height = this.Height - offset * 2;
			VScroll.Top = offset;
			VScroll.Left = ListPanel.Right + SPACING;
			VScroll.Width = VSCROLL_WIDTH;
			VScroll.Height = this.Height - offset * 2;
			ItemsToPaint = this.Height / (Font.Height + RowSpace);
			VScroll.ViewportSize = ItemsToPaint;
		}

		private void UiTimer_Tick(object sender, EventArgs e) {
			if (Defer) {
				Defer = false;
				// hold off on update;
			} else {
				if (Dirty) {
					if (VScroll.Maximum != Items.Count) {
						VScroll.Maximum = Items.Count;
						int itemsToPaint = ListPanel.Height / (Font.Height + RowSpace);
						VScroll.Value = Math.Max(0, VScroll.Maximum - itemsToPaint / 3);
					}
					//ListPanel.Invalidate();
					SmoothScrollTo(VScroll.Value);
					Dirty = false;
				}
			}
		}

		private void SmoothTimer_Tick(object sender, EventArgs e) {
			CurrentLogPosition += SmoothScrollDelta;
			if (SmoothScrollDelta > 0) {
				if (CurrentLogPosition >= VScroll.Value) {
					SmoothTimer.Stop();
					CurrentLogPosition = VScroll.Value;
				}
			} else {
				if (CurrentLogPosition <= VScroll.Value) {
					SmoothTimer.Stop();
					CurrentLogPosition = VScroll.Value;
				}
			}
			ListPanel.Invalidate();
		}

		private void VScroll_Scrolled(object sender, EventArgs e) {
			//ListPanel.Invalidate();
			SmoothScrollTo(VScroll.Value);
		}

		private void List_Paint(object sender, PaintEventArgs e) {
			int pos = CurrentLogPosition;
			IsPainting = true;
			SolidBrush brush = InfoBrush;
			int image = 0;
			int itemsPainted = 0;
			Point imgPt = new Point(RowSpace, RowSpace);
			Point txtPt = new Point(RowSpace, RowSpace);
			Image icon;
			int fineVoffset = 0;
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			e.Graphics.Clear(InnerBackColor);
			VScroll.Visible = VScroll.Maximum >= ItemsToPaint;
			if (pos > VScroll.Maximum - ItemsToPaint) pos = VScroll.Maximum - ItemsToPaint;
			if (pos > 0) {
				int usedV = ItemsToPaint * (Font.Height + RowSpace);
				fineVoffset = ListPanel.Height - usedV + RowSpace;
			} else fineVoffset = RowSpace;
			txtPt.Y = fineVoffset;
			imgPt.Y = fineVoffset;
			if (pos < 0) pos = 0;
			for (int i = pos; i < Items.Count; i++) {
				switch (Items[i].Level) {
					case ErrorLevel.LOG_INFO:
						brush = InfoBrush;
						if (Items[i].Sub) image = 6;
						else image = 1;
						break;
					case ErrorLevel.LOG_ALERT:
						brush = AlertBrush;
						if (Items[i].Sub) image = 7;
						else image = 2;
						break;
					case ErrorLevel.LOG_WARNING:
						brush = WarningBrush;
						if (Items[i].Sub) image = 8;
						else image = 3;
						break;
					case ErrorLevel.LOG_ERROR:
						brush = ErrorBrush;
						if (Items[i].Sub) image = 9;
						else image = 4;
						break;
					case ErrorLevel.LOG_EXTENDED:
						brush = new SolidBrush(Items[i].ExtendedColor);
						if (Items[i].Sub) image = 10;
						else image = 5;
						break;
				}
				icon = InnerImageList.Images[image];
				if (Items[i].Sub) {
					imgPt.X = RowSpace * 2 + icon.Width;
					txtPt.X = RowSpace * 3 + icon.Width * 2;
				} else {
					imgPt.X = RowSpace;
					txtPt.X = RowSpace * 2 + icon.Width;
				}
				e.Graphics.DrawImage(icon, imgPt);
				e.Graphics.DrawString(Items[i].Text, Font, brush, txtPt);
				txtPt.Y += Font.Height + RowSpace;
				imgPt.Y += Font.Height + RowSpace;
				itemsPainted++;
				if (itemsPainted >= ItemsToPaint) break;
			}
			IsPainting = false;
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			pevent.Graphics.Clear(StyleHelper.GetParentColor(this));
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			RectangleF rect;
			GraphicsPath penPath = StyleHelper.GetGraphicsPathSR(this.ClientRectangle, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			using (Pen pen = new Pen(InnerBorderColor, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, penPath);
			}
		}

		public int AddLog(string text, string tooltip, ErrorLevel level, Color extendedColor) {
			HyperLogItem item = new HyperLogItem(text, level, extendedColor, false);
			item.Text = text;
			item.Level = level;
			item.ExtendedColor = extendedColor;
			lock (IndexLock) {
				item.Index = Index;
				Index++;
			}
			Items.Add(item);
			if (Deferred) {
				Dirty = true;
			} else {
				VScroll.Maximum = Items.Count;
				int itemsToPaint = this.Height / (Font.Height + 2);
				VScroll.Value = Math.Max(0, VScroll.Maximum - itemsToPaint / 3);
				ListPanel.Invalidate();
			}
			Debug.WriteLine("Log add " + item.Text);
			return item.Index;
		}

		public int AddSubLog(int parent, string text, string tooltip, ErrorLevel level, Color extendedColor) {
			int i, c, j;

			j = -1;
			c = Items.Count;
			for (i = c - 1; i >= 0; i--) {
				if ((parent == Items[i].Index) || (parent == Items[i].Parent)) {
					j = i;
					break;
				}
			}
			if (j >= 0) {
				HyperLogItem item = new HyperLogItem(text, level, extendedColor, true);
				lock (IndexLock) {
					item.Index = Index;
					Index++;
				}
				item.Parent = parent;
				Items.Insert(j + 1, item);
				if (Deferred) {
					Dirty = true;
				} else {
					VScroll.Maximum = Items.Count;
					VScroll.Value++;
					ListPanel.Invalidate();
				}
				Debug.WriteLine("Log subadd " + item.Text);
				return item.Index;
			}
			return -1;
		}

		public void UpdateLog(int index, string text, string tooltip, ErrorLevel level, Color extendedColor) {
			int i, c, j, p;

			j = -1;
			c = Items.Count;
			for (i = 0; i < c; i++) {
				if (index == Items[i].Index) {
					j = i;
					break;
				}
			}
			if (j >= 0) {
				HyperLogItem item = new HyperLogItem(text, level, extendedColor, Items[j].Sub);
				item.Index = index;
				item.Parent = Items[j].Parent;
				Items[j] = item;
				if (Deferred) {
					Dirty = true;
				} else {
					ListPanel.Invalidate();
				}
				Debug.WriteLine("Log upd " + item.Text);
			}
		}

		public void ClearLog() {
			Items.Clear();
			VScroll.Value = 0;
			VScroll.Maximum = 1;
			ListPanel.Invalidate();
			VScroll.Visible = false;
			VScroll.Invalidate();
		}

		public string CopyToClipboard() {
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < Items.Count; i++) {
				if (Items[i].Sub) sb.Append(" \\- ");
				switch (Items[i].Level) {
					case ErrorLevel.LOG_INFO: sb.Append("[I] "); break;
					case ErrorLevel.LOG_ALERT: sb.Append("[A] "); break;
					case ErrorLevel.LOG_WARNING: sb.Append("[W] "); break;
					case ErrorLevel.LOG_ERROR: sb.Append("[E] "); break;
				}
				sb.Append(Items[i].Text + "\n");
			}
			return sb.ToString();
		}
	}


	public class HyperTextBox : TextBox, IVisualParameters {

		[DllImport("user32.dll")]
		static extern bool HideCaret(IntPtr hWnd);
		public void HideCaret() {
			HideCaret(this.Handle);
		}

		private System.Windows.Forms.Timer UiTimer;
		private bool Dirty;
		private bool Deferred;
		private bool Defer;
		private HyperTextItem LatestItem;
		private Font InnerFont;
		private ImageList? InnerImageList;
		public SolidBrush InfoBrush, AlertBrush, WarningBrush, ErrorBrush, ExtendedBrush;
		private ShapeSR InnerBorderShape;
		private int InnerBorderRadius;
		private Color InnerBorderColor;
		private float InnerBorderWidth;
		private float InnerBorderMargin;

		public class HyperTextItem {
			public string Text;
			public ErrorLevel Level;
			public Color ExtendedColor;

			public HyperTextItem(string text, ErrorLevel level, Color extendedColor) {
				Text = text;
				Level = level;
				ExtendedColor = extendedColor;
			}
		}

		public HyperTextBox() {
			InfoBrush = new SolidBrush(Color.Green);
			AlertBrush = new SolidBrush(Color.DarkCyan);
			WarningBrush = new SolidBrush(Color.DarkOrange);
			ErrorBrush = new SolidBrush(Color.Red);
			ExtendedBrush = new SolidBrush(Color.Blue);
			UiTimer = new System.Windows.Forms.Timer();
			UiTimer.Tick += UiTimer_Tick;
			UiTimer.Interval = 250;
			Dirty = false;
			Defer = false;
			SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
			this.Multiline = true;
			InnerFont = new Font("Segoe UI", 9F);
			this.Height = InnerFont.Height + 2;
			InnerImageList = null;
			InnerBorderShape = ShapeSR.ROUNDED;
			InnerBorderRadius = 5;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 1.0f;
			LatestItem = new HyperTextItem("", ErrorLevel.LOG_INFO, ForeColor);
			BorderStyle = BorderStyle.None;
			ReadOnly = true;
			//HideCaret();
		}
		//[Browsable(true)]
		public Font Font {
			get { return InnerFont; }
			set {
				InnerFont = value;
				this.Height = InnerFont.Height + 2 + ((InnerBorderShape == ShapeSR.ROUNDED) ? (int)(InnerBorderWidth * 2) : 0);
				this.Invalidate();
			}
		}
		public ImageList ImageList {
			get { return InnerImageList; }
			set { InnerImageList = value; }
		}

		public ShapeSR BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Height = InnerFont.Height + 2 + ((InnerBorderShape == ShapeSR.ROUNDED) ? (int)(InnerBorderWidth * 2) : 0);
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				this.Height = InnerFont.Height + 2 + ((InnerBorderShape == ShapeSR.ROUNDED) ? (int)(InnerBorderWidth * 2) : 0);
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Height = InnerFont.Height + 2 + ((InnerBorderShape == ShapeSR.ROUNDED) ? (int)(InnerBorderWidth * 2) : 0);
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Height = InnerFont.Height + 2 + ((InnerBorderShape == ShapeSR.ROUNDED) ? (int)(InnerBorderWidth * 2) : 0);
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Height = InnerFont.Height + 2 + ((InnerBorderShape == ShapeSR.ROUNDED) ? (int)(InnerBorderWidth * 2) : 0);
				this.Invalidate();
			}
		}

		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}

		protected override Size DefaultSize {
			get { return new Size(200, 24); }
		}

		public override string Text {
			get { return LatestItem.Text; }
			set {
				LatestItem = new HyperTextItem(value, ErrorLevel.LOG_INFO, Color.Black);
				if (Deferred) {
					Defer = true;
					Dirty = true;
				} else {
					this.Invalidate();
				}
			}
		}

		public HyperTextItem Item {
			get { return LatestItem; }
			set {
				LatestItem = value;
				if (Deferred) {
					Defer = true;
					Dirty = true;
				} else {
					this.Invalidate();
				}
			}
		}

		public bool DeferredUpdate {
			set {
				Deferred = value;
				if (value) {
					UiTimer.Enabled = true;
					UiTimer.Start();
				} else {
					UiTimer.Stop();
					UiTimer.Enabled = false;
				}
			}
		}

		private void UiTimer_Tick(object sender, EventArgs e) {
			//if (Defer) {
			//	Defer = false;
			// hold off on update;
			//} else {
			if (Dirty) {
				//base.Text = LatestItem.Text;
				this.Invalidate();
				Dirty = false;
			}
			//}
		}

		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}
		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}

		// shift focus back to parent to hide caret
		protected override void OnGotFocus(EventArgs e) {
			base.OnGotFocus(e);
			Parent.Focus();
			HideCaret();
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			pevent.Graphics.Clear(StyleHelper.GetParentColor(this));
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			if (LatestItem == null) return;
			Rectangle clipRect;
			// paint "transparent" background
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			// paint background
			RectangleF rect;
			rect = new RectangleF(InnerBorderWidth / 2f, InnerBorderWidth / 2f, this.Width - InnerBorderWidth, this.Height - InnerBorderWidth);
			GraphicsPath penPath = StyleHelper.GetGraphicsPathSR(this.ClientRectangle, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			// draw border
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, penPath);
			}
			// draw text
			rect.Width = rect.Width - 5;
			e.Graphics.SetClip(rect);
			Point imgPt = new Point(0, 0);
			Point txtPt = new Point(0, 0);
			Image icon;
			int image = 0;
			int padding = 0;
			if ((InnerBorderShape == ShapeSR.ROUNDED)) padding = InnerBorderRadius;
			SolidBrush brush = InfoBrush;
			if (InnerImageList != null) {
				switch (LatestItem.Level) {
					case ErrorLevel.LOG_INFO:
						brush = InfoBrush;
						image = 1;
						break;
					case ErrorLevel.LOG_ALERT:
						brush = AlertBrush;
						image = 2;
						break;
					case ErrorLevel.LOG_WARNING:
						brush = WarningBrush;
						image = 3;
						break;
					case ErrorLevel.LOG_ERROR:
						brush = ErrorBrush;
						image = 4;
						break;
					case ErrorLevel.LOG_EXTENDED:
						brush = new SolidBrush(LatestItem.ExtendedColor);
						image = 5;
						break;
				}
				icon = InnerImageList.Images[image];
				txtPt.X = icon.Width + 2 + padding;
				imgPt.X = padding;
				imgPt.Y = (this.Height - icon.Height) / 2;
				e.Graphics.DrawImage(icon, imgPt);
			} else {
				txtPt.X = padding;
			}
			e.Graphics.DrawString(LatestItem.Text, Font, brush, txtPt);
		}
	}

	[ToolboxItem(false)]
	public class RoundedSliderBase : PictureBox, IVisualParameters {
		protected int InnerBorderRadius;
		private Color InnerBorderColor;
		protected float InnerBorderWidth;
		protected float InnerBorderMargin;
		protected float InnerMinimum;
		protected float InnerMaximum;
		private float InnerValue;
		private float InnerStep;
		private Color InnerTrackColor;
		private Color InnerThumbColor;
		private ShapeSRE InnerThumbShape;
		private ShapeSRE InnerBorderShape;
		private bool HoveringThumb;
		private bool ClickedThumb;
		protected Point MouseDownPoint;
		protected Point MouseDelta;
		protected float MouseDownValue;
		protected float TravelToValue;
		protected float ValueToTravel;
		protected RectangleF TravelRect;
		protected RectangleF ThumbRect;
		protected const int ThumbBorder = 1;
		private EventHandler onValueChanged;
		private EventHandler onScroll;
		protected float DrawnValue;
		private Orientation InnerSliderOrientation;
		private System.Timers.Timer SmoothTimer;
		const int SMOOTH_SCROLL_PERIOD = 17;
		const int SMOOTH_SCROLL_STEPS = 6;
		private float SmoothScrollDelta;

		public RoundedSliderBase() {
			SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
			BackColor = Color.Linen;
			InnerThumbColor = Color.Olive;
			InnerThumbShape = ShapeSRE.ROUNDED;
			InnerBorderShape = ShapeSRE.ROUNDED;
			InnerBorderRadius = 3;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 1.0f;
			InnerMinimum = 0;
			InnerMaximum = 100;
			InnerStep = 1;
			InnerValue = 0;
			DrawnValue = 0;
			TravelRect = new RectangleF();
			ThumbRect = new RectangleF();
			SmoothTimer = new System.Timers.Timer();
			SmoothTimer.Interval = SMOOTH_SCROLL_PERIOD;
			SmoothTimer.Elapsed += SmoothTimer_Tick;
			ComputeRects();
			ComputeThumb();
		}

		//Properties
		[Browsable(true)]
		/*public Orientation SliderOrientation {
			get { return InnerSliderOrientation; }
			set { InnerSliderOrientation = value; }
		}*/
		public decimal Minimum {
			get { return (decimal)InnerMinimum; }
			set {
				if (InnerMinimum != (float)value) {
					InnerMinimum = (float)value;
					FitValueWithinMinMax();
					ComputeThumb();
					this.Invalidate();
				}
			}
		}
		public decimal Maximum {
			get { return (decimal)InnerMaximum; }
			set {
				if (InnerMaximum != (float)value) {
					InnerMaximum = (float)value;
					FitValueWithinMinMax();
					ComputeThumb();
					this.Invalidate();
				}
			}
		}
		public decimal Value {
			get { return (decimal)InnerValue; }
			set {
				float lastValue = InnerValue;
				InnerValue = (float)value;
				FitValueWithinMinMax();
				if (InnerValue != lastValue) {
					//DrawnValue = InnerValue;
					//ComputeThumb();
					OnValueChanged(new EventArgs());
					//this.Invalidate();
					SmoothScrollTo(InnerValue);
				}
			}
		}
		public decimal Step {
			get { return (decimal)InnerStep; }
			set {
				InnerStep = (float)value;
				if (InnerStep <= 0) InnerStep = 1;
			}
		}
		public ShapeSRE BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public ShapeSRE ThumbShape {
			get { return InnerThumbShape; }
			set {
				InnerThumbShape = value;
				this.Invalidate();
			}
		}
		public Color ThumbColor {
			get { return InnerThumbColor; }
			set {
				InnerThumbColor = value;
				this.Invalidate();
			}
		}
		public override Color ForeColor {
			get { return InnerThumbColor; }
			set {
				InnerThumbColor = value;
				this.Invalidate();
			}
		}
		public Color TrackColor {
			get { return InnerTrackColor; }
			set {
				InnerTrackColor = value;
				this.Invalidate();
			}
		}
		public event EventHandler ValueChanged {
			add { onValueChanged += value; }
			remove { onValueChanged -= value; }
		}

		public event EventHandler Scroll {
			add { onScroll += value; }
			remove { onScroll -= value; }
		}

		[Browsable(false)]

		public bool IsScrolling {
			get { return ClickedThumb; }
		}

		public decimal ScrollValue {
			get { return (decimal)SnapToStep(DrawnValue); }
		}
		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}
		protected virtual void OnValueChanged(EventArgs e) {
			onValueChanged?.Invoke(this, e);
		}
		protected virtual void OnScroll(EventArgs e) {
			onScroll?.Invoke(this, e);
		}

		protected override void OnMouseEnter(EventArgs e) {
			//base.OnMouseEnter(e);
			Point location = PointToClient(Cursor.Position);
			if (StyleHelper.HitTest(ThumbRect, location)) {
				HoveringThumb = true;
			}
			this.Invalidate();
		}

		protected override void OnMouseLeave(EventArgs e) {
			//base.OnMouseLeave(e);
			if (HoveringThumb) {
				HoveringThumb = false;
				this.Invalidate();
			}
		}

		protected override void OnMouseDown(MouseEventArgs e) {
			//base.OnMouseDown(e);
			if (StyleHelper.HitTest(ThumbRect, e.Location)) {
				ClickedThumb = true;
				MouseDownPoint = e.Location;
				MouseDownValue = InnerValue;
				this.Invalidate();
			} else {
				if (e.Location.X > ThumbRect.Right) {
					ChangeValueBy(InnerStep, e);
				} else if (e.Location.X < ThumbRect.Left) {
					ChangeValueBy(-InnerStep, e);
				}
			}
		}

		protected override void OnMouseUp(MouseEventArgs e) {
			//base.OnMouseUp(e);
			if (ClickedThumb) {
				ClickedThumb = false;
				if (InnerValue != SnapToStep(DrawnValue)) {
					InnerValue = SnapToStep(DrawnValue);
					DrawnValue = InnerValue;
					OnValueChanged(e);
				}
				ComputeThumb();
				this.Invalidate();
			}
		}

		protected override void OnMouseMove(MouseEventArgs e) {
			//base.OnMouseMove(e);
			float lastValue;
			if (ClickedThumb) {
				lastValue = DrawnValue;
				MouseDelta.X = e.Location.X - MouseDownPoint.X;
				MouseDelta.Y = e.Location.Y - MouseDownPoint.Y;
				//DrawnValue = MouseDownValue + (float)MouseDelta.X * TravelToValue;
				DrawnValue = GetDrawnValue();
				if (DrawnValue < InnerMinimum) DrawnValue = InnerMinimum;
				else if (DrawnValue > InnerMaximum) DrawnValue = InnerMaximum;
				//InnerValue = SnapToStep(DrawnValue);
				ComputeThumb();
				if (DrawnValue != lastValue) {
					//OnValueChanged(e);
					OnScroll(e);
					this.Invalidate();
				}
			} else {
				Point location = PointToClient(Cursor.Position);
				if (StyleHelper.HitTest(ThumbRect, location)) {
					if (!HoveringThumb) {
						HoveringThumb = true;
						this.Invalidate();
					}
				} else if (HoveringThumb) {
					HoveringThumb = false;
					this.Invalidate();
				}
			}
		}

		protected override void OnMouseWheel(MouseEventArgs e) {
			base.OnMouseWheel(e);
			if ((e.Delta / 120) >= 1) {
				ChangeValueBy(InnerStep, e);
			} else if ((e.Delta / 120) <= -1) {
				ChangeValueBy(-InnerStep, e);
			}
		}

		public void FitValueWithinMinMax() {
			if (InnerValue < InnerMinimum) InnerValue = InnerMinimum;
			else if (InnerValue > InnerMaximum) InnerValue = InnerMaximum;
			if (DrawnValue < InnerMinimum) DrawnValue = InnerMinimum;
			else if (DrawnValue > InnerMaximum) DrawnValue = InnerMaximum;
		}

		public virtual float GetDrawnValue() {
			return MouseDownValue + (float)MouseDelta.X * TravelToValue;
		}

		private float SnapToStep(float drawnValue) {
			float tentativeValue = drawnValue - InnerMinimum;
			tentativeValue = tentativeValue / (float)InnerStep;
			tentativeValue = (float)Math.Round(tentativeValue, 0) * InnerStep;
			return tentativeValue + InnerMinimum;
		}

		protected override void OnResize(EventArgs eventargs) {
			//base.OnResize(eventargs);
			ComputeRects();
			ComputeThumb();
		}

		private void ChangeValueBy(float diff, EventArgs e) {
			float lastValue = InnerValue;
			InnerValue += diff;
			FitValueWithinMinMax();
			if (InnerValue != lastValue) {
				//DrawnValue = InnerValue;
				OnValueChanged(e);
				//ComputeThumb();
				//this.Invalidate();
				SmoothScrollTo(InnerValue);
			}
		}

		public virtual void ComputeThumb() {
			ThumbRect.Y = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			ThumbRect.Height = this.Height - (InnerBorderMargin + InnerBorderWidth + ThumbBorder) * 2;
			ThumbRect.X = InnerBorderMargin + InnerBorderWidth + ThumbBorder + (TravelRect.Width - ThumbRect.Width) * (DrawnValue - (float)InnerMinimum) / (float)InnerMaximum;
			ThumbRect.Width = ThumbRect.Height;
			TravelToValue = (float)(InnerMaximum - InnerMinimum) / (TravelRect.Width - ThumbRect.Width - ThumbBorder * 2);
			if (TravelToValue == float.PositiveInfinity) ValueToTravel = 0f;
			else ValueToTravel = 1f / TravelToValue;
		}

		public virtual void ComputeRects() {
			TravelRect.X = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			TravelRect.Y = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			TravelRect.Height = this.Height;
			TravelRect.Width = this.Width - (InnerBorderMargin + InnerBorderWidth + ThumbBorder) * 2;
		}

		private void SmoothTimer_Tick(object sender, EventArgs e) {
			DrawnValue += SmoothScrollDelta;
			if (SmoothScrollDelta > 0) {
				if (DrawnValue >= InnerValue) {
					SmoothTimer.Stop();
					DrawnValue = InnerValue;
				}
			} else {
				if (DrawnValue <= InnerValue) {
					SmoothTimer.Stop();
					DrawnValue = InnerValue;
				}
			}
			ComputeThumb();
			this.Invalidate();
		}

		private void SmoothScrollTo(float pos) {
			if (DrawnValue == pos) return;
			SmoothScrollDelta = (pos - DrawnValue) / (float)SMOOTH_SCROLL_STEPS;
			SmoothTimer.Start();
		}

		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}
		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}
		private Color ThumbColorEn {
			get {
				if (Enabled) return InnerThumbColor;
				else return StyleHelper.GetInactiveColor(InnerThumbColor, BackColor);
			}
		}
		private Color TrackColorEn {
			get {
				if (Enabled) return InnerTrackColor;
				else return StyleHelper.GetInactiveColor(InnerTrackColor, BackColor);
			}
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			pevent.Graphics.Clear(StyleHelper.GetParentColor(this));
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			GraphicsPath graphPath;
			Color fillColor;
			Color foreColor;
			// draw "transparent" background
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			// draw control background;
			graphPath = StyleHelper.GetGraphicsPathSRE(this.ClientRectangle, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			e.Graphics.SetClip(graphPath);
			e.Graphics.Clear(BackColor);
			// draw border
			e.Graphics.ResetClip();
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, graphPath);
			}
			// draw track
			float trackWidth = Math.Max(1, ThumbRect.Height / 10);
			float mid = (float)(Math.Min(ClientRectangle.Width, ClientRectangle.Height)) / 2;
			using (Pen pen = new Pen(TrackColorEn, trackWidth)) {
				pen.Alignment = PenAlignment.Center;
				e.Graphics.DrawLine(pen, mid, mid, this.Width - mid, this.Height - mid);
			}
			// draw thumb
			graphPath = StyleHelper.GetGraphicsPathSRE(ThumbRect, InnerThumbShape, InnerBorderRadius, InnerBorderWidth, InnerBorderMargin);
			e.Graphics.SetClip(graphPath);
			if (ClickedThumb) {
				fillColor = StyleHelper.AdjustBrightness(InnerThumbColor, 0.90f);
			} else if (HoveringThumb) {
				fillColor = StyleHelper.AdjustBrightness(InnerThumbColor, 1.10f);
			} else {
				fillColor = ThumbColorEn;
			}
			e.Graphics.Clear(fillColor);
			e.Graphics.ResetClip();
			// draw thumb outline
			using (Pen pen = new Pen(fillColor, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, graphPath);
			}
			//Debug.WriteLine(String.Format("OnPaint Value {0} Drawn {1}", InnerValue, DrawnValue));
		}
	}

	[ToolboxItem(true)]
	public class RoundedHSlider : RoundedSliderBase {
		protected override Size DefaultSize {
			get { return new Size(150, 20); }
		}

		public override void ComputeThumb() {
			ThumbRect.Y = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			ThumbRect.Height = this.Height - (InnerBorderMargin + InnerBorderWidth + ThumbBorder) * 2;
			ThumbRect.Width = ThumbRect.Height;
			TravelToValue = (float)(InnerMaximum - InnerMinimum) / (TravelRect.Width - ThumbRect.Width - ThumbBorder * 2);
			if (TravelToValue == float.PositiveInfinity) ValueToTravel = 0f;
			else ValueToTravel = 1f / TravelToValue;
			ThumbRect.X = InnerBorderMargin + InnerBorderWidth + ThumbBorder + (DrawnValue - (float)InnerMinimum) * ValueToTravel;
		}

		public override void ComputeRects() {
			TravelRect.X = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			TravelRect.Y = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			TravelRect.Height = this.Height;
			TravelRect.Width = this.Width - (InnerBorderMargin + InnerBorderWidth + ThumbBorder) * 2;
		}

		public override float GetDrawnValue() {
			return MouseDownValue + (float)MouseDelta.X * TravelToValue;
		}
	}

	public class RoundedVSlider : RoundedSliderBase {
		protected override Size DefaultSize {
			get { return new Size(20, 150); }
		}
		public override void ComputeThumb() {
			ThumbRect.X = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			ThumbRect.Width = this.Width - (InnerBorderMargin + InnerBorderWidth + ThumbBorder) * 2;
			ThumbRect.Height = ThumbRect.Width;
			TravelToValue = (float)(InnerMaximum - InnerMinimum) / (TravelRect.Height - ThumbRect.Height - ThumbBorder * 2);
			if (TravelToValue == float.PositiveInfinity) ValueToTravel = 0f;
			else ValueToTravel = 1f / TravelToValue;
			ThumbRect.Y = InnerBorderMargin + InnerBorderWidth + ThumbBorder + (DrawnValue - (float)InnerMinimum) * ValueToTravel;
		}

		public override void ComputeRects() {
			TravelRect.Y = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			TravelRect.X = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			TravelRect.Width = this.Width;
			TravelRect.Height = this.Height - (InnerBorderMargin + InnerBorderWidth + ThumbBorder) * 2;
		}

		public override float GetDrawnValue() {
			return MouseDownValue + (float)MouseDelta.Y * TravelToValue;
		}
	}

	public class RoundedButton : Button, IVisualParameters {
		private ShapeSR InnerBorderShape;
		private int InnerBorderRadius;
		private Color InnerBorderColor;
		private float InnerBorderWidth;
		private float InnerBorderMargin;
		private bool Hovering;
		private bool Clicked;

		public RoundedButton() {
			this.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			//this.FlatAppearance.BorderColor = Color.Black;
			this.FlatAppearance.BorderSize = 0;
			this.Width = 50;
			this.Height = 24;
			InnerBorderShape = ShapeSR.ROUNDED;
			InnerBorderRadius = 5;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 2.5f;
			Hovering = false;
			Clicked = false;
		}

		//Properties
		[Browsable(true)]
		public ShapeSR BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Invalidate();
			}
		}
		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}
		protected override Size DefaultSize {
			get { return new Size(50, 20); }
		}

		protected override void OnMouseEnter(EventArgs e) {
			Hovering = true;
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e) {
			Hovering = false;
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs mevent) {
			Clicked = true;
			base.OnMouseDown(mevent);
		}

		protected override void OnMouseUp(MouseEventArgs mevent) {
			Clicked = false;
			base.OnMouseUp(mevent);
		}

		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}
		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			e.Graphics.Clear(StyleHelper.GetParentColor(this));
			RectangleF rect = new RectangleF(0, 0, this.Width, this.Height);
			GraphicsPath graphPath = StyleHelper.GetGraphicsPathSR(rect, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			e.Graphics.SetClip(graphPath);
			Color fillColor;
			if (Clicked) {
				fillColor = StyleHelper.AdjustBrightness(BackColor, 0.90f);
			} else if (Hovering) {
				fillColor = StyleHelper.AdjustBrightness(BackColor, 1.10f);
			} else {
				fillColor = BackColor;
			}
			e.Graphics.Clear(fillColor);
			e.Graphics.ResetClip();
			if (Image != null) {
				Rectangle srcRect = new Rectangle(0, 0, Image.Width, Image.Height);
				Rectangle dstRect = new Rectangle((this.Width - Image.Width) / 2, (this.Height - Image.Height) / 2, Image.Width, Image.Height);
				e.Graphics.DrawImage(Image, dstRect, srcRect, GraphicsUnit.Pixel);
			} else {
				//Size textSize = TextRenderer.MeasureText(this.Text, this.Font);
				SizeF textSize = e.Graphics.MeasureString(this.Text, this.Font);
				PointF txtPt = new PointF((this.Width - textSize.Width) / 2, (this.Height - Font.Height) / 2);
				e.Graphics.DrawString(Text, Font, new SolidBrush(ForeColorEn), txtPt);
			}
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, graphPath);
			}
		}
	}

	public class RoundedGroupBox : GroupBox, IVisualParameters {
		private ShapeSR InnerBorderShape;
		private int InnerBorderRadius;
		private Color InnerBorderColor;
		private float InnerBorderWidth;
		private float InnerBorderMargin;

		public RoundedGroupBox() {
			this.Width = 100;
			this.Height = 50;
			InnerBorderShape = ShapeSR.ROUNDED;
			InnerBorderRadius = 5;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 2.5f;
		}

		//Properties
		[Browsable(true)]
		public ShapeSR BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Invalidate();
			}
		}
		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}
		protected override Size DefaultSize {
			get { return new Size(100, 100); }
		}

		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}
		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			//pevent.Graphics.Clear(StyleHelper.GetParentColor(this));
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			Color parentColor = StyleHelper.GetParentColor(this);
			e.Graphics.Clear(parentColor);
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			int yOffset = Font.Height / 2;
			RectangleF rect = new RectangleF(0, yOffset, this.Width, this.Height - yOffset);
			GraphicsPath graphPath = StyleHelper.GetGraphicsPathSR(rect, InnerBorderShape, InnerBorderRadius, InnerBorderMargin);
			//this.Region	= new Region(GraphPath);
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, graphPath);
			}
			SizeF textSize = e.Graphics.MeasureString(this.Text, this.Font);
			//Size textSize = TextRenderer.MeasureText(this.Text, this.Font);
			RectangleF textRect = new RectangleF(12, 0, textSize.Width, textSize.Height);
			e.Graphics.FillRectangle(new SolidBrush(parentColor), textRect);
			e.Graphics.DrawString(this.Text, this.Font, new SolidBrush(ForeColorEn), new Point(14, 0));
		}
	}

	[ToolboxItem(false)]
	public class RoundedScrollBarBase : PictureBox, IVisualParameters {

		protected int InnerBorderRadius;
		private Color InnerBorderColor;
		protected float InnerBorderWidth;
		protected float InnerBorderMargin;
		protected int InnerMinimum;
		protected int InnerMaximum;
		protected int InnerValue;
		private ShapeSRE InnerThumbShape;
		private ShapeSRE InnerBorderShape;
		private Color InnerThumbColor;
		private bool HoveringThumb;
		private bool ClickedThumb;
		private bool Scrolling;
		private bool HoveringUpButton;
		private bool ClickedUpButton;
		private bool HoveringDnButton;
		private bool ClickedDnButton;
		private bool ClickedPageUp;
		private bool ClickedPageDn;
		private Point MouseDownPoint;
		protected Point MouseDelta;
		protected int MouseDownValue;
		protected float TravelToValue;
		protected float ValueToTravel;
		protected RectangleF TravelRect;
		protected RectangleF ThumbRect;
		protected const int ThumbBorder = 1;
		protected bool InnerButtons;
		protected RectangleF UpButtonRect;
		protected RectangleF DnButtonRect;
		protected PointF[] UpButtonPolygon;
		protected PointF[] DnButtonPolygon;
		private EventHandler onValueChanged;
		private EventHandler onScroll;
		protected int InnerViewportSize;
		private System.Timers.Timer RepeatTimer;
		private const int ButtonSlowInterval = 500;
		private const int ButtonFastInterval = 50;
		protected float DrawnValue;
		private System.Timers.Timer SmoothTimer;
		const int SMOOTH_SCROLL_PERIOD = 17;
		const int SMOOTH_SCROLL_STEPS = 6;
		private float SmoothScrollDelta;

		public RoundedScrollBarBase() {
			SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
			//this.Width = 20;
			//this.Height = 100;
			BackColor = Color.Linen;
			InnerThumbColor = Color.Olive;
			InnerButtons = true;
			InnerThumbShape = ShapeSRE.ROUNDED;
			InnerBorderShape = ShapeSRE.ROUNDED;
			InnerBorderRadius = 3;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 1.0f;
			InnerViewportSize = 1;
			InnerMinimum = 0;
			InnerMaximum = 100;
			InnerValue = 0;
			DrawnValue = 0;
			TravelRect = new RectangleF();
			ThumbRect = new RectangleF();
			UpButtonRect = new RectangleF();
			DnButtonRect = new RectangleF();
			UpButtonPolygon = new PointF[3];
			DnButtonPolygon = new PointF[3];
			RepeatTimer = new System.Timers.Timer();
			RepeatTimer.Elapsed += RepeatTimer_Tick;
			SmoothTimer = new System.Timers.Timer();
			SmoothTimer.Interval = SMOOTH_SCROLL_PERIOD;
			SmoothTimer.Elapsed += SmoothTimer_Tick;
			ComputeRects();
			ComputeThumb();
		}

		//Properties
		[Browsable(true)]
		public int Minimum {
			get { return InnerMinimum; }
			set {
				if (InnerMinimum != value) {
					InnerMinimum = value;
					FitValueWithinMinMax();
					ComputeThumb();
					this.Invalidate();
				}
			}
		}
		public int Maximum {
			get { return InnerMaximum; }
			set {
				if (InnerMaximum != value) {
					InnerMaximum = value;
					FitValueWithinMinMax();
					ComputeThumb();
					this.Invalidate();
				}
			}
		}
		public int Value {
			get { return InnerValue; }
			set {
				if (Scrolling) return;  // mouse scrolling override
				int lastValue = InnerValue;
				InnerValue = value;
				FitValueWithinMinMax();
				if (InnerValue != lastValue) {
					//DrawnValue = InnerValue;
					//ComputeThumb();
					OnValueChanged(new EventArgs());
					//this.Invalidate();
					SmoothScrollTo(InnerValue);
				}
			}
		}
		public int ViewportSize {
			get { return InnerViewportSize; }
			set {
				if (InnerViewportSize != value) {
					InnerViewportSize = value;
					if (InnerViewportSize < 1) InnerViewportSize = 1;
					if (InnerValue > ModifiedMax) InnerValue = ModifiedMax;
					if (DrawnValue > ModifiedMax) DrawnValue = ModifiedMax;
					ComputeThumb();
					this.Invalidate();
				}
			}
		}
		public ShapeSRE BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public ShapeSRE ThumbShape {
			get { return InnerThumbShape; }
			set {
				InnerThumbShape = value;
				this.Invalidate();
			}
		}
		public Color ThumbColor {
			get { return InnerThumbColor; }
			set {
				InnerThumbColor = value;
				this.Invalidate();
			}
		}
		public override Color ForeColor {
			get { return InnerThumbColor; }
			set {
				InnerThumbColor = value;
				this.Invalidate();
			}
		}

		public bool Buttons {
			get { return InnerButtons; }
			set {
				InnerButtons = value;
				ComputeRects();
				ComputeThumb();
				this.Invalidate();
			}
		}

		public event EventHandler ValueChanged {
			add { onValueChanged += value; }
			remove { onValueChanged -= value; }
		}

		public event EventHandler Scroll {
			add { onScroll += value; }
			remove { onScroll -= value; }
		}
		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}
		public int ModifiedMax {
			get {
				int mod = InnerMaximum - InnerViewportSize;
				if (mod < 0) mod = 0;
				return mod;
			}
		}

		private void RepeatTimer_Tick(object sender, EventArgs e) {
			if (ClickedUpButton) {
				ChangeValueBy(-1, e);
			} else if (ClickedDnButton) {
				ChangeValueBy(1, e);
			} else if (ClickedPageUp) {
				ChangeValueBy(-InnerViewportSize, e);
			} else if (ClickedPageDn) {
				ChangeValueBy(InnerViewportSize, e);
			}
			if (RepeatTimer.Interval == ButtonSlowInterval) {
				RepeatTimer.Interval = ButtonFastInterval;
				RepeatTimer.Stop();
				RepeatTimer.Start();
			}
		}

		private void SmoothTimer_Tick(object sender, EventArgs e) {
			DrawnValue += SmoothScrollDelta;
			if (SmoothScrollDelta > 0) {
				if (DrawnValue >= InnerValue) {
					SmoothTimer.Stop();
					DrawnValue = InnerValue;
				}
			} else {
				if (DrawnValue <= InnerValue) {
					SmoothTimer.Stop();
					DrawnValue = InnerValue;
				}
			}
			ComputeThumb();
			this.Invalidate();
		}

		private void SmoothScrollTo(int pos) {
			if (DrawnValue == (float)pos) return;
			SmoothScrollDelta = ((float)pos - DrawnValue) / (float)SMOOTH_SCROLL_STEPS;
			SmoothTimer.Start();
		}

		protected virtual void OnValueChanged(EventArgs e) {
			onValueChanged?.Invoke(this, e);
		}
		protected virtual void OnScroll(EventArgs e) {
			onScroll?.Invoke(this, e);
		}

		protected override void OnMouseEnter(EventArgs e) {
			//base.OnMouseEnter(e);
			Point location = PointToClient(Cursor.Position);
			if (StyleHelper.HitTest(ThumbRect, location)) {
				HoveringThumb = true;
			} else if (InnerButtons && StyleHelper.HitTest(UpButtonRect, location)) {
				HoveringUpButton = true;
			} else if (InnerButtons && StyleHelper.HitTest(DnButtonRect, location)) {
				HoveringDnButton = true;
			}
			this.Invalidate();
		}

		protected override void OnMouseLeave(EventArgs e) {
			//base.OnMouseLeave(e);
			if (HoveringThumb || HoveringUpButton || HoveringDnButton) {
				HoveringThumb = false;
				HoveringUpButton = false;
				HoveringDnButton = false;
				this.Invalidate();
			}
		}

		protected override void OnMouseDown(MouseEventArgs e) {
			//base.OnMouseDown(e);
			if (StyleHelper.HitTest(ThumbRect, e.Location)) {
				ClickedThumb = true;
				MouseDownPoint = e.Location;
				MouseDownValue = InnerValue;
				Scrolling = true;
				this.Invalidate();
			} else if (InnerButtons && StyleHelper.HitTest(UpButtonRect, e.Location)) {
				ClickedUpButton = true;
				ChangeValueBy(-1, e);
				RepeatTimer.Interval = ButtonSlowInterval;
				RepeatTimer.Start();
			} else if (InnerButtons && StyleHelper.HitTest(DnButtonRect, e.Location)) {
				ClickedDnButton = true;
				ChangeValueBy(1, e);
				RepeatTimer.Interval = ButtonSlowInterval;
				RepeatTimer.Start();
			} else {
				if (e.Location.Y > ThumbRect.Bottom) {
					ClickedPageDn = true;
					ChangeValueBy(InnerViewportSize, e);
					RepeatTimer.Interval = ButtonSlowInterval;
					RepeatTimer.Start();
				} else if (e.Location.Y < ThumbRect.Top) {
					ClickedPageUp = true;
					ChangeValueBy(-InnerViewportSize, e);
					RepeatTimer.Interval = ButtonSlowInterval;
					RepeatTimer.Start();
				}
			}
		}

		protected override void OnMouseUp(MouseEventArgs e) {
			//base.OnMouseUp(e);
			Scrolling = false;
			if (ClickedThumb || ClickedUpButton || ClickedDnButton || ClickedPageUp || ClickedPageDn) {
				ClickedThumb = false;
				ClickedUpButton = false;
				ClickedDnButton = false;
				ClickedPageUp = false;
				ClickedPageDn = false;
				RepeatTimer.Stop();
				this.Invalidate();
			}
		}

		protected override void OnMouseMove(MouseEventArgs e) {
			//base.OnMouseMove(e);
			float lastValue;
			if (Scrolling) {
				lastValue = DrawnValue;
				MouseDelta.X = e.Location.X - MouseDownPoint.X;
				MouseDelta.Y = e.Location.Y - MouseDownPoint.Y;
				//DrawnValue = MouseDownValue + (float)MouseDelta.Y * TravelToValue;
				DrawnValue = GetDrawnValue();
				if (DrawnValue < InnerMinimum) DrawnValue = InnerMinimum;
				else if (DrawnValue > ModifiedMax) DrawnValue = ModifiedMax;
				InnerValue = (int)DrawnValue;
				ComputeThumb();
				if (DrawnValue != lastValue) {
					OnValueChanged(e);
					this.Invalidate();
				}
			} else {
				Point location = PointToClient(Cursor.Position);
				if (StyleHelper.HitTest(ThumbRect, location)) {
					if (!HoveringThumb) {
						HoveringThumb = true;
						this.Invalidate();
					}
					if (HoveringUpButton || HoveringDnButton) {
						HoveringUpButton = false;
						HoveringDnButton = false;
						this.Invalidate();
					}
				} else if (InnerButtons && StyleHelper.HitTest(UpButtonRect, location)) {
					if (!HoveringUpButton) {
						HoveringUpButton = true;
						this.Invalidate();
					}
					if (HoveringThumb) {
						HoveringThumb = false;
						this.Invalidate();
					}
				} else if (InnerButtons && StyleHelper.HitTest(DnButtonRect, location)) {
					if (!HoveringDnButton) {
						HoveringDnButton = true;
						this.Invalidate();
					}
					if (HoveringThumb) {
						HoveringThumb = false;
						this.Invalidate();
					}
				} else {
					if (HoveringThumb || HoveringUpButton || HoveringDnButton) {
						HoveringThumb = false;
						HoveringUpButton = false;
						HoveringDnButton = false;
						this.Invalidate();
					}
				}
			}
		}

		public void FitValueWithinMinMax() {
			if (InnerValue < InnerMinimum) InnerValue = InnerMinimum;
			else if (InnerValue > ModifiedMax) InnerValue = ModifiedMax;
			if (DrawnValue < InnerMinimum) DrawnValue = InnerMinimum;
			else if (DrawnValue > ModifiedMax) DrawnValue = ModifiedMax;
		}

		public virtual float GetDrawnValue() {
			return MouseDownValue + (float)MouseDelta.Y * TravelToValue;
		}

		protected override void OnResize(EventArgs eventargs) {
			//base.OnResize(eventargs);
			ComputeRects();
			ComputeThumb();
		}

		private void ChangeValueBy(int diff, EventArgs e) {
			int lastValue = InnerValue;
			InnerValue += diff;
			FitValueWithinMinMax();
			if (InnerValue != lastValue) {
				//DrawnValue = InnerValue;
				OnValueChanged(e);
				//ComputeThumb();
				//this.Invalidate();
				SmoothScrollTo(InnerValue);
			}
		}

		public virtual void ComputeThumb() {
			ThumbRect.X = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			ThumbRect.Width = this.Width - (InnerBorderMargin + InnerBorderWidth + ThumbBorder) * 2;
			ThumbRect.Height = (TravelRect.Height - ThumbBorder * 2) * InnerViewportSize / (InnerMaximum - InnerMinimum + 1);
			if (ThumbRect.Height < this.Width) ThumbRect.Height = this.Width;
			if (ThumbRect.Height > TravelRect.Height - ThumbBorder * 2) ThumbRect.Height = TravelRect.Height - ThumbBorder * 2;
			TravelToValue = (float)(ModifiedMax - InnerMinimum) / (TravelRect.Height - ThumbRect.Height - ThumbBorder * 2);
			if (TravelToValue == float.PositiveInfinity) ValueToTravel = 0f;
			else ValueToTravel = 1f / TravelToValue;
			ThumbRect.Y = UpButtonRect.Height + ThumbBorder + (DrawnValue - (float)InnerMinimum) * ValueToTravel;
		}

		public virtual void ComputeRects() {
			DnButtonRect.X = this.Width - this.Height;
			UpButtonRect.Height = this.Height;
			DnButtonRect.Height = this.Height;
			UpButtonRect.Width = InnerButtons ? this.Height : 0;
			DnButtonRect.Width = InnerButtons ? this.Height : 0;
			TravelRect.X = UpButtonRect.Width;
			TravelRect.Height = this.Height;
			TravelRect.Width = this.Width - UpButtonRect.Width - DnButtonRect.Width;
			PointF upCenter = new PointF((UpButtonRect.Left + UpButtonRect.Right - 1) / 2f, (UpButtonRect.Top + UpButtonRect.Bottom) / 2f);
			PointF dnCenter = new PointF((DnButtonRect.Left + DnButtonRect.Right - 1) / 2f, upCenter.Y);
			float iconRadius = (float)UpButtonRect.Height / 5f;
			UpButtonPolygon[0].Y = upCenter.Y;
			UpButtonPolygon[0].X = upCenter.X - iconRadius;
			UpButtonPolygon[1].Y = upCenter.Y - iconRadius;
			UpButtonPolygon[1].X = upCenter.X + iconRadius;
			UpButtonPolygon[2].Y = upCenter.Y + iconRadius;
			UpButtonPolygon[2].X = upCenter.X + iconRadius;
			DnButtonPolygon[0].Y = dnCenter.Y;
			DnButtonPolygon[0].X = dnCenter.X + iconRadius;
			DnButtonPolygon[1].Y = dnCenter.Y - iconRadius;
			DnButtonPolygon[1].X = dnCenter.X - iconRadius;
			DnButtonPolygon[2].Y = dnCenter.Y + iconRadius;
			DnButtonPolygon[2].X = dnCenter.X - iconRadius;
		}

		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}
		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}
		private Color ThumbColorEn {
			get {
				if (Enabled) return InnerThumbColor;
				else return StyleHelper.GetInactiveColor(InnerThumbColor, BackColor);
			}
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			pevent.Graphics.Clear(StyleHelper.GetParentColor(this));
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			GraphicsPath graphPath;
			Color fillColor;
			Color foreColor;
			// draw "transparent" background
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			// draw control background;
			graphPath = StyleHelper.GetGraphicsPathSRE(this.ClientRectangle, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			// draw buttons/travel
			e.Graphics.SetClip(graphPath);
			if (InnerButtons) {
				e.Graphics.FillRectangle(new SolidBrush(BackColor), TravelRect);
				if (ClickedUpButton) {
					fillColor = StyleHelper.AdjustBrightness(InnerBorderColor, 0.90f);
					foreColor = StyleHelper.AdjustBrightness(InnerThumbColor, 0.90f);
				} else if (HoveringUpButton) {
					fillColor = StyleHelper.AdjustBrightness(InnerBorderColor, 1.10f);
					foreColor = StyleHelper.AdjustBrightness(InnerThumbColor, 1.10f);
				} else {
					fillColor = BorderColorEn;
					foreColor = ThumbColorEn;
				}
				e.Graphics.FillRectangle(new SolidBrush(fillColor), UpButtonRect);
				e.Graphics.FillPolygon(new SolidBrush(foreColor), UpButtonPolygon);
				if (ClickedDnButton) {
					fillColor = StyleHelper.AdjustBrightness(InnerBorderColor, 0.90f);
					foreColor = StyleHelper.AdjustBrightness(InnerThumbColor, 0.90f);
				} else if (HoveringDnButton) {
					fillColor = StyleHelper.AdjustBrightness(InnerBorderColor, 1.10f);
					foreColor = StyleHelper.AdjustBrightness(InnerThumbColor, 1.10f);
				} else {
					fillColor = BorderColorEn;
					foreColor = ThumbColorEn;
				}
				e.Graphics.FillRectangle(new SolidBrush(fillColor), DnButtonRect);
				e.Graphics.FillPolygon(new SolidBrush(foreColor), DnButtonPolygon);
			} else {
				e.Graphics.Clear(BackColor);
			}
			// draw thumb
			e.Graphics.ResetClip();
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, graphPath);
			}
			graphPath = StyleHelper.GetGraphicsPathSRE(ThumbRect, InnerThumbShape, InnerBorderRadius, InnerBorderWidth, InnerBorderMargin);
			e.Graphics.SetClip(graphPath);
			if (ClickedThumb) {
				fillColor = StyleHelper.AdjustBrightness(InnerThumbColor, 0.90f);
			} else if (HoveringThumb) {
				fillColor = StyleHelper.AdjustBrightness(InnerThumbColor, 1.10f);
			} else {
				fillColor = ThumbColorEn;
			}
			e.Graphics.Clear(fillColor);
			e.Graphics.ResetClip();
			// draw thumb outline
			using (Pen pen = new Pen(fillColor, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, graphPath);
			}
			//Debug.WriteLine(String.Format("OnPaint Value {0} Drawn {1}", InnerValue, DrawnValue));
		}
	}

	[ToolboxItem(true)]
	public class RoundedHScrollBar : RoundedScrollBarBase {
		protected override Size DefaultSize {
			get { return new Size(150, 20); }
		}
		public override float GetDrawnValue() {
			return MouseDownValue + (float)MouseDelta.X * TravelToValue;
		}
		public override void ComputeThumb() {
			ThumbRect.Y = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			ThumbRect.Height = this.Height - (InnerBorderMargin + InnerBorderWidth + ThumbBorder) * 2;
			ThumbRect.Width = (TravelRect.Width - ThumbBorder * 2) * InnerViewportSize / (ModifiedMax - InnerMinimum + 1);
			if (ThumbRect.Width < this.Height) ThumbRect.Width = this.Height;
			if (ThumbRect.Width > TravelRect.Width - ThumbBorder * 2) ThumbRect.Width = TravelRect.Width - ThumbBorder * 2;
			TravelToValue = (float)(ModifiedMax - InnerMinimum) / (TravelRect.Width - ThumbRect.Width - ThumbBorder * 2);
			if (TravelToValue == float.PositiveInfinity) ValueToTravel = 0f;
			else ValueToTravel = 1f / TravelToValue;
			ThumbRect.X = UpButtonRect.Width + ThumbBorder + (DrawnValue - (float)InnerMinimum) * ValueToTravel;
		}

		public override void ComputeRects() {
			DnButtonRect.X = this.Width - this.Height;
			UpButtonRect.Height = this.Height;
			DnButtonRect.Height = this.Height;
			UpButtonRect.Width = InnerButtons ? this.Height : 0;
			DnButtonRect.Width = InnerButtons ? this.Height : 0;
			TravelRect.X = UpButtonRect.Width;
			TravelRect.Height = this.Height;
			TravelRect.Width = this.Width - UpButtonRect.Width - DnButtonRect.Width;
			PointF upCenter = new PointF((UpButtonRect.Left + UpButtonRect.Right - 1) / 2f, (UpButtonRect.Top + UpButtonRect.Bottom) / 2f);
			PointF dnCenter = new PointF((DnButtonRect.Left + DnButtonRect.Right - 1) / 2f, upCenter.Y);
			float iconRadius = (float)UpButtonRect.Height / 5f;
			UpButtonPolygon[0].Y = upCenter.Y;
			UpButtonPolygon[0].X = upCenter.X - iconRadius;
			UpButtonPolygon[1].Y = upCenter.Y - iconRadius;
			UpButtonPolygon[1].X = upCenter.X + iconRadius;
			UpButtonPolygon[2].Y = upCenter.Y + iconRadius;
			UpButtonPolygon[2].X = upCenter.X + iconRadius;
			DnButtonPolygon[0].Y = dnCenter.Y;
			DnButtonPolygon[0].X = dnCenter.X + iconRadius;
			DnButtonPolygon[1].Y = dnCenter.Y - iconRadius;
			DnButtonPolygon[1].X = dnCenter.X - iconRadius;
			DnButtonPolygon[2].Y = dnCenter.Y + iconRadius;
			DnButtonPolygon[2].X = dnCenter.X - iconRadius;
		}

	}

	public class RoundedVScrollBar : RoundedScrollBarBase {
		protected override Size DefaultSize {
			get { return new Size(20, 150); }
		}
		public override float GetDrawnValue() {
			return MouseDownValue + (float)MouseDelta.Y * TravelToValue;
		}
		public override void ComputeThumb() {
			ThumbRect.X = InnerBorderMargin + InnerBorderWidth + ThumbBorder;
			ThumbRect.Width = this.Width - (InnerBorderMargin + InnerBorderWidth + ThumbBorder) * 2;
			ThumbRect.Height = (TravelRect.Height - ThumbBorder * 2) * InnerViewportSize / (InnerMaximum - InnerMinimum + 1);
			if (ThumbRect.Height < this.Width) ThumbRect.Height = this.Width;
			if (ThumbRect.Height > TravelRect.Height - ThumbBorder * 2) ThumbRect.Height = TravelRect.Height - ThumbBorder * 2;
			TravelToValue = (float)(ModifiedMax - InnerMinimum) / (TravelRect.Height - ThumbRect.Height - ThumbBorder * 2);
			if (TravelToValue == float.PositiveInfinity) ValueToTravel = 0f;
			else ValueToTravel = 1f / TravelToValue;
			ThumbRect.Y = UpButtonRect.Height + ThumbBorder + (DrawnValue - (float)InnerMinimum) * ValueToTravel;
		}

		public override void ComputeRects() {
			DnButtonRect.Y = this.Height - this.Width;
			UpButtonRect.Width = this.Width;
			DnButtonRect.Width = this.Width;
			UpButtonRect.Height = InnerButtons ? this.Width : 0;
			DnButtonRect.Height = InnerButtons ? this.Width : 0;
			TravelRect.Y = UpButtonRect.Height;
			TravelRect.Width = this.Width;
			TravelRect.Height = this.Height - UpButtonRect.Height - DnButtonRect.Height;
			PointF upCenter = new PointF((UpButtonRect.Left + UpButtonRect.Right - 1) / 2f, (UpButtonRect.Top + UpButtonRect.Bottom) / 2f);
			PointF dnCenter = new PointF(upCenter.X, (DnButtonRect.Top + DnButtonRect.Bottom) / 2f);
			float iconRadius = (float)UpButtonRect.Width / 5f;
			UpButtonPolygon[0].X = upCenter.X;
			UpButtonPolygon[0].Y = upCenter.Y - iconRadius;
			UpButtonPolygon[1].X = upCenter.X - iconRadius;
			UpButtonPolygon[1].Y = upCenter.Y + iconRadius;
			UpButtonPolygon[2].X = upCenter.X + iconRadius;
			UpButtonPolygon[2].Y = upCenter.Y + iconRadius;
			DnButtonPolygon[0].X = dnCenter.X;
			DnButtonPolygon[0].Y = dnCenter.Y + iconRadius;
			DnButtonPolygon[1].X = dnCenter.X - iconRadius;
			DnButtonPolygon[1].Y = dnCenter.Y - iconRadius;
			DnButtonPolygon[2].X = dnCenter.X + iconRadius;
			DnButtonPolygon[2].Y = dnCenter.Y - iconRadius;
		}

	}

	public class RoundedProgressBar : ProgressBar, IVisualParameters {
		private ShapeSRE InnerBorderShape;
		private int InnerBorderRadius;
		private Color InnerBorderColor;
		private float InnerBorderWidth;
		private float InnerBorderMargin;
		private int InnerMinimum;
		private int InnerMaximum;
		private int InnerValue;

		public RoundedProgressBar() {
			SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
			InnerBorderShape = ShapeSRE.ROUNDED;
			InnerBorderRadius = 5;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 1.0f;
			InnerMaximum = 100;
		}

		//Properties
		[Browsable(true)]
		public ShapeSRE BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Invalidate();
			}
		}
		public int Minimum {
			get { return InnerMinimum; }
			set {
				if (InnerMinimum != value) {
					InnerMinimum = value;
					FitValueWithinMinMax();
					this.Invalidate();
				}
			}
		}
		public int Maximum {
			get { return InnerMaximum; }
			set {
				if (InnerMaximum != value) {
					InnerMaximum = value;
					FitValueWithinMinMax();
					this.Invalidate();
				}
			}
		}
		public int Value {
			get { return InnerValue; }
			set {
				int lastValue = InnerValue;
				InnerValue = value;
				FitValueWithinMinMax();
				if (InnerValue != lastValue) {
					this.Invalidate();
				}
			}
		}
		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}
		protected override Size DefaultSize {
			get { return new Size(150, 20); }
		}
		public void FitValueWithinMinMax() {
			if (InnerValue < InnerMinimum) InnerValue = InnerMinimum;
			else if (InnerValue > InnerMaximum) InnerValue = InnerMaximum;
		}

		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}

		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			pevent.Graphics.Clear(StyleHelper.GetParentColor(this));
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			RectangleF rect;
			RectangleF rectYes, rectNo;
			float percent = (float)InnerValue / (float)(InnerMaximum - InnerMinimum);
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			rect = new RectangleF(InnerBorderWidth / 2f, InnerBorderWidth / 2f, this.Width - InnerBorderWidth / 2f - 0.5f, this.Height - InnerBorderWidth / 2f - 0.5f);
			GraphicsPath penPath = StyleHelper.GetGraphicsPathSRE(rect, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			e.Graphics.SetClip(penPath);
			rectYes = new RectangleF(0, 0, percent * ((float)Width - InnerBorderWidth * 2), Height);
			rectNo = new RectangleF(rectYes.Width, 0, (float)Width - rectYes.Width, Height);
			e.Graphics.FillRectangle(new SolidBrush(BackColor), rectNo);
			e.Graphics.FillRectangle(new SolidBrush(ForeColorEn), rectYes);
			e.Graphics.ResetClip();
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, penPath);
			}

		}
	}

	public class RoundedSpinner : PictureBox, IVisualParameters {

		// missing collection base for use in Designer
		public class ItemPair {
			public string Text;
			public decimal Value;

			public ItemPair(string text, decimal value) {
				Text = text;
				Value = value;
			}
		}

		public enum Justification { LEFT, RIGHT };
		private int InnerBorderRadius;
		private Color InnerBorderColor;
		private float InnerBorderWidth;
		private float InnerBorderMargin;
		private ShapeSR InnerBorderShape;
		private float InnerMinimum;
		private float InnerMaximum;
		private Justification InnerJustify;
		private float InnerValue;
		private float InnerStep;
		private int InnerIndex;
		private int InnerDecimalPlaces;
		private ItemPair[] InnerItemList;
		private bool HoveringUpButton;
		private bool ClickedUpButton;
		private bool HoveringDnButton;
		private bool ClickedDnButton;
		private RectangleF NumberRect;
		private RectangleF UpButtonRect;
		private RectangleF DnButtonRect;
		private PointF[] UpButtonPolygon;
		private PointF[] DnButtonPolygon;
		private EventHandler onValueChanged;
		private string InnerPrefix;
		private string InnerSuffix;
		private System.Timers.Timer RepeatTimer;
		private const int ButtonSlowInterval = 500;
		private const int ButtonFastInterval = 50;
		private System.Timers.Timer SmoothTimer;
		const int SMOOTH_SCROLL_PERIOD = 17;
		const int SMOOTH_SCROLL_STEPS = 6;
		private float SmoothScrollDelta;
		private float OldScrollY, NewScrollY, ScrollTarget;
		private string OldValueString, NewValueString;

		public RoundedSpinner() {
			SetStyle(ControlStyles.UserPaint, true);
			//this.Width = 20;
			//this.Height = 100;
			BackColor = Color.Linen;
			ForeColor = Color.Olive;
			BorderShape = ShapeSR.ROUNDED;
			InnerBorderRadius = 3;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 1.0f;
			InnerMinimum = 0;
			InnerMaximum = 100;
			InnerValue = 0;
			InnerStep = 1;
			InnerJustify = Justification.LEFT;
			//InnerDecimalPlaces = 1;
			InnerItemList = null;
			InnerPrefix = "";
			InnerSuffix = "";
			NumberRect = new RectangleF();
			UpButtonRect = new RectangleF();
			DnButtonRect = new RectangleF();
			UpButtonPolygon = new PointF[3];
			DnButtonPolygon = new PointF[3];
			RepeatTimer = new System.Timers.Timer();
			RepeatTimer.Elapsed += RepeatTimer_Tick;
			SmoothTimer = new System.Timers.Timer();
			SmoothTimer.Interval = SMOOTH_SCROLL_PERIOD;
			SmoothTimer.Elapsed += SmoothTimer_Tick;
			OldValueString = "";
			NewValueString = GetFormattedString();
			ComputeRects();
		}

		protected override void OnCreateControl() {
			base.OnCreateControl();
			SmoothScrollTo(NewValueString, true);
		}

		//Properties
		[Browsable(true)]
		public decimal Minimum {
			get { return (decimal)InnerMinimum; }
			set {
				if (InnerMinimum != (float)value) {
					InnerMinimum = (float)value;
					FitValueWithinMinMax();
					this.Invalidate();
				}
			}
		}
		public decimal Maximum {
			get { return (decimal)InnerMaximum; }
			set {
				if (InnerMaximum != (float)value) {
					InnerMaximum = (float)value;
					FitValueWithinMinMax();
					this.Invalidate();
				}
			}
		}
		public int Index {
			get { return InnerIndex; }
			set {
				if (InnerItemList == null) return;
				int lastIndex = InnerIndex;
				InnerIndex = value;
				FitValueWithinMinMax();
				if (lastIndex != InnerIndex) {
					OnValueChanged(new EventArgs());
					this.Invalidate();
				}
			}
		}
		public decimal Value {
			get {
				if (InnerItemList != null) {
					return InnerItemList[InnerIndex].Value;
				} else {
					return (decimal)InnerValue;
				}
			}
			set {
				if (InnerItemList != null) {
					int lastIndex = InnerIndex;
					for (int i = 0; i < InnerItemList.Length; i++) {
						if (InnerItemList[i].Value == value) {
							InnerIndex = i;
							break;
						}
					}
					if (InnerIndex != lastIndex) {
						OnValueChanged(new EventArgs());
						//this.Invalidate();
						SmoothScrollTo(GetFormattedString(), InnerIndex > lastIndex);

					}
				} else {
					float lastValue = InnerValue;
					InnerValue = (float)value;
					FitValueWithinMinMax();
					if (InnerValue != lastValue) {
						OnValueChanged(new EventArgs());
						//this.Invalidate();
						SmoothScrollTo(GetFormattedString(), InnerValue > lastValue);
					}
				}
			}
		}
		public decimal Step {
			get { return (decimal)InnerStep; }
			set { InnerStep = (float)value; }
		}
		public int DecimalPlaces {
			get { return InnerDecimalPlaces; }
			set {
				InnerDecimalPlaces = value;
				NewValueString = GetFormattedString();
				SmoothScrollTo(NewValueString, true);
			}
		}
		public string Prefix {
			get { return InnerPrefix; }
			set {
				InnerPrefix = value;
				NewValueString = GetFormattedString();
				SmoothScrollTo(NewValueString, true);
			}
		}
		public string Suffix {
			get { return InnerSuffix; }
			set {
				InnerSuffix = value;
				NewValueString = GetFormattedString();
				SmoothScrollTo(NewValueString, true);
			}
		}
		public Justification Justify {
			get { return InnerJustify; }
			set {
				InnerJustify = value;
				NewValueString = GetFormattedString();
				SmoothScrollTo(NewValueString, true);
			}
		}
		public ItemPair[] ItemList {
			get { return InnerItemList; }
			set {
				InnerItemList = value;
				NewValueString = GetFormattedString();
				SmoothScrollTo(NewValueString, true);
			}
		}
		public ShapeSR BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Invalidate();
			}
		}

		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}
		protected override Size DefaultSize {
			get { return new Size(80, 26); }
		}

		public event EventHandler ValueChanged {
			add { onValueChanged += value; }
			remove { onValueChanged -= value; }
		}

		private string GetFormattedString() {
			string numText = "";
			if (InnerItemList != null) {
				if (InnerIndex >= InnerItemList.Length) InnerIndex = InnerItemList.Length - 1;
				numText = InnerItemList[InnerIndex].Text;
			} else {
				string fmt = "{0}{1:F" + InnerDecimalPlaces.ToString() + "}{2}";
				numText = String.Format(fmt, InnerPrefix, InnerValue, InnerSuffix);
			}
			return numText;
		}

		private void RepeatTimer_Tick(object sender, EventArgs e) {
			if (ClickedUpButton) {
				ChangeValueBy(InnerStep, e);
			} else if (ClickedDnButton) {
				ChangeValueBy(-InnerStep, e);
			}
			if (RepeatTimer.Interval == ButtonSlowInterval) {
				RepeatTimer.Interval = ButtonFastInterval;
				RepeatTimer.Stop();
				RepeatTimer.Start();
			}
		}

		private void SmoothTimer_Tick(object sender, EventArgs e) {
			NewScrollY += SmoothScrollDelta;
			OldScrollY += SmoothScrollDelta;
			if (SmoothScrollDelta > 0) {
				if (NewScrollY >= ScrollTarget) {
					NewScrollY = ScrollTarget;
					OldValueString = NewValueString;
					SmoothTimer.Stop();
				}
			} else {
				if (NewScrollY <= ScrollTarget) {
					NewScrollY = ScrollTarget;
					OldValueString = NewValueString;
					SmoothTimer.Stop();
				}
			}
			this.Invalidate();
		}

		private void SmoothScrollTo(string newString, bool moveUp) {
			if (OldValueString == newString) return;
			NewValueString = newString;
			if (moveUp) {
				NewScrollY = this.Height;
			} else {
				NewScrollY = -this.Height;

			}
			OldScrollY = (this.Height - Font.Height) / 2;
			ScrollTarget = (this.Height - Font.Height) / 2;
			SmoothScrollDelta = (float)(ScrollTarget - NewScrollY) / (float)SMOOTH_SCROLL_STEPS;
			SmoothTimer.Start();
		}

		protected virtual void OnValueChanged(EventArgs e) {
			onValueChanged?.Invoke(this, e);
		}

		protected override void OnMouseEnter(EventArgs e) {
			//base.OnMouseEnter(e);
			Point location = PointToClient(Cursor.Position);
			if (StyleHelper.HitTest(UpButtonRect, location)) {
				HoveringUpButton = true;
			} else if (StyleHelper.HitTest(DnButtonRect, location)) {
				HoveringDnButton = true;
			}
			this.Invalidate();
		}

		protected override void OnMouseLeave(EventArgs e) {
			//base.OnMouseLeave(e);
			if (HoveringUpButton || HoveringDnButton) {
				HoveringUpButton = false;
				HoveringDnButton = false;
				this.Invalidate();
			}
		}

		protected override void OnMouseDown(MouseEventArgs e) {
			//base.OnMouseDown(e);
			if (StyleHelper.HitTest(UpButtonRect, e.Location)) {
				ClickedUpButton = true;
				if (InnerItemList != null) {
					ChangeIndexBy(1, e);
				} else {
					ChangeValueBy(InnerStep, e);
				}
				RepeatTimer.Interval = ButtonSlowInterval;
				RepeatTimer.Start();
			} else if (StyleHelper.HitTest(DnButtonRect, e.Location)) {
				ClickedDnButton = true;
				if (InnerItemList != null) {
					ChangeIndexBy(-1, e);
				} else {
					ChangeValueBy(-InnerStep, e);
				}
				RepeatTimer.Interval = ButtonSlowInterval;
				RepeatTimer.Start();
			}
		}

		protected override void OnMouseUp(MouseEventArgs e) {
			//base.OnMouseUp(e);
			if (ClickedUpButton || ClickedDnButton) {
				ClickedUpButton = false;
				ClickedDnButton = false;
				RepeatTimer.Stop();
				this.Invalidate();
			}
		}

		protected override void OnMouseMove(MouseEventArgs e) {
			//base.OnMouseMove(e);
			int lastValue;
			Point location = PointToClient(Cursor.Position);
			if (StyleHelper.HitTest(UpButtonRect, location)) {
				if (!HoveringUpButton) {
					HoveringUpButton = true;
					this.Invalidate();
				}
				if (HoveringDnButton) {
					HoveringDnButton = false;
					this.Invalidate();
				}
			} else if (StyleHelper.HitTest(DnButtonRect, location)) {
				if (!HoveringDnButton) {
					HoveringDnButton = true;
					this.Invalidate();
				}
				if (HoveringUpButton) {
					HoveringUpButton = false;
					this.Invalidate();
				}
			} else {
				if (HoveringUpButton || HoveringDnButton) {
					HoveringUpButton = false;
					HoveringDnButton = false;
					this.Invalidate();
				}
			}
		}

		protected override void OnMouseWheel(MouseEventArgs e) {
			base.OnMouseWheel(e);
			if ((e.Delta / 120) >= 1) {
				if (InnerItemList != null) {
					ChangeIndexBy(1, e);
				} else {
					ChangeValueBy(InnerStep, e);
				}
			} else if ((e.Delta / 120) <= -1) {
				if (InnerItemList != null) {
					ChangeIndexBy(-1, e);
				} else {
					ChangeValueBy(-InnerStep, e);
				}
			}
		}

		protected override void OnResize(EventArgs eventargs) {
			//base.OnResize(eventargs);
			ComputeRects();
		}

		public void FitValueWithinMinMax() {
			if (InnerValue < InnerMinimum) InnerValue = InnerMinimum;
			else if (InnerValue > InnerMaximum) InnerValue = InnerMaximum;
		}

		private void ChangeValueBy(float diff, EventArgs e) {
			float lastValue = InnerValue;
			InnerValue += diff;
			FitValueWithinMinMax();
			if (InnerValue != lastValue) {
				OnValueChanged(e);
				SmoothScrollTo(GetFormattedString(), InnerValue > lastValue);
				//this.Invalidate();
			}
		}

		private void ChangeIndexBy(int incdec, EventArgs e) {
			int lastIndex = InnerIndex;
			InnerIndex += incdec;
			if (InnerIndex < 0) InnerIndex = 0;
			else if (InnerIndex >= InnerItemList.Length) InnerIndex = InnerItemList.Length - 1;
			if (InnerIndex != lastIndex) {
				OnValueChanged(e);
				SmoothScrollTo(GetFormattedString(), InnerIndex > lastIndex);
				//this.Invalidate();
			}
		}

		private void ComputeRects() {
			//if (UpButtonPolygon == null) return;
			UpButtonRect.Width = this.Height * 3 / 4;
			DnButtonRect.Width = this.Height * 3 / 4;
			UpButtonRect.Height = this.Height / 2;
			DnButtonRect.Height = this.Height / 2;
			UpButtonRect.Y = 0;
			DnButtonRect.Y = this.Height / 2;
			UpButtonRect.X = this.Width - UpButtonRect.Width;
			DnButtonRect.X = this.Width - DnButtonRect.Width;
			NumberRect.Width = this.Width - UpButtonRect.Width;
			NumberRect.Height = this.Height;
			PointF upCenter = new PointF((UpButtonRect.Left + UpButtonRect.Right - 1) / 2f, (UpButtonRect.Top + UpButtonRect.Bottom) / 2f);
			PointF dnCenter = new PointF(upCenter.X, (DnButtonRect.Top + DnButtonRect.Bottom) / 2f);
			float iconRadius = (float)UpButtonRect.Height / 5f;
			UpButtonPolygon[0].X = upCenter.X;
			UpButtonPolygon[0].Y = upCenter.Y - iconRadius;
			UpButtonPolygon[1].X = upCenter.X - iconRadius;
			UpButtonPolygon[1].Y = upCenter.Y + iconRadius;
			UpButtonPolygon[2].X = upCenter.X + iconRadius;
			UpButtonPolygon[2].Y = upCenter.Y + iconRadius;
			DnButtonPolygon[0].X = dnCenter.X;
			DnButtonPolygon[0].Y = dnCenter.Y + iconRadius;
			DnButtonPolygon[1].X = dnCenter.X - iconRadius;
			DnButtonPolygon[1].Y = dnCenter.Y - iconRadius;
			DnButtonPolygon[2].X = dnCenter.X + iconRadius;
			DnButtonPolygon[2].Y = dnCenter.Y - iconRadius;
		}

		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}

		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			pevent.Graphics.Clear(StyleHelper.GetParentColor(this));
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			GraphicsPath graphPath;
			Color fillColor;
			Color foreColor;
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphPath = StyleHelper.GetGraphicsPathSR(this.ClientRectangle, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			// draw number
			e.Graphics.SetClip(graphPath);
			e.Graphics.FillRectangle(new SolidBrush(BackColor), NumberRect);
			if (ClickedUpButton) {
				fillColor = StyleHelper.AdjustBrightness(InnerBorderColor, 0.90f);
				foreColor = StyleHelper.AdjustBrightness(ForeColor, 0.90f);
			} else if (HoveringUpButton) {
				fillColor = StyleHelper.AdjustBrightness(InnerBorderColor, 1.10f);
				foreColor = StyleHelper.AdjustBrightness(ForeColor, 1.10f);
			} else {
				fillColor = BorderColorEn;
				foreColor = ForeColorEn;
			}
			e.Graphics.FillRectangle(new SolidBrush(fillColor), UpButtonRect);
			e.Graphics.FillPolygon(new SolidBrush(foreColor), UpButtonPolygon);
			// draw up/down buttons
			if (ClickedDnButton) {
				fillColor = StyleHelper.AdjustBrightness(InnerBorderColor, 0.90f);
				foreColor = StyleHelper.AdjustBrightness(ForeColor, 0.90f);
			} else if (HoveringDnButton) {
				fillColor = StyleHelper.AdjustBrightness(InnerBorderColor, 1.10f);
				foreColor = StyleHelper.AdjustBrightness(ForeColor, 1.10f);
			} else {
				fillColor = BorderColorEn;
				foreColor = ForeColorEn;
			}
			e.Graphics.FillRectangle(new SolidBrush(fillColor), DnButtonRect);
			e.Graphics.FillPolygon(new SolidBrush(foreColor), DnButtonPolygon);
			// draw text
			e.Graphics.SetClip(NumberRect);
			/*string numText = GetFormattedString();
			Size textSize = TextRenderer.MeasureText(numText, this.Font);
			Point txtPt = new Point();
			txtPt.X = (InnerJustify == Justification.LEFT) ?
				(int)(InnerBorderMargin + InnerBorderWidth + 2) :
				(int)(NumberRect.Width - textSize.Width - 2);
			txtPt.Y = (this.Height - Font.Height) / 2;
			e.Graphics.DrawString(numText, Font, new SolidBrush(ForeColorEn), txtPt);*/
			SizeF textSize;
			PointF txtPt = new PointF();
			//textSize = TextRenderer.MeasureText(OldValueString, this.Font);
			textSize = e.Graphics.MeasureString(OldValueString, this.Font);
			txtPt.X = (InnerJustify == Justification.LEFT) ?
				(int)(InnerBorderMargin + InnerBorderWidth + 2) :
				(int)(NumberRect.Width - textSize.Width - 2);
			txtPt.Y = OldScrollY;
			e.Graphics.DrawString(OldValueString, Font, new SolidBrush(ForeColorEn), txtPt);
			//textSize = TextRenderer.MeasureText(NewValueString, this.Font);
			textSize = e.Graphics.MeasureString(NewValueString, this.Font);
			txtPt.X = (InnerJustify == Justification.LEFT) ?
				(int)(InnerBorderMargin + InnerBorderWidth + 2) :
				(int)(NumberRect.Width - textSize.Width - 2);
			txtPt.Y = NewScrollY;
			e.Graphics.DrawString(NewValueString, Font, new SolidBrush(ForeColorEn), txtPt);
			// draw border
			e.Graphics.ResetClip();
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, graphPath);
			}
		}
	}

	public class RoundedDataGridView : DataGridView, IVisualParameters {
		private RoundedVScrollBar NewScrollBar;
		private Rectangle GridRectangle;
		private VisualParams SavedVisuals;

		public RoundedDataGridView() {
			//SetStyle(ControlStyles.UserPaint, true);
			RowHeadersVisible = false;
			MultiSelect = false;
			SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			BorderStyle = BorderStyle.None;
			CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
			this.ScrollBars = ScrollBars.None;
			NewScrollBar = new RoundedVScrollBar();
			NewScrollBar.Top = 2;
			NewScrollBar.Width = 20;
			NewScrollBar.Left = this.Width - 22;
			NewScrollBar.Height = this.Height - 4;
			NewScrollBar.BorderMargin = 1;
			SavedVisuals = new VisualParams();
		}
		//Properties
		//[Browsable(true)]
		//[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.RowHeadersVisible = false;
				this.MultiSelect = false;
				this.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
				this.BorderStyle = BorderStyle.None;
				this.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
				//this.AlternatingRowsDefaultCellStyle.BackColor = Color.GhostWhite;
				this.BackgroundColor = value.GridBG;
				this.DefaultCellStyle.BackColor = value.GridRowBG;
				this.DefaultCellStyle.ForeColor = value.GridRowFG;
				this.DefaultCellStyle.SelectionBackColor = value.GridSelBG;
				this.DefaultCellStyle.SelectionForeColor = value.GridSelFG;
				this.EnableHeadersVisualStyles = false;
				this.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
				this.ColumnHeadersDefaultCellStyle.BackColor = value.GridColBG;
				this.ColumnHeadersDefaultCellStyle.ForeColor = value.GridColFG;
				this.ColumnHeadersDefaultCellStyle.SelectionBackColor = value.GridColBG;
				this.ColumnHeadersDefaultCellStyle.SelectionForeColor = value.GridColFG;
				this.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
				this.RowTemplate.Height = 25;
				this.ColumnHeadersHeight = 25;
				NewScrollBar.BackColor = value.ControlBG;
				NewScrollBar.ThumbColor = value.ControlFG;
				SavedVisuals = value;
				this.Invalidate();
			}
		}

		protected override void OnCreateControl() {
			base.OnCreateControl();
			this.Controls.Add(NewScrollBar);
			GridRectangle = new Rectangle(0, 0, this.Width - 30, this.Height);
			BackgroundColor = Color.WhiteSmoke;
			NewScrollBar.ValueChanged += VScroll_Scrolled;
		}

		protected override void OnResize(EventArgs e) {
			base.OnResize(e);
			NewScrollBar.Left = this.Width - 22;
			NewScrollBar.Height = this.Height - 4;
			GridRectangle.Width = this.Width - 30;
			GridRectangle.Height = this.Height;
			NewScrollBar.ViewportSize = (this.Height - this.ColumnHeadersHeight) / (this.RowTemplate.Height + this.RowTemplate.DividerHeight);
			NewScrollBar.Visible = NewScrollBar.ViewportSize < this.RowCount;
		}

		protected override void OnMouseWheel(MouseEventArgs e) {
			//base.OnMouseWheel(e);
			if ((e.Delta / 120) >= 1) {
				NewScrollBar.Value--;
			} else if ((e.Delta / 120) <= -1) {
				NewScrollBar.Value++;
			}
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			e.Graphics.SetClip(GridRectangle);
		}

		protected override void OnRowsAdded(DataGridViewRowsAddedEventArgs e) {
			base.OnRowsAdded(e);
			NewScrollBar.Maximum = this.RowCount;
			NewScrollBar.ViewportSize = (this.Height - this.ColumnHeadersHeight) / (this.RowTemplate.Height + this.RowTemplate.DividerHeight);
			NewScrollBar.Visible = NewScrollBar.ViewportSize < this.RowCount;
		}

		protected override void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e) {
			base.OnRowsRemoved(e);
			NewScrollBar.Maximum = this.RowCount;
			NewScrollBar.ViewportSize = (this.Height - this.ColumnHeadersHeight) / (this.RowTemplate.Height + this.RowTemplate.DividerHeight);
			NewScrollBar.Visible = NewScrollBar.ViewportSize < this.RowCount;
		}

		protected override void PaintBackground(Graphics graphics, Rectangle clipBounds, Rectangle gridBounds) {
			base.PaintBackground(graphics, clipBounds, gridBounds);
			graphics.Clear(SavedVisuals.GridBG);
		}

		protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e) {
			base.OnCellPainting(e);
			int col = e.ColumnIndex;
			int size;
			Color chkColor;
			if ((e.RowIndex >= 0) && (Columns[col].CellType == typeof(DataGridViewCheckBoxCell))) {
				e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
				e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
				e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
				e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				size = Math.Min(e.CellBounds.Width, e.CellBounds.Height) - 4;
				RectangleF checkRect = new RectangleF(e.CellBounds.X + (e.CellBounds.Width - size) / 2.0f, e.CellBounds.Y + (e.CellBounds.Height - size) / 2.0f, size, size);
				GraphicsPath penPath = StyleHelper.GetRoundPath(checkRect, 3);
				e.Graphics.FillPath(new SolidBrush(SavedVisuals.ControlBG), penPath);
				GraphicsPath checkPath = StyleHelper.GetCheckPath(checkRect, 3);
				if ((bool)e.FormattedValue == true) chkColor = SavedVisuals.ControlFG;
				else chkColor = SavedVisuals.GridRowBG;
				using (Pen pen = new Pen(chkColor, 2f)) {
					pen.Alignment = PenAlignment.Center;
					e.Graphics.DrawPath(pen, checkPath);
				}
				e.Handled = true;
			}
		}

		private void VScroll_Scrolled(object sender, EventArgs e) {
			if (this.RowCount == 0) return;
			int row = NewScrollBar.Value;
			if (row >= this.RowCount - 1) row = this.RowCount - 1;
			// FirstDisplayedScrollingRowIndex can stall the message queue so let others go first
			Application.DoEvents();
			this.FirstDisplayedScrollingRowIndex = row;
		}


	}

	public class RoundedTextBox : Control, IVisualParameters {
		private ShapeSR InnerBorderShape;
		private int InnerBorderRadius;
		private Color InnerBorderColor;
		private float InnerBorderWidth;
		private float InnerBorderMargin;
		private TextBox EmbeddedTextBox;

		public RoundedTextBox() {
			InnerBorderShape = ShapeSR.ROUNDED;
			InnerBorderRadius = 5;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 1.0f;
			EmbeddedTextBox = new TextBox();
			int offset = (int)Math.Round((float)InnerBorderRadius * 0.4 + InnerBorderWidth);
			MinimumSize = new Size(4, EmbeddedTextBox.Font.Height + offset * 2);
		}
		//Properties
		[Browsable(true)]
		public ShapeSR BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Height = Font.Height + 2 + ((InnerBorderShape == ShapeSR.ROUNDED) ? (int)(InnerBorderWidth * 2) : 0);
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				PositionTextBox();
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Invalidate();
			}
		}

		public Font Font {
			get { return EmbeddedTextBox.Font; }
			set {
				EmbeddedTextBox.Font = value;
				int offset = (int)Math.Round((float)InnerBorderRadius * 0.4 + InnerBorderWidth);
				MinimumSize = new Size(4, EmbeddedTextBox.Font.Height + offset * 2);
			}
		}

		public override Color ForeColor {
			get { return base.BackColor; }
			set {
				base.ForeColor = value;
				EmbeddedTextBox.ForeColor = value;
			}
		}

		public override Color BackColor {
			get { return base.BackColor; }
			set {
				base.BackColor = value;
				EmbeddedTextBox.BackColor = value;
			}
		}

		public override string Text {
			get { return EmbeddedTextBox.Text; }
			set { EmbeddedTextBox.Text = value; }
		}

		public bool ReadOnly {
			get { return EmbeddedTextBox.ReadOnly; }
			set { EmbeddedTextBox.ReadOnly = value; }
		}

		public int SelectionStart {
			get { return EmbeddedTextBox.SelectionStart; }
			set { EmbeddedTextBox.SelectionStart = value; }
		}

		public int SelectionLength {
			get { return EmbeddedTextBox.SelectionLength; }
			set { EmbeddedTextBox.SelectionLength = value; }
		}

		public string SelectedText {
			get { return EmbeddedTextBox.SelectedText; }
			set { EmbeddedTextBox.SelectedText = value; }
		}

		public bool Multiline {
			get { return EmbeddedTextBox.Multiline; }
			set { EmbeddedTextBox.Multiline = value; }
		}

		public event EventHandler TextChanged {
			add {
				EmbeddedTextBox.TextChanged += value;
			}
			remove {
				EmbeddedTextBox.TextChanged -= value;
			}
		}

		public event KeyEventHandler KeyDown {
			add {
				EmbeddedTextBox.KeyDown += value;
			}
			remove {
				EmbeddedTextBox.KeyDown -= value;
			}
		}

		public event KeyEventHandler KeyUp {
			add {
				EmbeddedTextBox.KeyUp += value;
			}
			remove {
				EmbeddedTextBox.KeyUp -= value;
			}
		}

		public event KeyPressEventHandler KeyPress {
			add {
				EmbeddedTextBox.KeyPress += value;
			}
			remove {
				EmbeddedTextBox.KeyPress -= value;
			}
		}

		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}
		public void Select(int start, int length) {
			EmbeddedTextBox.Select(start, length);
		}

		public void SelectAll() {
			EmbeddedTextBox.SelectAll();
		}

		protected override void OnCreateControl() {
			base.OnCreateControl();
			Controls.Add(EmbeddedTextBox);
			EmbeddedTextBox.BorderStyle = BorderStyle.None;
		}

		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}

		private void PositionTextBox() {
			int offset = (int)Math.Round((float)InnerBorderRadius * 0.4 + InnerBorderWidth);
			EmbeddedTextBox.Left = offset;
			EmbeddedTextBox.Top = offset;
			EmbeddedTextBox.Width = this.Width - offset * 2;
			EmbeddedTextBox.Height = this.Height - offset * 2;
		}

		protected override void OnResize(EventArgs e) {
			base.OnResize(e);
			PositionTextBox();
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			pevent.Graphics.Clear(EmbeddedTextBox.BackColor);
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			GraphicsPath penPath;
			RectangleF rect;
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			penPath = StyleHelper.GetGraphicsPathSR(this.ClientRectangle, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, penPath);
			}
			e.Graphics.SetClip(penPath);
		}
	}

	public class RoundedRichTextBox : Control, IVisualParameters {
		private ShapeSR InnerBorderShape;
		private int InnerBorderRadius;
		private Color InnerBorderColor;
		private float InnerBorderWidth;
		private float InnerBorderMargin;
		private RichTextBox EmbeddedTextBox;

		public RoundedRichTextBox() {
			InnerBorderShape = ShapeSR.ROUNDED;
			InnerBorderRadius = 5;
			InnerBorderColor = Color.DarkGray;
			InnerBorderWidth = 1.5f;
			InnerBorderMargin = 1.0f;
			EmbeddedTextBox = new RichTextBox();
		}
		//Properties
		[Browsable(true)]
		public ShapeSR BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Height = Font.Height + 2 + ((InnerBorderShape == ShapeSR.ROUNDED) ? (int)(InnerBorderWidth * 2) : 0);
				this.Invalidate();
			}
		}
		public int BorderRadius {
			get { return InnerBorderRadius; }
			set {
				InnerBorderRadius = value;
				PositionTextBox();
				this.Invalidate();
			}
		}
		public Color BorderColor {
			get { return InnerBorderColor; }
			set {
				InnerBorderColor = value;
				this.Invalidate();
			}
		}
		public float BorderWidth {
			get { return InnerBorderWidth; }
			set {
				InnerBorderWidth = value;
				this.Invalidate();
			}
		}
		public float BorderMargin {
			get { return InnerBorderMargin; }
			set {
				InnerBorderMargin = value;
				this.Invalidate();
			}
		}

		public Font Font {
			get { return EmbeddedTextBox.Font; }
			set {
				EmbeddedTextBox.Font = value;
				int offset = (int)Math.Round((float)InnerBorderRadius * 0.4 + InnerBorderWidth);
				MinimumSize = new Size(MinimumSize.Width, EmbeddedTextBox.Font.Height + offset * 2);
			}
		}

		public override Color ForeColor {
			get { return base.BackColor; }
			set {
				base.ForeColor = value;
				EmbeddedTextBox.ForeColor = value;
			}
		}

		public override Color BackColor {
			get { return base.BackColor; }
			set {
				base.BackColor = value;
				EmbeddedTextBox.BackColor = value;
			}
		}

		public override string Text {
			get { return EmbeddedTextBox.Text; }
			set { EmbeddedTextBox.Text = value; }
		}

		public bool ReadOnly {
			get { return EmbeddedTextBox.ReadOnly; }
			set { EmbeddedTextBox.ReadOnly = value; }
		}

		public event EventHandler TextChanged {
			add {
				EmbeddedTextBox.TextChanged += value;
			}
			remove {
				EmbeddedTextBox.TextChanged -= value;
			}
		}

		public event KeyEventHandler KeyDown {
			add {
				EmbeddedTextBox.KeyDown  += value;
			}
			remove {
				EmbeddedTextBox.KeyDown -= value;
			}
		}

		public event KeyEventHandler KeyUp {
			add {
				EmbeddedTextBox.KeyUp += value;
			}
			remove {
				EmbeddedTextBox.KeyUp -= value;
			}
		}

		public event KeyPressEventHandler KeyPress {
			add {
				EmbeddedTextBox.KeyPress += value;
			}
			remove {
				EmbeddedTextBox.KeyPress -= value;
			}
		}

		[Browsable(false)]

		public int SelectionStart {
			get { return EmbeddedTextBox.SelectionStart; }
			set { EmbeddedTextBox.SelectionStart = value; }
		}

		public int SelectionLength {
			get { return EmbeddedTextBox.SelectionLength; }
			set { EmbeddedTextBox.SelectionLength = value; }
		}

		public string SelectedText {
			get { return EmbeddedTextBox.SelectedText; }
			set { EmbeddedTextBox.SelectedText = value; }
		}

		public Color SelectionColor {
			get { return EmbeddedTextBox.SelectionColor; }
			set { EmbeddedTextBox.SelectionColor = value; }
		}

		public void AppendText(string text) {
			EmbeddedTextBox.AppendText(text);
		}

		public void ScrollToCaret() {
			EmbeddedTextBox.ScrollToCaret();
		}

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}
		public void Select(int start, int length) {
			EmbeddedTextBox.Select(start, length);
		}

		public void SelectAll() {
			EmbeddedTextBox.SelectAll();
		}

		protected override void OnCreateControl() {
			base.OnCreateControl();
			Controls.Add(EmbeddedTextBox);
			EmbeddedTextBox.BorderStyle = BorderStyle.None;
		}

		private Color BorderColorEn {
			get {
				if (Enabled) return InnerBorderColor;
				else return StyleHelper.GetInactiveColor(InnerBorderColor, BackColor);
			}
		}

		private void PositionTextBox() {
			int offset = (int)Math.Round((float)InnerBorderRadius * 0.4 + InnerBorderWidth);
			EmbeddedTextBox.Left = offset;
			EmbeddedTextBox.Top = offset;
			EmbeddedTextBox.Width = this.Width - offset * 2;
			EmbeddedTextBox.Height = this.Height - offset * 2;
		}

		protected override void OnResize(EventArgs e) {
			base.OnResize(e);
			PositionTextBox();
		}

		protected override void OnPaintBackground(PaintEventArgs pevent) {
			base.OnPaintBackground(pevent);
			pevent.Graphics.Clear(EmbeddedTextBox.BackColor);
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			GraphicsPath penPath;
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			penPath = StyleHelper.GetGraphicsPathSR(this.ClientRectangle, InnerBorderShape, InnerBorderRadius, InnerBorderWidth);
			using (Pen pen = new Pen(BorderColorEn, InnerBorderWidth)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, penPath);
			}
			e.Graphics.SetClip(penPath);
		}
	}

	public class SlidingToggleButton : CheckBox, IVisualParameters {

		public enum TextLocation { NONE, LEFT, RIGHT };
		private Color offToggleColor = Color.Gainsboro;
		private bool InnerSolidStyle = true;
		private System.Timers.Timer SmoothTimer;
		const int SMOOTH_SCROLL_PERIOD = 17;
		const int SMOOTH_SCROLL_STEPS = 6;
		private float SmoothScrollDelta;
		//private bool InnerChecked;
		private float DrawnOffset;
		private int NewOffset;
		private Size InnerToggleSize;
		private Font InnerFont;
		private TextLocation InnerJustify;
		private string InnerText;
		private const int SPACING = 4;
		private SizeF TextSize;
		private Size InnerSize;
		//Constructor
		public SlidingToggleButton() {
			this.MinimumSize = new Size(30, 15);
			SmoothTimer = new System.Timers.Timer();
			SmoothTimer.Interval = SMOOTH_SCROLL_PERIOD;
			SmoothTimer.Elapsed += SmoothTimer_Tick;
			DrawnOffset = 2;
			InnerToggleSize = new Size(30, 15);
			InnerFont = base.Font;
			Justify = TextLocation.RIGHT;
			InnerText = this.Name;
			UpdateSize();
		}

		//Properties
		[Browsable(true)]
		public bool SolidStyle {
			get { return InnerSolidStyle; }
			set {
				if (InnerSolidStyle != value) {
					InnerSolidStyle = value;
					this.Invalidate();
				}
			}
		}
		public Color OffToggleColor {
			get {
				return offToggleColor;
			}
			set {
				if (offToggleColor != value) {
					offToggleColor = value;
					this.Invalidate();
				}
			}
		}

		public Size ToggleSize {
			get { return InnerToggleSize; }
			set {
				InnerToggleSize = value;
				UpdateSize();
			}
		}

		public override Font Font {
			get { return InnerFont; }
			set {
				InnerFont = value;
				UpdateSize();
			}
		}

		public TextLocation Justify {
			get { return InnerJustify; }
			set {
				InnerJustify = value;
				UpdateSize();
			}
		}

		public override string Text {
			get { return InnerText; }
			set {
				InnerText = value;
				UpdateSize();
			}
		}

		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}

		[DefaultValue(true)]


		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}
		private Color OutlineColorEn {
			get {
				if (Enabled) return Color.Gray;
				else return StyleHelper.GetInactiveColor(Color.Gray, BackColor);
			}
		}
		private Color OffToggleColorEn {
			get {
				if (Enabled) return OffToggleColor;
				else return StyleHelper.GetInactiveColor(offToggleColor, BackColor);
			}
		}

		//Methods

		private void UpdateSize() {
			if (InnerJustify == TextLocation.NONE) {
				MinimumSize = ToggleSize;
			} else {
				TextSize = TextRenderer.MeasureText(InnerText, InnerFont);
				MinimumSize = new Size((int)(ToggleSize.Width + SPACING + TextSize.Width),
					(int)Math.Max(ToggleSize.Height, TextSize.Height)
					);
			}
			MaximumSize = MinimumSize;
		}

		private void SmoothToggleTo(bool state) {
			if (state) {
				NewOffset = ToggleSize.Width - ToggleSize.Height + 1;
			} else {
				NewOffset = 2;
			}
			SmoothScrollDelta = ((float)NewOffset - DrawnOffset) / (float)SMOOTH_SCROLL_STEPS;
			if (DrawnOffset == (float)NewOffset) return;
			SmoothTimer.Start();
		}

		private void SmoothTimer_Tick(object sender, EventArgs e) {
			DrawnOffset += SmoothScrollDelta;
			if (SmoothScrollDelta > 0) {
				if (DrawnOffset >= NewOffset) {
					SmoothTimer.Stop();
					DrawnOffset = NewOffset;
				}
			} else {
				if (DrawnOffset <= NewOffset) {
					SmoothTimer.Stop();
					DrawnOffset = NewOffset;
				}
			}
			this.Invalidate();
		}

		private GraphicsPath GetFigurePath(PointF offset) {
			int arcSize = ToggleSize.Height - 1;
			RectangleF leftArc = new RectangleF(offset.X, offset.Y, arcSize, arcSize);
			RectangleF rightArc = new RectangleF(offset.X + ToggleSize.Width - arcSize - 2, offset.Y, arcSize, arcSize);
			GraphicsPath path = new GraphicsPath();
			path.StartFigure();
			path.AddArc(leftArc, 90, 180);
			path.AddArc(rightArc, 270, 180);
			path.CloseFigure();
			return path;
		}

		protected override void OnCheckedChanged(EventArgs e) {
			base.OnCheckedChanged(e);
			SmoothToggleTo(Checked);
		}

		protected override void OnMouseWheel(MouseEventArgs e) {
			base.OnMouseWheel(e);
			if ((e.Delta / 120) >= 1) {
				Checked = true;
			} else if ((e.Delta / 120) <= -1) {
				Checked = false;
			}
		}

		protected override void OnPaint(PaintEventArgs e) {
			float toggleSize = InnerToggleSize.Height - 5;
			Color toggleColor;
			PointF togglePt = new PointF(0, 0);
			PointF textPt = new PointF(0, 0);
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			e.Graphics.Clear(StyleHelper.GetParentColor(this));
			switch (InnerJustify) {
				case TextLocation.NONE:
					togglePt = new PointF(0, 0);
					break;
				case TextLocation.LEFT:
					textPt = new PointF(0, Math.Max(0f, (InnerToggleSize.Height - TextSize.Height) / 2f));
					togglePt = new PointF(TextSize.Width + SPACING, Math.Max(0f, (TextSize.Height - InnerToggleSize.Height) / 2f));
					break;
				case TextLocation.RIGHT:
					textPt = new PointF(InnerToggleSize.Width + SPACING, Math.Max(0f, (InnerToggleSize.Height - TextSize.Height) / 2f));
					togglePt = new PointF(0, Math.Max(0f, (TextSize.Height - InnerToggleSize.Height) / 2f));
					break;
			}
			//Draw the control surface
			if (InnerSolidStyle) e.Graphics.FillPath(new SolidBrush(BackColor), GetFigurePath(togglePt));
			using (Pen pen = new Pen(OutlineColorEn, 1.5f)) {
				pen.Alignment = PenAlignment.Inset;
				e.Graphics.DrawPath(pen, GetFigurePath(togglePt));
			}
			if (Checked) toggleColor = ForeColorEn;
			else toggleColor = OffToggleColorEn;
			e.Graphics.FillEllipse(new SolidBrush(toggleColor), new RectangleF(togglePt.X + DrawnOffset, togglePt.Y + 2.0f, toggleSize, toggleSize));
			if (InnerJustify != TextLocation.NONE) {
				e.Graphics.DrawString(InnerText, InnerFont, new SolidBrush(ForeColorEn), textPt);
			}
		}
	}

	public class ColorRadioButton : RadioButton, IVisualParameters {

		private Color InnerCheckedColor;
		private Color InnerUncheckedColor;
		private Color InnerOutlineColor;

		public ColorRadioButton() {
			InnerCheckedColor = ForeColor;
			InnerUncheckedColor = BackColor;
			InnerOutlineColor = ForeColor;
		}

		//Properties
		[Browsable(true)]
		public Color CheckedColor {
			get {
				return InnerCheckedColor;
			}
			set {
				InnerCheckedColor = value;
				this.Invalidate();
			}
		}

		public Color UncheckedColor {
			get {
				return InnerUncheckedColor;
			}
			set {
				InnerUncheckedColor = value;
				this.Invalidate();
			}
		}

		public Color OutlineColor {
			get {
				return InnerOutlineColor;
			}
			set {
				InnerOutlineColor = value;
				this.Invalidate();
			}
		}

		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}
		protected override void OnPaint(PaintEventArgs pevent) {
			base.OnPaint(pevent);
			Color circleColor;

			pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
			RectangleF rectRadio = new RectangleF(0, (ClientRectangle.Height - 13) / 2 - 1, 13, 13);
			pevent.Graphics.DrawEllipse(new Pen(InnerOutlineColor, 1.0f), rectRadio);
			rectRadio.Inflate(new Size(-3, -3));
			if (Checked) circleColor = InnerCheckedColor;
			else circleColor = InnerUncheckedColor;
			pevent.Graphics.FillEllipse(new SolidBrush(circleColor), rectRadio);
		}
	}

	public class RoundedCheckBox : CheckBox, IVisualParameters  {
		public enum TextLocation { NONE, LEFT, RIGHT };
		private ShapeSR InnerBorderShape;
		private bool InnerSolidStyle = true;
		private int NewOffset;
		private Size InnerCheckSize;
		private Font InnerFont;
		private TextLocation InnerJustify;
		private string InnerText;
		private const int SPACING = 4;
		private SizeF TextSize;
		private Size InnerSize;
		//Constructor
		public RoundedCheckBox() {
			this.MinimumSize = new Size(15, 15);
			InnerBorderShape = ShapeSR.ROUNDED;
			InnerCheckSize = new Size(15, 15);
			InnerFont = base.Font;
			Justify = TextLocation.RIGHT;
			InnerText = this.Name;
			UpdateSize();
		}

		//Properties
		[Browsable(true)]
		public ShapeSR BorderShape {
			get { return InnerBorderShape; }
			set {
				InnerBorderShape = value;
				this.Invalidate();
			}
		}

		public Size CheckSize {
			get { return InnerCheckSize; }
			set {
				int min = (int)Math.Min(value.Width, value.Width);
				InnerCheckSize = new Size(min, min);
				UpdateSize();
			}
		}

		public override Font Font {
			get { return InnerFont; }
			set {
				InnerFont = value;
				UpdateSize();
			}
		}

		public TextLocation Justify {
			get { return InnerJustify; }
			set {
				InnerJustify = value;
				UpdateSize();
			}
		}

		public override string Text {
			get { return InnerText; }
			set {
				InnerText = value;
				UpdateSize();
			}
		}

		[Browsable(false)]

		public VisualParams Visuals {
			set {
				this.BackColor = value.ControlBG;
				this.ForeColor = value.ControlFG;
				this.Invalidate();
			}
		}

		[DefaultValue(true)]
		public bool SolidStyle {
			get { return InnerSolidStyle; }
			set {
				if (InnerSolidStyle != value) {
					InnerSolidStyle = value;
					this.Invalidate();
				}
			}
		}

		private Color ForeColorEn {
			get {
				if (Enabled) return ForeColor;
				else return StyleHelper.GetInactiveColor(ForeColor, BackColor);
			}
		}
		private Color OutlineColorEn {
			get {
				if (Enabled) return Color.Gray;
				else return StyleHelper.GetInactiveColor(Color.Gray, BackColor);
			}
		}
		//Methods

		private void UpdateSize() {
			if (InnerJustify == TextLocation.NONE) {
				MinimumSize = InnerCheckSize;
			} else {
				TextSize = TextRenderer.MeasureText(InnerText, InnerFont);
				MinimumSize = new Size((int)(InnerCheckSize.Width + SPACING + TextSize.Width),
					(int)Math.Max(InnerCheckSize.Height, TextSize.Height)
					);
			}
			MaximumSize = MinimumSize;
		}

		protected override void OnCheckedChanged(EventArgs e) {
			base.OnCheckedChanged(e);
		}

		protected override void OnPaint(PaintEventArgs e) {
			Color checkColor;
			PointF checkPt = new PointF(0, 0);
			PointF textPt = new PointF(0, 0);
			e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
			e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			e.Graphics.Clear(StyleHelper.GetParentColor(this));
			switch (InnerJustify) {
				case TextLocation.NONE:
					checkPt = new PointF(0, 0);
					break;
				case TextLocation.LEFT:
					textPt = new PointF(0, Math.Max(0f, (InnerCheckSize.Height - TextSize.Height) / 2f));
					checkPt = new PointF(TextSize.Width + SPACING, Math.Max(0f, (TextSize.Height - InnerCheckSize.Height) / 2f));
					break;
				case TextLocation.RIGHT:
					textPt = new PointF(InnerCheckSize.Width + SPACING, Math.Max(0f, (InnerCheckSize.Height - TextSize.Height) / 2f));
					checkPt = new PointF(0, Math.Max(0f, (TextSize.Height - InnerCheckSize.Height) / 2f));
					break;
			}
			//Draw the control surface
			RectangleF checkRect = new RectangleF(checkPt, CheckSize);
			GraphicsPath penPath = StyleHelper.GetGraphicsPathSR(checkRect, InnerBorderShape, InnerCheckSize.Width / 4, 1.5f);
			if (InnerSolidStyle) {
				e.Graphics.FillPath(new SolidBrush(OutlineColorEn), penPath);
				if (Checked) checkColor = ForeColorEn;
				else checkColor = BackColor;
			} else {
				using (Pen pen = new Pen(OutlineColorEn, 1.5f)) {
					pen.Alignment = PenAlignment.Inset;
					e.Graphics.DrawPath(pen, penPath);
				}
				if (Checked) checkColor = ForeColorEn;
				else checkColor = BackColor;
			}
			penPath = StyleHelper.GetCheckPath(checkRect, (float)InnerCheckSize.Width / 4f);
			e.Graphics.DrawPath(new Pen(checkColor, (float)InnerCheckSize.Width / 6f), penPath);
			if (InnerJustify != TextLocation.NONE) {
				e.Graphics.DrawString(InnerText, InnerFont, new SolidBrush(ForeColorEn), textPt);
			}
		}

	}

	public class RoundedComboBox : Panel {
		private TextBox EditTextBox;
		//private Button DropButton;
		private DropDownForm DropForm;

		private class DropDownForm : Form {
			private RoundedVScrollBar Scroll;
			public int MaxItemsToShow;
			public Size CollapsedSize;
			private Size ExpandedSize;
			public List<string> Items;

			public DropDownForm() {
				Scroll = new RoundedVScrollBar();
				Items = new List<string>();
				MaxItemsToShow = 8;
				CollapsedSize = new Size(80, 24);
				ExpandedSize = CollapsedSize;
				this.StartPosition = FormStartPosition.Manual;
				this.FormBorderStyle = FormBorderStyle.FixedSingle;
			}

			public void Expand() {
				int itemsToShow = Math.Min(MaxItemsToShow, Items.Count());
				ExpandedSize = new Size(CollapsedSize.Width, CollapsedSize.Height * itemsToShow);
				this.Size = ExpandedSize;
			}

			public void Collapse() {
				this.Size = CollapsedSize;
			}
		}

		public List<string> Items {
			get { return DropForm.Items; }
			set { DropForm.Items = value; }
		}

		private void Expand() {
			DropForm.Left = this.Left;
			DropForm.Top = this.Bottom;
			DropForm.Expand();
		}

		private void Collapse() {
			DropForm.Collapse();
		}

		public RoundedComboBox() {
			this.BorderStyle = BorderStyle.FixedSingle;
			EditTextBox = new TextBox();
			DropForm = new DropDownForm();
			SetStyle(ControlStyles.UserPaint, true);
			Items.Add("Item 1");
			Items.Add("Item 2");
			Items.Add("Item 3");
			Expand();
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
		}
	}

	public class SliderSpinnerLink {
		RoundedSliderBase Slider;
		RoundedSpinner Spinner;
		public SliderSpinnerLink(RoundedSliderBase slider, RoundedSpinner spinner) {
			Slider = slider;
			Spinner = spinner;
			Slider.ValueChanged += CopyToSpinner;
			Spinner.ValueChanged += CopyToSlider;
			Slider.Scroll += ScrollToSpinner;	// this only goes 1-way
		}

		private void CopyToSpinner(object sender, EventArgs e) {
			Spinner.Value = Slider.Value;
		}

		private void ScrollToSpinner(object sender, EventArgs e) {
			Spinner.Value = Slider.ScrollValue;
		}

		private void CopyToSlider(object sender, EventArgs e) {
			// Don't do round-trip Value update if Slider is still being dragged
			if (!Slider.IsScrolling) Slider.Value = Spinner.Value;
		}

		public void SetCommonRange(decimal minimum, decimal maximum, decimal step, decimal value) {
			Spinner.Minimum = minimum;
			Slider.Minimum = minimum;
			Spinner.Maximum = maximum;
			Slider.Maximum = maximum;
			Spinner.Step = step;
			Slider.Step = step;
			Spinner.Value = value;
			Slider.Value = value;
		}

		public decimal Value {
			set {
				Spinner.Value = value;
				Slider.Value = value;
			}
			get {
				return Spinner.Value;
			}
		}

		public decimal Minimum {
			set {
				Spinner.Minimum = value;
				Slider.Minimum = value;
			}
			get {
				return Spinner.Value;
			}
		}

		public decimal Maximum {
			set {
				Spinner.Maximum = value;
				Slider.Maximum = value;
			}
			get {
				return Spinner.Value;
			}
		}

		public decimal Step {
			set {
				Spinner.Step = value;
				Slider.Step = value;
			}
			get {
				return Spinner.Step;
			}
		}
	}

	public class Helper {

		public class TextFilter {
			public Dictionary<string, string> FilterDictionary;
			public TextFilter() {
				FilterDictionary = new Dictionary<string, string>();
			}

			public void Clear() {
				FilterDictionary.Clear();
			}

			public void AddFilter(string key, string value) {
				FilterDictionary.Add(key, value);
			}

			public void RemoveFilter(string key) {
				FilterDictionary.Remove(key);
			}

			public string Filter(string input) {
				foreach (KeyValuePair<string, string> pair in FilterDictionary) {
					if (input.Contains(pair.Key)) {
						return input.Replace(pair.Key, pair.Value);
					}
				}
				return input;
			}
		}

		public static string EmbedComponent(object component) {
			StringBuilder sb = new StringBuilder();
			TextFilter filter = new TextFilter();
			filter.AddFilter("Boolean", "bool");
			filter.AddFilter("Int32", "int");
			filter.AddFilter("String", "string");
			filter.AddFilter("Void", "void");
			filter.AddFilter("Object", "object");

			// get component type
			Type componentType = component.GetType();
			string componentName = "Embedded" + componentType.Name;

			// list events
			EventInfo[] events = component.GetType().GetEvents(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
			foreach (EventInfo ev in events) {
				sb.Append(String.Format("public event {0} {1} {{\n", ev.EventHandlerType.Name, ev.Name));
				sb.Append(String.Format("\tadd {{ {0}.{1} += value; }}\n", componentName, ev.Name));
				sb.Append(String.Format("\tremove {{ {0}.{1} -= value; }}\n", componentName, ev.Name));
				sb.Append("}\n");
			}

			// list properties
			PropertyInfo[] props = component.GetType().GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
			foreach (PropertyInfo prop in props) {
				sb.Append(filter.Filter(String.Format("public {0} {1} {{\n", prop.PropertyType.Name, prop.Name)));
				if (prop.CanWrite) sb.Append(String.Format("\t set {{ {0}.{1} = value; }}\n", componentName, prop.Name));
				if (prop.CanRead) sb.Append(String.Format("\t get {{ return {0}.{1}; }}\n", componentName, prop.Name));
				sb.Append("}\n");
				// Do something with propValue
			}

			// list methods
			bool first;
			MethodInfo[] methods = component.GetType().GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
			foreach (MethodInfo method in methods) {
				if (method.Name.Contains("set_")) continue;
				if (method.Name.Contains("get_")) continue;
				if (method.Name.Contains("add_")) continue;
				if (method.Name.Contains("remove_")) continue;
				sb.Append(filter.Filter(String.Format("public {0} {1}(", method.ReturnType.Name, method.Name)));
				first = true;
				foreach (var param in method.GetParameters()) {
					if (!first) sb.Append(", ");
					first = false;
					if (param.ParameterType.IsByRef) sb.Append("ref ");
					else if (param.IsOut) sb.Append("out ");
					sb.Append(filter.Filter(param.ParameterType.Name) + " ");
					sb.Append(param.Name);

				}
				sb.Append(") {\n");
				if (method.ReturnType == typeof(void)) {
					sb.Append("\t");
				} else {
					sb.Append("\treturn ");
				}
				sb.Append(componentName + "." + method.Name + "(");
				first = true;
				foreach (var param in method.GetParameters()) {
					if (!first) sb.Append(", ");
					first = false;
					if (param.ParameterType.IsByRef) sb.Append("ref ");
					else if (param.IsOut) sb.Append("out ");
					sb.Append(param.Name);
				}
				sb.Append(");\n}\n");
			}

			Clipboard.SetText(sb.ToString());
			return sb.ToString();
		}
	}

	public class SynchronousPictureBox : PictureBox {
		public EventWaitHandle PaintDone;
		private Bitmap DisplayImage;
		private Bitmap InputImage;
		public bool Resizing;
		//Graphics gr;

		public SynchronousPictureBox() {
			PaintDone = new AutoResetEvent(false);
		}

		protected override void CreateHandle() {
			base.CreateHandle();
			//gr = Graphics.FromHwnd(this.Handle);
		}
		public new Bitmap Image {
			get {
				return InputImage;
			}
			set {
				InputImage = value;
				if (InputImage != null) {
					DisplayImage = new Bitmap(InputImage);
					this.Invalidate();
					//this.Refresh();
				}
			}
		}

		public void UpdateToDisplay() {
			if (InputImage != null) {
				CopyBitmap(InputImage, DisplayImage);
				this.Invalidate();
				//this.Refresh();
			}
		}

		protected override void OnPaint(PaintEventArgs pe) {
			//base.OnPaint(pe);
			if (DisplayImage != null) {
				Rectangle destRect = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
				Rectangle sourceRect = new Rectangle(0, 0, DisplayImage.Width, DisplayImage.Height);
				PaintBitmap(DisplayImage, sourceRect, pe.Graphics, destRect);
				PaintDone.Set();
			}
		}

		private void CopyBitmap(Bitmap src, Bitmap dst) {
			Rectangle srcRect = new Rectangle(0, 0, src.Width, src.Height);
			Rectangle dstRect = new Rectangle(0, 0, dst.Width, dst.Height);
			using (var graphics = Graphics.FromImage(dst)) {
				graphics.CompositingMode = CompositingMode.SourceCopy;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				graphics.DrawImage(src, dstRect, srcRect, GraphicsUnit.Pixel);
			}
		}

		private void PaintBitmap(Bitmap src, Rectangle srcRect, Graphics graphics, Rectangle dstRect) {
			graphics.CompositingMode = CompositingMode.SourceCopy;
			graphics.CompositingQuality = CompositingQuality.HighQuality;
			graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.DrawImage(DisplayImage, dstRect, srcRect, GraphicsUnit.Pixel);
		}
	}
}

