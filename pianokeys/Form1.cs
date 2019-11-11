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
        private Frame ActiveFrame;
        private BindingSource frameDataSource;
        private BindingList<Frame> frameList;
        private DTM loadedFile;

        private List<DataGridViewRow> bookmarkedRows;

        private List<Frame> clipboard;

        public Form1()
        {
            InitializeComponent();
            ActiveFrame = new Frame();
            var t = new Frame();
            activeFrameBindingSource.Add(t);
            frameList = new BindingList<Frame>();
            loadedFile = null;
            bookmarkedRows = new List<DataGridViewRow>();
            clipboard = new List<Frame>();
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

        private HashSet<int> getSelectedRowsFromCells()
        {
            var selection = frameDataGridView.SelectedCells;
            HashSet<int> selectedRowList = new HashSet<int>();
            foreach (DataGridViewCell selectedThing in selection)
            {
                int index = selectedThing.OwningRow.Index;
                if (index < frameDataGridView.Rows.Count - 1)
                selectedRowList.Add(index);
            }
            return selectedRowList;
        }

        private int singleSelectedRow(DataGridViewSelectedCellCollection selection)
        {
            if (selection.Count <= 0)
                return -1;

            HashSet<int> selectedRowList = new HashSet<int>();
            foreach (DataGridViewCell selectedThing in selection)
            {
                selectedRowList.Add(selectedThing.OwningRow.Index);
            }

            if (selectedRowList.Count == 1)
            {
                var firstRow = frameDataGridView.Rows[selectedRowList.First()].Index;
                return firstRow;
            }
            //otherwise invalid
            return -1;
        }


        private void dataGridSelectionChanged(object sender, EventArgs e)
        {
            //If only one frame is selected, set it as the active display frame
            //else disable upper panel
            bool enableActivePanel = true;
            
            DataGridViewSelectedCellCollection selection = frameDataGridView.SelectedCells;
            int row = singleSelectedRow(selection);
            if (row != -1)
            {
                Frame activeFrame = frameList.ElementAt(row);
                showActiveFrame(activeFrame, row);
                enableActivePanel = true;
            }
            else
            {
                enableActivePanel = false;
            }

            selectedFrameBox.Enabled = enableActivePanel;
        }
        
        

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            //TODO Load adjacent bookmarks file if present
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
            saveFile(loadedFile.FilePath);
        }
        
        private void saveFile(string path)
        {
            //TODO Save adjacent bookmarks file if present
            if (loadedFile != null)
            {
                loadedFile.ControllerData.Clear();
                foreach (Frame frame in frameList)
                {
                    loadedFile.ControllerData.Add(Frame.MakeFileDatum(frame));
                }
                loadedFile.Save(path);
                loadedFile.FilePath = path;
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

        private void fileInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //TODO Show a form with info about the loaded file
            
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Pick somewhere else to save a file
            var result = saveFileDialog1.ShowDialog();
            if (result == DialogResult.OK && saveFileDialog1.FileName.Length > 0)
            {
                saveFile(saveFileDialog1.FileName);
            }

        }

        private void gridContextMenu_Opening_1(object sender, CancelEventArgs e)
        {
            HashSet<int> selectedRowList = getSelectedRowsFromCells();

            bool singleRowSelected = selectedRowList.Count == 1;
            bool anyRowsSelected = selectedRowList.Count > 0;

            copyFramesToolStripMenuItem.Enabled = anyRowsSelected;
            pasteToolStripMenuItem.Enabled = anyRowsSelected;
            pasteAfterToolStripMenuItem.Enabled = anyRowsSelected;
            insertAfterMenuItem.Enabled = anyRowsSelected;
            insertBeforeMenuItem.Enabled = anyRowsSelected;
            insertMultipleMenuItem.Enabled = anyRowsSelected;
            deleteMenuItem.Enabled = anyRowsSelected;
            clearSelectedValuesToolStripMenuItem.Enabled = anyRowsSelected;


            if (gridContextMenu.Parent == frameDataGridView)
            {
                gridContextMenu.Show(this, frameDataGridView.PointToClient(Cursor.Position));
            }
        }

        private void clearSelectedValuesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selection = frameDataGridView.SelectedCells;
            if (singleSelectedRow(selection) != -1)
            {
                foreach (DataGridViewCell cell in selection)
                {
                    if (cell.ValueType == typeof(bool))
                    {
                        cell.Value = false;
                    }
                    else if (cell.ValueType == typeof(byte))
                    {
                        var columnName = cell.OwningColumn.Name;
                        if (columnName[1] == 'P')
                            cell.Value = 0;
                        else
                            cell.Value = 128;
                    }
                }
            }

        }

        private void insertBeforeMenuItem_Click(object sender, EventArgs e)
        {
            HashSet<int> selectedRowList = getSelectedRowsFromCells();
            if (selectedRowList.Count > 0)
                frameList.Insert(selectedRowList.Min(), new Frame());
        }

        private void insertAfterMenuItem_Click(object sender, EventArgs e)
        {
            HashSet<int> selectedRowList = getSelectedRowsFromCells();
            if (selectedRowList.Count > 0)
                frameList.Insert(selectedRowList.Max() + 1, new Frame());
        }

        private void insertMultipleMenuItem_Click(object sender, EventArgs e)
        {
            //TODO prompt for a number of rows and whether to add them before or
            // after the selected row(s)
        }

        private void deleteMenuItem_Click(object sender, EventArgs e)
        {
            var selectedRowList = getSelectedRowsFromCells();
            var toDelete = new List<DataGridViewRow>();
            foreach (var index in selectedRowList)
            {
                toDelete.Add(frameDataGridView.Rows[index]);
            }
            foreach (DataGridViewRow row in toDelete)
            {
                frameDataGridView.Rows.Remove(row);
            }
        }

        private void undoToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //TODO Get old item state from change stack, make changes
            //note: maybe, maybe not, depending on undo-ability of gridview's built-in adds/deletes
        }

        private void redoToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //TODO get item state from redo stack, make changes
            //note: maybe, maybe not, depending on undo-ability of gridview's built-in adds/deletes

        }

        private void copyFramesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // copy selected rows to clipboard
            HashSet<int> selectedRows = getSelectedRowsFromCells();

            if (selectedRows.Count > 0)
            {
                clipboard.Clear();
                foreach (int rowIndex in selectedRows)
                {
                    clipboard.Add(new Frame(frameList[rowIndex]));
                }
            }
        }

        private void pasteBeforeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // paste rows before selection
            HashSet<int> selectedRowList = getSelectedRowsFromCells();
            if (selectedRowList.Count > 0 && clipboard.Count > 0)
            {
                frameList.RaiseListChangedEvents = false;
                int insertPosition = selectedRowList.Min();
                for (int i = clipboard.Count - 1; i >= 0; i--)
                {
                    if (i == 0)
                        frameList.RaiseListChangedEvents = true;
                    frameList.Insert(insertPosition, new Frame(clipboard[i]));
                }
            }
        }
        
        private void pasteAfterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // paste rows after selection, or at the end of the file
            HashSet<int> selectedRowList = getSelectedRowsFromCells();
            if (selectedRowList.Count > 0 && clipboard.Count > 0)
            {
                frameList.RaiseListChangedEvents = false;
                int insertPosition = selectedRowList.Max() + 1;
                for (int i = clipboard.Count - 1; i >= 0; i--)
                {
                    if (i == 0)
                        frameList.RaiseListChangedEvents = true;
                    frameList.Insert(insertPosition, new Frame(clipboard[i]));
                }
            }
        }
        private void bookmarkFrameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //TODO add a nameable bookmark to the selected row
        }
        
    }

}
