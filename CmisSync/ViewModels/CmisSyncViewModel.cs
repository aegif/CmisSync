using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using CmisSync.Lib;
using CmisSync.Lib.Sync;
using CmisSync.Utils;

namespace CmisSync.ViewModels
{
    public class ControllerViewModel : ViewModelBase
    {
        public enum WorkState
        {
            RUNNING, IDLE, ERROR
        }

        private WorkState _workState;
        private String _workStateText = "";

        public RelayCommand exitCommand { get; internal set; }
        public RelayCommand newRepositoryCommand { get; internal set; }
        
        public WorkState CurrentWorkState { get { return _workState; } set { _workState = value; } }
        public String CurrentWorkStateText { get { return _workStateText; } set { _workStateText = value; } }

        public ControllerViewModel(Controller controller) : base(controller)
        {
            exitCommand = new RelayCommand(exit);
            newRepositoryCommand = new RelayCommand(newRepository);

            _readOnlySyncFolders = new SyncFolderSyncronizerViewModelCollection(controller, controller.SyncFolders);
        }

        #region IActivityListener

        public void ActivityStarted()
        {
            CurrentWorkState = WorkState.RUNNING;
        }

        public void ActivityStopped()
        {
            CurrentWorkState = WorkState.IDLE;
        }

        public void ActivityError(Config.SyncConfig.SyncFolder repo, Exception error)
        {
            CurrentWorkState = WorkState.ERROR;
        }

        #endregion

        public void exit()
        {
            Controller.ShutdownApplication();
        }

        public void newRepository()
        {
            Controller.createAndShowNewSyncFolder();
        }

        protected override string validate(string columnName)
        {
            return String.Empty;
        }

        #region SyncFolders

        public class SyncFolderSyncronizerViewModelCollection : Utils.MVVMWrapper.WrappedObservableCollection<SyncFolderSyncronizer, ViewModels.SyncFolderSyncronizerViewModel>
        {
            private Controller controller;
            public SyncFolderSyncronizerViewModelCollection(Controller controller, ObservableCollection<SyncFolderSyncronizer> _syncFolders)
                : base()
            {
                this.controller = controller;
                this.SourceCollection = _syncFolders;
            }

            protected override ViewModels.SyncFolderSyncronizerViewModel WrapItem(SyncFolderSyncronizer source)
            {
                return new ViewModels.SyncFolderSyncronizerViewModel(controller, source);
            }
        }

        private SyncFolderSyncronizerViewModelCollection _readOnlySyncFolders;
        public SyncFolderSyncronizerViewModelCollection SyncFolders { get { return _readOnlySyncFolders; } }

        #endregion

    }
}
