using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CourseWork
{
    public partial class CreateTorrent : Form
    {
        public CreateTorrent()
        {
            InitializeComponent();
        }

        private void FileButton_Click(object sender, EventArgs e)
        {
            if (OpenFileDia.ShowDialog() == DialogResult.OK)
            {

            }
        }

        private void FolderButton_Click(object sender, EventArgs e)
        {
            if (FolderBrowserDia.ShowDialog() == DialogResult.OK)
            {

            }
        }

        private void CreateButton_Click(object sender, EventArgs e)
        {

        }
    }
}
