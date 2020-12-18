using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;

namespace PoorMansAmbilight {
	class Program {
		static ColorDisplayForm ColorForm;

		[STAThread]
		static void Main(string[] args) {
			PixelColor[] Colors = Utils.GenerateColors().ToArray();

			float Dist = Utils.Distance(new PixelColor(255, 0, 0), new PixelColor(0, 255, 0));
			Dist = Utils.Distance(new PixelColor(255, 0, 255), new PixelColor(0, 0, 255));

			Application.EnableVisualStyles();
			ColorForm = new ColorDisplayForm();

			Thread Main2Thread = new Thread(Thread2);
			Main2Thread.IsBackground = true;
			Main2Thread.Start();

			Application.Run(ColorForm);
		}

		static void Thread2() {
			AuraSDK.Init();

			DesktopDuplicator Dupl = new DesktopDuplicator(0);
			Dupl.OnFrame += OnFrame;

			int FPS = 30;
			int Framerate = (int)((1.0f / FPS) * 1000);

			while (true) {
				Dupl.GetLatestFrame();
				Thread.Sleep(Framerate);
			}
		}

		static int FrameCtr = 0;

		static void OnFrame(DesktopDuplicator This, int W, int H) {
			int Offset = 50;

			int R = 0;
			int G = 0;
			int B = 0;
			int Samples = 0;

			for (int Y = 0; Y < H; Y += Offset)
				for (int X = 0; X < W; X += Offset) {
					PixelColor P = This.GetPixel(X, Y);

					R += P.R * P.R;
					G += P.G * P.G;
					B += P.B * P.B;
					Samples++;
				}

			int AvgR = (byte)Math.Sqrt(R / Samples);
			int AvgG = (byte)Math.Sqrt(G / Samples);
			int AvgB = (byte)Math.Sqrt(B / Samples);

			Contrast(ref AvgR, ref AvgG, ref AvgB, 50);

			AuraSDK.SetRGB((byte)AvgR, (byte)AvgG, (byte)AvgB);
			ColorForm.SetColor((byte)AvgR, (byte)AvgG, (byte)AvgB);

			Console.WriteLine("Frame {0}, samples {1}", FrameCtr++, Samples);
		}

		static void Contrast(ref int R, ref int G, ref int B, int Amt) {
			Contrast(ref R, Amt);
			Contrast(ref G, Amt);
			Contrast(ref B, Amt);
		}

		static void Contrast(ref int Clr, int Amt) {
			float Factor = (259.0f * (Amt + 255.0f)) / (255.0f * (259.0f - Amt));
			Clr = (int)Math.Truncate(Factor * ((float)Clr - 128.0f) + 128.0f);
		}
	}
}
