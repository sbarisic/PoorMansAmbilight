using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PoorMansAmbilight {
	public partial class ColorDisplayForm : Form {
		public ColorDisplayForm() {
			InitializeComponent();
		}

		private void ColorDisplayForm_Load(object sender, EventArgs e) {

		}

		public void SetColor(byte R, byte G, byte B) {
			BackColor = Color.FromArgb(R, G, B);
		}
	}
}
