using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    Use ellipses with bottom bar messages
    Move heavy tasks to worker thread if necessary

    KNOWN BUGS:
    
    Top bar throws exceptions when the list has one frame and it hasn't been made "official" by the grid viewer yet.
        (This is only possible to find if the user deletes their entire frame list. Is it worth worrying about?)
    


    NOTES:
    clicking new row when it's partly below the screen adds 10 rows instead. Is this fine or is there a way to change this?
    If we locate dolphin.exe, we can run "dolphin -m filename" to view the current movie
*/

namespace pianokeys
{
    public partial class Form1 : Form
    {
        private Frame ActiveFrame;
        private ObservableCollection<Frame> frameList; //List containing data associated with the loaded file
        private BindingListView<Frame> frameView; //Provides filters for the frameList
        private BindingSource frameSource; //Shows frameView to window's controls
        private DTM loadedFile;

        private Stack<ActionStackItem> undoStack;
        private Stack<ActionStackItem> redoStack;
        private bool usingUndoStack;


        private List<Frame> clipboard;

        public Form1()
        {
            InitializeComponent();
            filterComboBox.SelectedIndex = 0;
            ActiveFrame = new Frame();
            var t = new Frame();
            activeFrameBindingSource.Add(t);
            frameList = new ObservableCollection<Frame>();
            frameView = new BindingListView<Frame>(frameList);
            frameSource = new BindingSource(frameView, null);
            loadedFile = null;
            clipboard = new List<Frame>();
            frameSource.CurrentChanged += frameSource_CurrentChanged;

            frameDataGridView.DataSource = frameSource;
            frameNavigator.BindingSource = frameSource;

            frameList.CollectionChanged += FrameList_CollectionChanged;

            usingUndoStack = false;
            undoStack = new Stack<ActionStackItem>();
            redoStack = new Stack<ActionStackItem>();
        }

        #region List Accessing

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

        #endregion

        #region List Manipulation
        
        private void insertNewFrame(int pos, uint count, Frame baseFrame = null)
        {
            baseFrame = baseFrame ?? new Frame();

            for (int i = 0; i < count; i++)
                frameList.Insert(pos + i, new Frame(baseFrame));
        }

        #endregion

        #region List IO

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

        #endregion

        #region UndoRedo

        private void FrameList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //Check the top stack item. If it's the same type of change & occurred within the last second,
            // combine the changes; otherwise make copies of all the old
            if (!usingUndoStack)
            {
                redoStack.Clear();
                frameDataGridView.EndEdit();
                frameSource.EndEdit();
                frameView.EndNew(e.NewStartingIndex);

                var changeTime = DateTime.UtcNow;
                List<Frame> newItems = new List<Frame>();
                List<int> newIndices = new List<int>();

                //TODO Specify what to add to the undo stack
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        for (int i = 0; i < e.NewItems.Count; i++)
                        {
                            newItems.Add((Frame)e.NewItems[i]);
                            newIndices.Add(e.NewStartingIndex + i);
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        for (int i = 0; i < e.OldItems.Count; i++)
                        {
                            newItems.Add((Frame)e.OldItems[i]);
                            newIndices.Add(e.OldStartingIndex + i);
                        }
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        throw new NotImplementedException("Did't do replace");
                    case NotifyCollectionChangedAction.Move:
                        throw new NotImplementedException("Did't do move");
                    case NotifyCollectionChangedAction.Reset:
                        undoStack.Clear();
                        redoStack.Clear();
                        return;
                }
                commitToUndoStack(new ActionStackItem((ActionType)(int)e.Action, newItems, newIndices));
                frameView.Refresh();
            }

        }

        private void commitToUndoStack(ActionStackItem newItem)
        {
            if (usingUndoStack)
                return;

            const double stackMergeTimeLimitSeconds = 1;
            ActionStackItem undoStackTop = (undoStack.Count > 0 ? undoStack.Peek() : null);
            bool useNew = true;

            if (undoStackTop != null &&
                (newItem.actionTime <= undoStackTop.actionTime.AddSeconds(stackMergeTimeLimitSeconds)))
            {
                if (undoStackTop.Type == newItem.Type)
                {
                    undoStackTop.extendAction(newItem);
                    useNew = false;
                }
                else if ((undoStackTop.Type == ActionType.Add) &&
                    (newItem.Type == ActionType.Edit))
                {
                    foreach (var pair in newItem.ChangedFrames)
                    {
                        undoStackTop.ChangedFrames[pair.Key] = pair.Value;
                    }
                    useNew = false;
                }
            }
            if (useNew)
                undoStack.Push(newItem);
        }

        private void Undo()
        {
            if (undoStack.Count > 0)
            {
                stopListEvents();
                usingUndoStack = true;
                var action = undoStack.Pop();
                var keys = action.ChangedFrames.Keys.ToList();
                keys.Sort();
                switch (action.Type)
                {
                    case ActionType.Add:
                        keys.Reverse();
                        foreach (int pos in keys)
                        {
                            if (frameList.Count > pos)
                            {
                                action.ChangedFrames[pos] = frameList[pos];
                                frameList.RemoveAt(pos);
                            }
                        }
                        break;
                    case ActionType.Remove:
                        foreach (int pos in keys)
                        {
                            frameList.Insert(pos, new Frame(action.ChangedFrames[pos]));
                        }
                        break;
                    case ActionType.Replace:
                        throw new NotImplementedException("No Undo Replace");
                    case ActionType.Move:
                        throw new NotImplementedException("No Undo Move");
                    case ActionType.Reset:
                        throw new NotImplementedException("No Undo Reset");
                    case ActionType.Edit:
                        foreach (var pos in keys)
                        {
                            var currentFrameCopy = new Frame(frameList[pos]);
                            frameList[pos].copyDataFromFrame(action.ChangedFrames[pos]);
                            action.ChangedFrames[pos] = currentFrameCopy;
                        }
                        break;
                }
                redoStack.Push(action);
                continueListEvents();
                usingUndoStack = false;
            }
        }

        private void Redo()
        {
            if (redoStack.Count > 0)
            {
                stopListEvents();
                usingUndoStack = true;
                var action = redoStack.Pop();
                var keys = action.ChangedFrames.Keys.ToList();
                keys.Sort();
                switch (action.Type)
                {
                    case ActionType.Add:
                        foreach (int pos in keys)
                        {
                            frameList.Insert(pos, new Frame(action.ChangedFrames[pos]));
                        }
                        break;
                    case ActionType.Remove:
                        keys.Reverse();
                        foreach (int pos in keys)
                        {
                            if (frameList.Count > pos)
                            {
                                action.ChangedFrames[pos] = frameList[pos];
                                frameList.RemoveAt(pos);
                            }
                        }
                        break;
                    case ActionType.Replace:
                        throw new NotImplementedException("No Redo Replace");
                    case ActionType.Move:
                        throw new NotImplementedException("No Redo Move");
                    case ActionType.Reset:
                        throw new NotImplementedException("No Redo Reset");
                    case ActionType.Edit:
                        foreach (var pos in keys)
                        {
                            var currentFrameCopy = new Frame(frameList[pos]);
                            frameList[pos].copyDataFromFrame(action.ChangedFrames[pos]);
                            action.ChangedFrames[pos] = currentFrameCopy;
                        }
                        break;
                }
                undoStack.Push(action);
                continueListEvents();
                usingUndoStack = false;
            }
        }

        #endregion
        
        #region Active Frame Viewer events

        private void SliderValueChanged(object sender, EventArgs e)
        {
            if (!usingUndoStack)
            {
                Frame f = getActiveFrame();
                commitToUndoStack(new ActionStackItem(ActionType.Edit,
                    new List<Frame>() { f },
                    new List<int>() { frameList.IndexOf(f) }
                    ));
            }
            activeFrameBindingSource.EndEdit();
            activeFrameBindingSource.ResetCurrentItem();
            //TODO if no valid current item, make one
            frameSource.ResetCurrentItem();
        }

        private void CheckboxValueChanged(object sender, MouseEventArgs e)
        {
            if (!usingUndoStack)
            {
                Frame f = getActiveFrame();
                commitToUndoStack(new ActionStackItem(ActionType.Edit,
                    new List<Frame>() { f },
                    new List<int>() { frameList.IndexOf(f) }
                    ));
            }
            activeFrameBindingSource.EndEdit();
            //TODO if no valid current item, make one
            frameSource.ResetCurrentItem();
        }

        #endregion

        #region Data Grid View events

        private void frameDataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            //TODO Don't call if nothing changes from committing
            var initialFrame = getActiveFrame();
            if (initialFrame != null)
            {
                int index = frameList.IndexOf(initialFrame);
                if (index != -1)
                {
                    ActiveFrame = initialFrame;
                    initialFrame = new Frame(ActiveFrame);
                    frameDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    //Active Frame might have changed
                    if (!ActiveFrame.Equals(initialFrame))
                    {
                        commitToUndoStack(new ActionStackItem(ActionType.Edit,
                           new List<Frame>() { initialFrame },
                           new List<int>() { index }
                           ));
                    }
                }
            }
        }

        private void frameDataGridView_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            Frame currentFrame = getActiveFrame();
            if (currentFrame != null)
                ActiveFrame = new Frame(currentFrame);
        }

        private void frameDataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            Frame newActiveFrame = getActiveFrame();
            if (newActiveFrame != null && !newActiveFrame.Equals(ActiveFrame))
            {
                commitToUndoStack(new ActionStackItem(ActionType.Edit,
                    new List<Frame>() { ActiveFrame },
                    new List<int>() { frameList.IndexOf(newActiveFrame) }
                    ));
                frameDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
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

        #endregion

        #region Menu Button events

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
                    usingUndoStack = true;
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
                    usingUndoStack = false;
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

        private void undoToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //TODO Get old item state from change stack, make changes
            //note: maybe, maybe not, depending on undo-ability of gridview's built-in adds/deletes
            Undo();
        }

        private void redoToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //TODO get item state from redo stack, make changes
            //note: maybe, maybe not, depending on undo-ability of gridview's built-in adds/deletes
            Redo();
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

        private void fileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool shouldEnable = (loadedFile != null);

            saveAsToolStripMenuItem.Enabled = shouldEnable;
            saveToolStripMenuItem.Enabled = shouldEnable;
            fileInfoToolStripMenuItem.Enabled = shouldEnable;

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
        
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult savePromptResult = DialogResult.No;
            if (loadedFile != null)
                savePromptResult = MessageBox.Show("Save your work before exiting?",
                    "Exit", MessageBoxButtons.YesNoCancel);

            switch (savePromptResult)
            {
                case DialogResult.Yes:
                    saveFile(loadedFile.FilePath);
                    break;
                case DialogResult.No:
                    break;
                case DialogResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }

        #endregion

        #region List Edit Menu Events

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

        #endregion

        #region View Functions

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

        #endregion
        
    }
}
