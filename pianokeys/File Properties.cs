using DTMEditor.FileHandling;
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
    public partial class FilePropertiesDialog : Form
    {
        public string authorName;

        public FilePropertiesDialog(DTM dtmHeader)
        {
            if (dtmHeader != null)
                authorName = Encoding.UTF8.GetString(dtmHeader.Author);
            else
                authorName = "";
            InitializeComponent();
            textBox2.Text = authorName;
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            authorName = textBox2.Text;
        }
    }
}
