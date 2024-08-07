using System.Drawing.Drawing2D;

namespace CustomControls {
	public enum ShapeSR { SQUARE, ROUNDED };
	public enum ShapeSRE { SQUARE, ROUNDED, ELLIPSE };
	public enum TransitionEffect { NONE, FADE, SCROLL };

	public class StyleHelper {
		public static GraphicsPath GetRoundPath(RectangleF rect, float radius, float margin) {
			RectangleF shrunk = new RectangleF(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2);
			return GetRoundPath(shrunk, radius);
		}

		// Draw path from top-left in clockwise direction.
		// Top-left arc sweeps from 180->270deg, followed by 270->0, 0->90, finally 90->180
		// https://www.codeproject.com/Articles/1010822/RoundedButton-Control-Demystifying-DrawArc
		public static GraphicsPath GetRoundPath(RectangleF rect, float radius) {
			float diameter = radius * 2f;
			float pullIn = 0.0f;
			RectangleF shrunk = new RectangleF(rect.X, rect.Y, rect.Width - pullIn, rect.Height - pullIn);
			GraphicsPath graphPath = new GraphicsPath();
			graphPath.AddArc(shrunk.Left, shrunk.Top, diameter, diameter, 180, 90);	// upper-left
			//graphPath.AddLine(shrunk.Left + radius, shrunk.Top, shrunk.Right - radius, shrunk.Top);
			graphPath.AddArc(shrunk.Right - diameter, shrunk.Top, diameter, diameter, 270, 90); // upper-right
			//graphPath.AddLine(shrunk.Right, shrunk.Top + radius, shrunk.Right, shrunk.Bottom - radius);
			graphPath.AddArc(shrunk.Right - diameter, shrunk.Bottom - diameter, diameter, diameter, 0, 90); // lower-right
			//graphPath.AddLine(shrunk.Right - radius, shrunk.Bottom, shrunk.Left + radius, shrunk.Bottom);
			graphPath.AddArc(shrunk.Left, shrunk.Bottom - diameter, diameter, diameter, 90, 90); // lower-left
			//graphPath.AddLine(shrunk.Left, shrunk.Bottom - radius, shrunk.Left, shrunk.Top + radius);
			graphPath.CloseFigure();
			return graphPath;
		}
		public static GraphicsPath GetRoundPathLeftOnly(RectangleF rect, float radius) {
			float diameter = radius * 2f;
			float pullIn = 0.0f;
			RectangleF shrunk = new RectangleF(rect.X, rect.Y, rect.Width - pullIn, rect.Height - pullIn);
			GraphicsPath graphPath = new GraphicsPath();
			graphPath.AddArc(shrunk.Left, shrunk.Top, diameter, diameter, 180, 90); // upper-left
			graphPath.AddLine(shrunk.Left + radius, shrunk.Top, shrunk.Right, shrunk.Top);
			graphPath.AddLine(shrunk.Right, shrunk.Top, shrunk.Right, shrunk.Bottom);
			graphPath.AddLine(shrunk.Right, shrunk.Bottom, shrunk.Left + radius, shrunk.Bottom);
			graphPath.AddArc(shrunk.Left, shrunk.Bottom - diameter, diameter, diameter, 90, 90); // lower-left
			//graphPath.AddLine(shrunk.Left, shrunk.Bottom - radius, shrunk.Left, shrunk.Top + radius);
			graphPath.CloseFigure();
			return graphPath;
		}
		public static GraphicsPath GetRoundPathRightOnly(RectangleF rect, float radius) {
			float diameter = radius * 2f;
			float pullIn = 0.0f;
			RectangleF shrunk = new RectangleF(rect.X, rect.Y, rect.Width - pullIn, rect.Height - pullIn);
			GraphicsPath graphPath = new GraphicsPath();
			graphPath.AddLine(shrunk.Left, shrunk.Top, shrunk.Right - radius, shrunk.Top);
			graphPath.AddArc(shrunk.Right - diameter, shrunk.Top, diameter, diameter, 270, 90); // upper-right
			graphPath.AddArc(shrunk.Right - diameter, shrunk.Bottom - diameter, diameter, diameter, 0, 90); // lower-right
			graphPath.AddLine(shrunk.Right - radius, shrunk.Bottom, shrunk.Left, shrunk.Bottom);
			graphPath.AddLine(shrunk.Left, shrunk.Bottom, shrunk.Left, shrunk.Top);
			graphPath.CloseFigure();
			return graphPath;
		}

		public static GraphicsPath GetSquarePath(RectangleF rect, float margin) {
			RectangleF shrunk = new RectangleF(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2);
			return GetSquarePath(shrunk);
		}

		public static GraphicsPath GetSquarePath(RectangleF rect) {
			float pullIn = 0.0f;
			RectangleF shrunk = new RectangleF(rect.X, rect.Y, rect.Width - pullIn, rect.Height - pullIn);
			GraphicsPath graphPath = new GraphicsPath();
			graphPath.AddLine(shrunk.Left, shrunk.Top, shrunk.Right, shrunk.Top);
			graphPath.AddLine(shrunk.Right, shrunk.Top, shrunk.Right, shrunk.Bottom);
			graphPath.AddLine(shrunk.Right, shrunk.Bottom, shrunk.Left, shrunk.Bottom);
			graphPath.AddLine(shrunk.Left, shrunk.Bottom, shrunk.Left, shrunk.Top);
			graphPath.CloseFigure();
			return graphPath;
		}
		public static GraphicsPath GetCheckPath(RectangleF rect, float margin) {
			float pullIn = 0.0f;
			PointF[] checkPoints = new PointF[3];
			RectangleF shrunk = new RectangleF(rect.X, rect.Y, rect.Width - pullIn, rect.Height - pullIn);
			GraphicsPath graphPath = new GraphicsPath();
			checkPoints[0] = new PointF(shrunk.X + margin, (shrunk.Y + shrunk.Bottom) / 2.0f);
			checkPoints[1] = new PointF(shrunk.X + margin + ((shrunk.Width - margin * 2) / 3.0f), shrunk.Bottom - margin);
			checkPoints[2] = new PointF(shrunk.Right - margin, shrunk.Y + margin);
			graphPath.AddLine(checkPoints[0], checkPoints[1]);
			graphPath.AddLine(checkPoints[1], checkPoints[2]);
			return graphPath;
		}
		public static Color AdjustBrightness(Color color, double ratio) {
			int r, g, b;
			r = Math.Min((int)(color.R * ratio), 255);
			g = Math.Min((int)(color.G * ratio), 255);
			b = Math.Min((int)(color.B * ratio), 255);
			return Color.FromArgb(color.A, r, g, b);
		}
		public static Color GetParentColor(Control control) {
			Color backColor;
			Control parent = control.Parent;
			do {
				backColor = parent.BackColor;
				parent = parent.Parent;
			} while ((backColor == Color.Transparent) && (parent != null));
			return backColor;
		}
		public static bool HitTest(Rectangle rect, Point pt) {
			return HitTest(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), pt);
		}
		public static bool HitTest(RectangleF rect, Point pt) {
			if ((pt.X >= rect.X) && (pt.X < rect.Right) &&
				(pt.Y >= rect.Y) && (pt.Y < rect.Bottom)) {
				return true;
			} else return false;
		}
		public static Color GetInactiveColor(Color activeColor, Color background) {
			int A, R, G, B;
			A = (activeColor.A + background.A) / 2;
			R = (activeColor.R + background.R) / 2;
			G = (activeColor.G + background.G) / 2;
			B = (activeColor.B + background.B) / 2;
			Color inactive = Color.FromArgb(A, R, G, B);
			return inactive;
		}
		public static GraphicsPath GetGraphicsPathSR(RectangleF rect, ShapeSR shape, float radius, float width, float margin = 0) {
			RectangleF inner = new RectangleF(rect.X + width / 2f, rect.Y + width / 2f, rect.Width - width, rect.Height - width); 
			GraphicsPath path;
			switch (shape) {
				case ShapeSR.SQUARE:
					path = GetSquarePath(inner, margin);
					break;
				case ShapeSR.ROUNDED:
					path = GetRoundPath(inner, radius, margin);
					break;
				default:
					path = GetSquarePath(inner);
					break;
			}
			return path;
		}

		public static GraphicsPath GetGraphicsPathSRE(RectangleF rect, ShapeSRE shape, float radius, float width, float margin = 0) {
			RectangleF inner = new RectangleF(rect.X + width / 2f, rect.Y + width / 2f, rect.Width - width, rect.Height - width);
			GraphicsPath path;
			switch (shape) {
				case ShapeSRE.SQUARE:
					path = GetSquarePath(inner, margin);
					break;
				case ShapeSRE.ROUNDED:
					path = GetRoundPath(inner, radius, margin);
					break;
				case ShapeSRE.ELLIPSE:
					float rad = Math.Min(inner.Width, inner.Height) / 2f - margin;
					path = GetRoundPath(inner, rad, margin);
					break;
				default:
					path = GetSquarePath(inner, margin);
					break;
			}
			return path;
		}
	}

}