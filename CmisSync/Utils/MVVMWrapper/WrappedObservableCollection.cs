using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections;
using System.ComponentModel;

namespace CmisSync.Utils.MVVMWrapper
{
    /// <summary>
    /// Represents an ObservableCollection of items of type TWrapped that wraps an ObservableCollection of items of type TSource
    /// </summary>
    public abstract class WrappedObservableCollection<TSource, TWrapped> : ObservableCollection<TWrapped>
        where TWrapped : IItemWrapper<TSource>
    {
        /// <summary>
        /// Returns the item in this collection that is the wrapped version of the supplied item
        /// </summary>
        /// <param name="item">The item for which the matching wrapped item is sought</param>
        /// <returns>The item in this collection that is the wrapped version of the supplied item</returns>
        /// <remarks>If there is no corresponding wrapped item for the supplied item, an exception is thrown</remarks>
        public virtual TWrapped GetWrapped(TSource sourceItem)
        {
            return this.FirstOrDefault(item => item.IsItemWrapper(sourceItem));
        }

        public WrappedObservableCollection()
        {

        }

        private IEnumerable<TSource> _sourceCollection;

        /// <summary>
        /// Gets and Sets the SourceCollection (the collection we are wrapping)
        /// </summary>
        public IEnumerable<TSource> SourceCollection
        {
            get { return _sourceCollection; }
            set
            {
                if (_sourceCollection == null || _sourceCollection.Equals(value) == false)
                {

                    //unsubscribe
                    if (_sourceCollection != null && _sourceCollection is INotifyCollectionChanged)
                    {
                        ((INotifyCollectionChanged)_sourceCollection).CollectionChanged -= new NotifyCollectionChangedEventHandler(SourceCollectionChanged);
                    }

                    //set the value;
                    _sourceCollection = value;

                    //clear all items in this collection
                    this.Clear();

                    //subscribe
                    if (_sourceCollection != null && _sourceCollection is INotifyCollectionChanged)
                    {
                        ((INotifyCollectionChanged)_sourceCollection).CollectionChanged += new NotifyCollectionChangedEventHandler(SourceCollectionChanged);
                        //rebuild the collection
                        this.CopyItems();
                    }
                }
            }
        }

        private void CopyItems()
        {
            var x = from child in this.SourceCollection
                    select WrapItem(child);
            foreach (var y in x)
            {
                OnItemConstruction(y);
                base.Add(y);
                OnItemConstructed(y);
            }
        }

        protected abstract TWrapped WrapItem(TSource child);

        /// <summary>
        /// Method is called before the wrapped item is added to the collection
        /// </summary>
        /// <param name="itemConstructed">item that is about to be added to this collection</param>
        protected virtual void OnItemConstruction(TWrapped itemConstructed) { }

        /// <summary>
        /// Method is called after the wrapped item is added to the collection
        /// </summary>
        /// <param name="itemConstructed">item that is about to be added to this collection</param>
        protected virtual void OnItemConstructed(TWrapped itemConstructed) { }

        /// <summary>
        /// Method is called after the wrapped item is removed from the collection
        /// </summary>
        /// <param name="itemRemoved"></param>
        protected virtual void OnItemRemoved(TWrapped itemRemoved) { }


        private void SourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                base.Move(e.OldStartingIndex, e.NewStartingIndex);
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                int startingIndex = e.NewStartingIndex;
                foreach (TSource item in e.NewItems)
                {
                    TWrapped wrappedItem = WrapItem(item);
                    OnItemConstruction(wrappedItem);
                    base.Insert(startingIndex++, wrappedItem);
                    OnItemConstructed(wrappedItem);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TSource item in e.OldItems)
                {
                    TWrapped itemToRemove = GetWrapped(item);
                    base.Remove(itemToRemove);
                    OnItemRemoved(itemToRemove);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                base.Clear();
                CopyItems();
            }
        }
    }
}
