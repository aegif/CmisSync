using CmisSync.Lib.Sync;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmisSync
{
    public class SyncronizersCollection : ThreadSafeObservableCollection<SyncFolderSyncronizer>
    {
        public SyncronizersCollection()
            : base()
        {
            this.CollectionChanged += collectionChanged;
        }

        private void collectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (SyncFolderSyncronizer item in e.OldItems)
                {
                    //Removed items
                    item.PropertyChanged -= entityViewModelPropertyChanged;
                    if (item.Status == SyncStatus.Syncing) {
                        SyncingSyncronizersCount--;
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (SyncFolderSyncronizer item in e.NewItems)
                {
                    //Added items
                    item.PropertyChanged += entityViewModelPropertyChanged;
                    if (item.Status == SyncStatus.Syncing)
                    {
                        SyncingSyncronizersCount++;
                    }
                }
            }
        }

        private void entityViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if ("Status".Equals(e.PropertyName))
            {
                switch (((SyncFolderSyncronizer)sender).Status)
                {
                    case SyncStatus.Syncing:
                        SyncingSyncronizersCount++;
                        break;
                    default:
                        SyncingSyncronizersCount--;
                        break;
                }
            }
        }

        private int _syncingSyncronizersCount = 0;
        private int SyncingSyncronizersCount
        {
            get
            {
                return _syncingSyncronizersCount;
            }
            set
            {
                if (_syncingSyncronizersCount != value)
                {
                    bool isSyncing_OldValue = IsSyncing;
                    _syncingSyncronizersCount = value;
                    OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("SyncingSyncronizersCount"));
                    
                    if (isSyncing_OldValue != IsSyncing)
                    {
                        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("IsSyncing"));
                    }
                }
            }
        }

        public bool IsSyncing
        {
            get { return _syncingSyncronizersCount > 0; }            
        }
    }
}
