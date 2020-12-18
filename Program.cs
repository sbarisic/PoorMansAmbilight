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
			Colors = Utils.GenerateColors().ToArray();
			Dists = new float[Colors.Length];

			Application.EnableVisualStyles();
			ColorForm = new ColorDisplayForm();

			Thread Main2Thread = new Thread(Thread2);
			Main2Thread.IsBackground = true;
			Main2Thread.Start();

			Application.Run(ColorForm);
		}

		static void Thread2() {
			RGB_SDK.Init();

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
		static PixelColor[] Colors;
		static float[] Dists;

		static void OnFrame(DesktopDuplicator This, int W, int H) {
			int Offset = 60;

			ulong R = 0;
			ulong G = 0;
			ulong B = 0;
			ulong Samples = 0;

			for (int Y = 0; Y < H; Y += Offset)
				for (int X = 0; X < W; X += Offset) {
					PixelColor P = This.GetPixel(X, Y);

					R += (ulong)P.R * (ulong)P.R;
					G += (ulong)P.G * (ulong)P.G;
					B += (ulong)P.B * (ulong)P.B;
					Samples++;
				}

			byte AvgR = (byte)Math.Sqrt(R / Samples);
			byte AvgG = (byte)Math.Sqrt(G / Samples);
			byte AvgB = (byte)Math.Sqrt(B / Samples);

			ColorForm.SetColor(AvgR, AvgG, AvgB);


			RGB_SDK.SetRGB(AvgR, AvgG, AvgB);
			ColorForm.SetColor2(AvgR, AvgG, AvgB);

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
