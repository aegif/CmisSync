using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync
{
    public class EventsObservableCollection : ThreadSafeObservableCollection<SyncronizerEvent>
    {
        private ObservableDictionary<EventLevel, int> eventsTypeCount = new ObservableDictionary<EventLevel, int>();
        public ObservableDictionary<EventLevel, int> EventsTypeCount { get; internal set; }

        private List<SyncronizerEvent> markedToBeRemoved = new List<SyncronizerEvent>();

        public EventsObservableCollection() {
            EventsTypeCount = eventsTypeCount;
            ClearItems();
        }

        public void MarkAllToBeRemoved() {
            markedToBeRemoved.Clear();
            markedToBeRemoved.AddRange(this);
        }

        public void RemoveAllMarked() {
            foreach (SyncronizerEvent e in markedToBeRemoved)
            {
                this.Remove(e);
            }
        }

        //----overrides----

        protected override void InsertItem(int index, SyncronizerEvent item)
        {
            int oldIndex = this.IndexOf(item);
            if (oldIndex >= 0) {
                markedToBeRemoved.Remove(item);
                this.SetItem(oldIndex, item);
                return;
            }

            base.InsertItem(index, item);
            eventsTypeCount[item.Level]++;
        }

        protected override void RemoveItem(int index)
        {
            eventsTypeCount[this.Items[index].Level]--;
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            eventsTypeCount.Clear();
            base.ClearItems();
            foreach (EventLevel level in Enum.GetValues(typeof(EventLevel)))
            {
                eventsTypeCount[level] = 0;
            }
        }

        protected override void SetItem(int index, SyncronizerEvent item)
        {
            eventsTypeCount[this.Items[index].Level]--;
            base.SetItem(index, item);
            eventsTypeCount[item.Level]++;
        }
    }
}
