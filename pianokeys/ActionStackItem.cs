using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pianokeys
{
    class ActionStackItem
    {
        public DateTime actionTime { get; private set; }
        public ActionType Type { get; private set; }
        //Dictionary where int = position, Frame is the changed frame
        public Dictionary<int, Frame> ChangedFrames { get; set; }

        public ActionStackItem(ActionType actionType, List<Frame> editedFrames, List<int> sourceListIndices)
        {
            actionTime = DateTime.UtcNow;
            Type = actionType;
            ChangedFrames = new Dictionary<int, Frame>();

            extendAction(editedFrames, sourceListIndices);

        }

        public void extendAction(List<Frame> editedFrames, List<int> sourceListIndices)
        {
            #if DEBUG
            if (editedFrames.Count != sourceListIndices.Count)
                throw new FormatException("Passed lists must be of the same length.");
            #endif

            for (int i = 0; i < editedFrames.Count; i++)
            {
                ChangedFrames[sourceListIndices[i]] = new Frame(editedFrames[i]);
            }
        }
        
        public void extendAction(ActionStackItem newItem)
        {
            if (this.Type != ActionType.Edit)
            {
                foreach (var fc in newItem.ChangedFrames)
                {
                    ChangedFrames[fc.Key] = fc.Value;
                }
            }
        }
    }

    enum ActionType
    {
        Add = NotifyCollectionChangedAction.Add,
        Remove = NotifyCollectionChangedAction.Remove,
        Replace = NotifyCollectionChangedAction.Replace,

        Move = NotifyCollectionChangedAction.Move,
        Reset = NotifyCollectionChangedAction.Reset,
        Edit = 5
    }
}
