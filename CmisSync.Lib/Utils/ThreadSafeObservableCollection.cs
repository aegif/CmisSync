using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows;

namespace System.Collections.ObjectModel
{
    public class ThreadSafeObservableCollection<T> : ObservableCollection<T>
    {
        private static void Dispatch(Action action) {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Input, action);
        }

        protected override void ClearItems()
        {
            Dispatch(() => { base.ClearItems(); });
        }

        protected override void InsertItem(int index, T item)
        {
            Dispatch(() => { base.InsertItem(index, item); });
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            Dispatch(() => { base.MoveItem(oldIndex, newIndex); });
        }

        protected override void RemoveItem(int index)
        {
            Dispatch(() => { base.RemoveItem(index); });
        }

        protected override void SetItem(int index, T item)
        {
            Dispatch(() => { base.SetItem(index, item); });
        }
    }
}
