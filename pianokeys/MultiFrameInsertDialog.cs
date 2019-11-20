using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pianokeys
{
    public partial class MultiFrameInsertDialog : Form
    {
        public bool InsertFramesAfter { get; protected set; }
        public uint FrameCount { get; protected set; }

        public MultiFrameInsertDialog()
        {
            InsertFramesAfter = true;
            FrameCount = 1;
            InitializeComponent();
        }

        private void insAfterButton_CheckedChanged(object sender, EventArgs e)
        {
            InsertFramesAfter = insAfterButton.Checked;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            FrameCount = (uint)numericUpDown1.Value;
        }
    }
}
