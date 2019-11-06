using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DTMEditor.FileHandling;

//TODO

// Menu Bar Functions
// Bookmarks
// Bugfix: Active section checkboxes take 2 clicks to start changing???

namespace pianokeys
{
    public partial class Form1 : Form
    {
        public Frame ActiveFrame;
        BindingSource frameDataSource;
        BindingList<Frame> frameList;
        DTM loadedFile;

        public Form1()
        {
            InitializeComponent();
            ActiveFrame = new Frame();
            var t = new Frame();
            activeFrameBindingSource.Add(t);
            frameList = new BindingList<Frame>();
            loadedFile = null;
            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            frameDataSource = new BindingSource(frameList, null);
            frameDataGridView.DataSource = frameDataSource;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            frameDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
            var row = frameDataGridView.Rows[e.RowIndex].Index;
            Frame activeFrame = frameList[row];
            showActiveFrame(activeFrame, row);
        }

        private void gridContextMenu_Opening(object sender, CancelEventArgs e)
        {
            
        }

        private void SliderValueChanged(object sender, EventArgs e)
        {
            activeFrameBindingSource.EndEdit();
            activeFrameBindingSource.ResetCurrentItem();
        }

        private void showActiveFrame(Frame t, int frameNumber)
        {
            selectedFrameBox.Enabled = true;
            ActiveFrame = t;
            selectedFrameBox.Text = "Frame " + frameNumber.ToString();
            activeFrameBindingSource.Clear();
            activeFrameBindingSource.Add(t);
            activeFrameBindingSource.EndEdit();
            activeFrameBindingSource.ResetCurrentItem();
        }


        private void dataGridSelectionChanged(object sender, EventArgs e)
        {
            //If only one frame is selected, set it as the active display frame
            //else disable upper panel
            bool enableActivePanel = true;
            
            DataGridViewSelectedCellCollection selection = frameDataGridView.SelectedCells;
            
            HashSet<int> selectedRowList = new HashSet<int>();
            foreach (DataGridViewCell selectedThing in selection)
            {
                selectedRowList.Add(selectedThing.OwningRow.Index);
            }

            if (selectedRowList.Count == 1)
            {
                var firstRow = frameDataGridView.Rows[selectedRowList.First()].Index;

                if (firstRow < frameDataGridView.RowCount - 1)
                {
                    Frame activeFrame = frameList.ElementAt(firstRow);
                    showActiveFrame(activeFrame, firstRow);
                    enableActivePanel = true;
                }
                else
                {
                    enableActivePanel = false;
                }
            }
            else
            {
                enableActivePanel = false;
            }

            selectedFrameBox.Enabled = enableActivePanel;
        }
        

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            DTM inputFile = null;
            try
            {
                inputFile = new DTM(openFileDialog1.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Load Error", MessageBoxButtons.OK);
            }
            if (inputFile != null)
            {
                loadedFile = inputFile;

                frameList.Clear();

                foreach (DTMEditor.FileHandling.ControllerData.DTMControllerDatum datum in inputFile.ControllerData)
                {
                    frameList.Add(new Frame(datum));
                }
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (loadedFile != null)
            {
                loadedFile.ControllerData.Clear();
                foreach (Frame frame in frameList)
                {
                    loadedFile.ControllerData.Add(Frame.MakeFileDatum(frame));
                }
                loadedFile.Save(loadedFile.FilePath);
            }
        }
        

        private void frameDataGridView_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            for (int i = Math.Max(e.RowIndex - 1, 0); i < frameDataGridView.RowCount - 1; i++)
            {
                frameDataGridView.Rows[i].HeaderCell.Value = i.ToString();
            }
        }

        private void frameDataGridView_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            for (int i = Math.Max(e.RowIndex - 1, 0); i < frameDataGridView.RowCount - 1; i++)
            {
                frameDataGridView.Rows[i].HeaderCell.Value = i.ToString();
            }
        }
    }

}
