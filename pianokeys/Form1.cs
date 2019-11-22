﻿using System;
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

/*  TODO
    FEATURES:
    Navigate to next frame with specific button values/containing notes
    Copy/Paste working between instances of pianokeys
    Disable controls while no file is loaded (or if no game is specified?)
    Call exit stuff on pressing window X
    Undo/Redo
    
    CLEANUP:
    Deleting multiple frames takes a long time
    Try to condense usage of frameList, frameSource, frameView, and frameDataGridView into fewer private variables.
    Replace stalling moments with progress bar/disabling controls
    Move heavy tasks to worker thread if necessary

    KNOWN BUGS:
    Crash with filter and loading new files?

    NOTES:
    clicking new row when it's partly below the screen adds 10 rows instead. Is this fine or is there a way to change this?
    Lol what if we could "video preview" by running dolphin with the current movie
*/

namespace pianokeys
{
    public partial class Form1 : Form
    {
        private Frame ActiveFrame;
        private List<Frame> frameList; //List containing data associated with the loaded file
        private BindingListView<Frame> frameView; //Provides filters for the frameList
        private BindingSource frameSource; //Shows frameView to window's controls
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

            frameDataGridView.DataSource = frameSource;
            frameNavigator.BindingSource = frameSource;
        }
        
        private Frame getActiveFrame()
        {
            ObjectView<Frame> frameSourceObj = null;
            try
            {
                frameSourceObj = ((ObjectView<Frame>)frameSource.Current);
            }
            catch { }
            if (frameSourceObj != null)
                return frameSourceObj.Object;
            return null;
        }

        private List<Frame> getGridSelectedFrames()
        {
            List<Frame> frames = new List<Frame>();

            var selection = frameDataGridView.SelectedCells;
            HashSet<int> selectedRows = new HashSet<int>();
            foreach (DataGridViewCell selectedThing in selection)
            {
                int index = selectedThing.OwningRow.Index;
                if (index < frameDataGridView.Rows.Count - 1)
                    selectedRows.Add(index);
            }
            
            foreach (int i in selectedRows)
            {
                var gridRow = frameDataGridView.Rows[i];
                var gridItem = (ObjectView<Frame>)gridRow.DataBoundItem;
                if (gridItem != null)
                {
                    frames.Add(gridItem.Object);
                }
            }
            return frames;
        }

        private void frameSource_CurrentChanged(object sender, EventArgs e)
        {
            frameSource.EndEdit();

            Frame currentFrame = getActiveFrame();
            if (currentFrame != null)
            {
                int index = frameList.IndexOf(currentFrame);
                if (index != -1)
                {
                    selectedFrameBox.Text = "Frame " + (index + 1).ToString();
                    activeFrameBindingSource.Add(currentFrame);
                    activeFrameBindingSource.RemoveAt(0);
                    activeFrameBindingSource.EndEdit();
                    activeFrameBindingSource.ResetCurrentItem();
                }
                selectedFrameBox.Enabled = index != -1;
                
                var countVisible = frameDataGridView.DisplayedRowCount(false);
                var firstVisible = frameDataGridView.FirstDisplayedScrollingRowIndex;
                var currentPos = frameSource.Position;
                if (currentPos < firstVisible)
                {
                    frameDataGridView.FirstDisplayedScrollingRowIndex = currentPos;
                }
                else if (currentPos >= firstVisible + countVisible)
                {
                    frameDataGridView.FirstDisplayedScrollingRowIndex = currentPos - countVisible + 1;
                }
            }
            else
            {
                selectedFrameBox.Enabled = false;
            }
        }
        
        private void SliderValueChanged(object sender, EventArgs e)
        {
            activeFrameBindingSource.EndEdit();
            activeFrameBindingSource.ResetCurrentItem();
            frameSource.ResetCurrentItem();
        }

        private void SliderValueChanged(object sender, MouseEventArgs e)
        {
            activeFrameBindingSource.EndEdit();
            frameSource.ResetCurrentItem();
        }

        private void frameDataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            frameDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private HashSet<int> getSelectedFrameIndices()
        {
            var frameIndices = new HashSet<int>();
            var selectedFrames = getGridSelectedFrames();
            foreach (Frame frame in selectedFrames)
            {
                int index = frameList.IndexOf(frame);
                if (index != -1)
                    frameIndices.Add(index);
            }

            return frameIndices;
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
            frameSource.SuspendBinding();
            frameDataGridView.SuspendLayout();
            frameNavigator.SuspendLayout();
        }

        private void continueListEvents()
        {
            frameView.Refresh();
            frameSource.ResumeBinding();
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
                    filterComboBox.SelectedIndex = 0;
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

                    frameDataGridView.Enabled = true;
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
                msg += " at " + DateTime.Now.ToShortTimeString() + ".";

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
            HashSet<int> selectedRowList = getSelectedFrameIndices();

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
            if (getSelectedFrameIndices().Count > 0)
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
            frameView.Refresh();
        }

        private void insertNewFrame(int pos, uint count, Frame baseFrame = null)
        {
            baseFrame = baseFrame ?? new Frame();
            
            for (int i = 0; i < count; i++)
                frameList.Insert(pos, new Frame(baseFrame));

            frameView.Refresh();
        }

        private void insertBeforeMenuItem_Click(object sender, EventArgs e)
        {
            HashSet<int> selectedRowList = getSelectedFrameIndices();
            if (selectedRowList.Count > 0)
                insertNewFrame(selectedRowList.Min(), 1);
        }

        private void insertAfterMenuItem_Click(object sender, EventArgs e)
        {
            HashSet<int> selectedRowList = getSelectedFrameIndices();
            if (selectedRowList.Count > 0)
                insertNewFrame(selectedRowList.Max() + 1, 1);
        }

        private void insertMultipleMenuItem_Click(object sender, EventArgs e)
        {
            // prompt for a number of rows and whether to add them before or
            // after the selected row(s)
            HashSet<int> selectedRowList = getSelectedFrameIndices();
            if (selectedRowList.Count > 0)
            {
                var insDialog = new MultiFrameInsertDialog();
                if (insDialog.ShowDialog() == DialogResult.OK)
                {
                    if (insDialog.InsertFramesAfter)
                        insertNewFrame(selectedRowList.Max() + 1, insDialog.FrameCount);
                    else
                        insertNewFrame(selectedRowList.Min(), insDialog.FrameCount);
                }
            }
        }

        private void deleteMenuItem_Click(object sender, EventArgs e)
        {
            var itemsToDelete = getGridSelectedFrames();
            foreach (Frame f in itemsToDelete)
            {
                frameSource.Remove(f);
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
            HashSet<int> selectedRows = getSelectedFrameIndices();

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
            HashSet<int> selectedRowList = getSelectedFrameIndices();
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
            HashSet<int> selectedRowList = getSelectedFrameIndices();
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
                frameDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
                frameSource.EndEdit();
                var currentFrame = getActiveFrame();

                switch (filterComboBox.SelectedIndex)
                {
                    case 1:
                        //Frame has a note
                        frameView.ApplyFilter(delegate (Frame f) { return f.Note.Length > 0; });
                        frameSource.AllowNew = false;
                        break;
                    case 2:
                        //Input is empty (neutral sticks, no buttons, no pressure)
                        Frame defaultFrame = new Frame();
                        frameView.ApplyFilter(delegate (Frame f) { return !(f.Equals(defaultFrame)); });
                        frameSource.AllowNew = false;
                        break;
                    case 3:
                        //Input has at least 1 button pressed
                        frameView.ApplyFilter(delegate (Frame f) {
                            return (
                                f.A || f.B || f.X || f.Y ||
                                f.Z || f.L || f.R || f.ST ||
                                f.DD || f.DL || f.DR || f.DU
                            );
                        });
                        frameSource.AllowNew = false;
                        break;
                    default:
                        frameView.RemoveFilter();
                        frameSource.AllowNew = true;
                        break;
                }

                var newPos = frameSource.List.IndexOf(currentFrame);
                if (newPos >= 0)
                    frameSource.Position = newPos;
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

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult savePromptResult = DialogResult.No;
            if (loadedFile != null)
                savePromptResult = MessageBox.Show("Save your work before exiting?",
                    "Exit", MessageBoxButtons.YesNoCancel);

            switch (savePromptResult)
            {
                case DialogResult.Yes:
                    saveFile(loadedFile.FilePath);
                    Application.Exit();
                    break;
                case DialogResult.No:
                    Application.Exit();
                    break;
                case DialogResult.Cancel:
                    break;
            }
        }
    }
}
