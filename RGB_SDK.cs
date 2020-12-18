using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoorMansAmbilight {
	static class RGB_SDK {
		public static void Init() {
			Console.WriteLine("Init Aura RGB");
		}

		public static void SetRGB(byte R, byte G, byte B) {
			Console.WriteLine("Setting {0}, {1}, {2}", R, G, B);
		}
	}
}
