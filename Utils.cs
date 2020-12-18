using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Mathematics.Interop;

namespace PoorMansAmbilight {
	static class Utils {
		public static int GetX(this RawRectangle Rect) {
			return Rect.Left;
		}

		public static int GetY(this RawRectangle Rect) {
			return Rect.Top;
		}

		public static int GetWidth(this RawRectangle Rect) {
			return Rect.Right - Rect.Left;
		}

		public static int GetHeight(this RawRectangle Rect) {
			return Rect.Bottom - Rect.Top;
		}

		public static float Distance(PixelColor A, PixelColor B) {
			HSVColor HSV_0 = A;
			HSVColor HSV_1 = B;

			double Hue_0 = HSV_0.H;
			double Hue_1 = HSV_1.H;

			double Sat_0 = HSV_0.S / 255.0;
			double Sat_1 = HSV_1.S / 255.0;

			double Val_0 = HSV_0.V;
			double Val_1 = HSV_1.V;

			double dHue = Math.Min(Math.Abs(Hue_1 - Hue_0), 360 - Math.Abs(Hue_1 - Hue_0)) / 180.0;
			double dSat = Math.Abs(Sat_1 - Sat_0);
			double dVal = Math.Abs(Val_1 - Val_0) / 255.0;

			return (float)Math.Sqrt(dHue * dHue + dSat * dSat + dVal * dVal);
		}

		public static int ClosestColor(float[] Distances, PixelColor[] Colors, PixelColor Test) {
			for (int i = 0; i < Colors.Length; i++)
				Distances[i] = Distance(Colors[i], Test);

			float Min = Distances[0];
			int Idx = 0;

			for (int i = 0; i < Distances.Length; i++) {
				if (Distances[i] < Min) {
					Idx = i;
					Min = Distances[i];
				}
			}

			return Idx;
		}

		public static int Clamp(int Val, int Nearest) {
			return (int)(Math.Round((float)Val / Nearest) * Nearest);
		}

		public static PixelColor Clamp(PixelColor Clr, int Nearest) {
			return new PixelColor((byte)Clamp(Clr.R, Nearest), (byte)Clamp(Clr.G, Nearest), (byte)Clamp(Clr.B, Nearest));
		}

		public static IEnumerable<PixelColor> GenerateColors() {
			int SSteps = 5;
			int VSteps = 10;

			yield return new PixelColor(0, 0, 0);
			yield return new PixelColor(255, 255, 255);

			for (int H = 0; H < 360; H++) {
				for (int S = 0; S < SSteps; S++) {
					for (int V = 0; V < VSteps; V++) {
						byte SVal = (byte)(((100.0f / 5) * S) / 100.0f * 255);
						byte VVal = (byte)(((100.0f / 5) * S) / 100.0f * 255);

						yield return new HSVColor(H, SVal, VVal);
					}
				}
			}
		}
	}
}
