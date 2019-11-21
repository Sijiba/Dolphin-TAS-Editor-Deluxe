using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DTMEditor.FileHandling;
using Equin.ApplicationFramework;

//TODO

// Replace stalling moments with progress bar/disabling controls
// Undo/Redo
// Lol what if we could "video preview" by running dolphin with the current movie
// Bugfix: Active section checkboxes take 2 clicks to start changing???

namespace pianokeys
{
    public partial class Form1 : Form
    {
        private Frame ActiveFrame;
        private List<Frame> frameList;
        private BindingSource frameSource;
        private BindingListView<Frame> frameView;
        private DTM loadedFile;

        private List<Frame> clipboard;

        public Form1()
        {
            InitializeComponent();
            filterComboBox.SelectedIndex = 0;
            ActiveFrame = new Frame();
            var t = new Frame();
            activeFrameBindingSource.Add(t);
            frameList = new List<Frame>();
            frameView = new BindingListView<Frame>(frameList);
            frameSource = new BindingSource(frameView, null);
            loadedFile = null;
            clipboard = new List<Frame>();
            frameSource.CurrentChanged += frameSource_CurrentChanged;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            frameDataGridView.DataSource = frameSource;
            frameNavigator.BindingSource = frameSource;
        }
        

        private void frameSource_CurrentChanged(object sender, EventArgs e)
        {
            frameDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
            frameSource.EndEdit();

            Frame currentFrame = ((ObjectView<Frame>)frameSource.Current).Object;
            int index = frameList.IndexOf(currentFrame);
            if (index != -1)
            {
                selectedFrameBox.Text = "Frame " + (index + 1).ToString();

                activeFrameBindingSource.Clear();
                activeFrameBindingSource.Add(currentFrame);
                activeFrameBindingSource.EndEdit();
                activeFrameBindingSource.ResetCurrentItem();
            }
            selectedFrameBox.Enabled = index != -1;
        }

        private void SliderValueChanged(object sender, EventArgs e)
        {
            activeFrameBindingSource.EndEdit();
            activeFrameBindingSource.ResetCurrentItem();
            frameSource.ResetCurrentItem();
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

        private int singleSelectedRow()
        {
            DataGridViewSelectedCellCollection selection = frameDataGridView.SelectedCells;
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


        private void stopListEvents()
        {
            frameDataGridView.SuspendLayout();
            frameNavigator.SuspendLayout();
        }

        private void continueListEvents()
        {
            frameView.Refresh();
            frameDataGridView.ResumeLayout(true);
            frameNavigator.ResumeLayout(true);
        }
        
        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string path = openFileDialog1.FileName;
                DTM inputFile = null;
                try
                {
                    inputFile = new DTM(path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Load Error", MessageBoxButtons.OK);
                }
                if (inputFile != null)
                {
                    loadedFile = inputFile;
                    stopListEvents();
                    frameList.Clear();

                    foreach (DTMEditor.FileHandling.ControllerData.DTMControllerDatum datum in inputFile.ControllerData)
                    {
                        frameList.Add(new Frame(datum));
                    }

                    // Load adjacent bookmarks file if present
                    string notesPath = Path.ChangeExtension(path, ".log");
                    if (File.Exists(notesPath))
                    {
                        using (StreamReader file =
                            new StreamReader(notesPath))
                        {
                            while (!file.EndOfStream)
                            {
                                string[] valuePair = file.ReadLine().Split('\t');
                                int frame = int.Parse(valuePair[0]) - 1;
                                string msg = valuePair[1].Trim();
                                if (frameList.Count > frame && frame >= 0)
                                    frameList[frame].Note = msg;
                            }
                        }
                    }

                    continueListEvents();
                    statusLabel.Text = "Loaded " + Path.GetFileName(path) + ".";
                }
            }
            
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (loadedFile != null)
            {
                saveFile(loadedFile.FilePath);
            }
        }

        private void saveFile(string path)
        {
            if (loadedFile != null)
            {
                var noteData = new List<Tuple<int, string>>();
                loadedFile.ControllerData.Clear();
                for (int i = 0; i < frameList.Count; i++)
                {
                    Frame frame = frameList[i];
                    loadedFile.ControllerData.Add(Frame.MakeFileDatum(frame));
                    var note = frame.Note;
                    if (note.Length > 0)
                    {
                        noteData.Add(new Tuple<int, string>(i + 1, note));
                    }
                }
                loadedFile.Save(path);
                loadedFile.FilePath = path;

                string msg = "Saved " + Path.GetFileName(path);

                // Save adjacent notes file if notes are present
                if (noteData.Count > 0)
                {
                    string notesPath = Path.ChangeExtension(path, ".log");

                    using (StreamWriter file =
                        new StreamWriter(notesPath))
                    {
                        foreach (var tuple in noteData)
                        {
                            file.Write(tuple.Item1.ToString() + "\t");
                            file.Write(tuple.Item2.ToString() + "\n");
                        }
                    }
                    msg += " and its notes";
                }
                msg += ".";

                statusLabel.Text = msg;
            }
            else
            {
                MessageBox.Show("You can't save a movie file without specifying a game. Load an existing TAS file.",
                    "Failed to save", MessageBoxButtons.OK);
            }
        }
        

        private void fileInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show a form with info about the loaded file
            var propsDialog = new FilePropertiesDialog(loadedFile);
            if (propsDialog.ShowDialog() == DialogResult.OK)
            {
                byte[] newName = Encoding.UTF8.GetBytes(propsDialog.authorName);
                Array.Resize(ref newName, 32);
                loadedFile.Author = newName;
            }

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
            if (singleSelectedRow() != -1)
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
                    else if (cell.ValueType == typeof(string))
                    {
                        cell.Value = "";
                    }
                }
            }

        }

        private void insertNewFrame(int pos, uint count, Frame baseFrame = null)
        {
            baseFrame = baseFrame ?? new Frame();

            if (count > 1)
                stopListEvents();

            for (int i = 0; i < count; i++)
                frameList.Insert(pos, baseFrame);

            if (count > 1)
                continueListEvents();
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

            HashSet<int> selectedRowList = getSelectedRowsFromCells();
            if (selectedRowList.Count > 0)
            {
                var insDialog = new MultiFrameInsertDialog();
                if (insDialog.ShowDialog() == DialogResult.OK)
                {
                    int insertIndex = 0;
                    if (insDialog.InsertFramesAfter)
                        insertIndex = selectedRowList.Max() + 1;
                    else
                        insertIndex = selectedRowList.Min();

                    insertNewFrame(insertIndex, insDialog.FrameCount);
                }
            }

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
                stopListEvents();
                //frameView.RaiseListChangedEvents = false;
                int insertPosition = selectedRowList.Min();
                for (int i = clipboard.Count - 1; i >= 0; i--)
                {
                    frameList.Insert(insertPosition, new Frame(clipboard[i]));
                }
                continueListEvents();
            }
        }

        private void pasteAfterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // paste rows after selection, or at the end of the file
            HashSet<int> selectedRowList = getSelectedRowsFromCells();
            if (selectedRowList.Count > 0 && clipboard.Count > 0)
            {
                stopListEvents();
                int insertPosition = selectedRowList.Max() + 1;
                for (int i = clipboard.Count - 1; i >= 0; i--)
                {
                    frameList.Insert(insertPosition, new Frame(clipboard[i]));
                }
                continueListEvents();
            }
        }

        private void filterComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (frameView != null)
            {
                switch (filterComboBox.SelectedIndex)
                {
                    case 1:
                        frameView.ApplyFilter(delegate (Frame f) { return f.Note.Length > 0; });
                        frameSource.AllowNew = false;
                        break;
                    case 2:
                        Frame defaultFrame = new Frame();
                        frameView.ApplyFilter(delegate (Frame f) { return !(f.Equals(defaultFrame)); });
                        frameSource.AllowNew = false;
                        break;
                    default:
                        frameView.RemoveFilter();
                        frameSource.AllowNew = true;
                        break;
                }
            }
        }

        private void fileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool shouldEnable = (loadedFile != null);

            saveAsToolStripMenuItem.Enabled = shouldEnable;
            saveToolStripMenuItem.Enabled = shouldEnable;
            fileInfoToolStripMenuItem.Enabled = shouldEnable;

        }

        private void frameDataGridView_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            if ((!frameSource.AllowNew || e.RowIndex != grid.RowCount - 1))
            {
                var rowIdx = (e.RowIndex + 1).ToString();
                ObjectView<Frame> frameObj = (ObjectView<Frame>)grid.Rows[e.RowIndex].DataBoundItem;
                var frame = frameObj.Object;
                if (frame != null)
                {
                    int index = frameList.IndexOf(frame);
                    if (index >= 0)
                        rowIdx = (index + 1).ToString();
                }
                else
                    rowIdx = "";

                var centerFormat = new StringFormat()
                {
                    // right alignment might actually make more sense for numbers
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center
                };

                var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth - 3, e.RowBounds.Height);
                e.Graphics.DrawString(rowIdx, this.Font, SystemBrushes.ControlText, headerBounds, centerFormat);
            }
        }
    }
}
