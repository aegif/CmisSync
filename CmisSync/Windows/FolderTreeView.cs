using CmisSync.Lib.Cmis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows;
using System.Windows.Input;

namespace CmisSync
{
    namespace CmisTree
    {

        /// <summary>
        /// Cmis Repository Tree.
        /// </summary>
        public class CmisRepo : INotifyPropertyChanged
        {
            private BackgroundWorker worker = new BackgroundWorker();
            private BackgroundWorker folderworker = new BackgroundWorker();
            private ObservableCollection<Folder> _folder = new ObservableCollection<Folder>();
            private string name;

            /// <summary>
            /// Repository name.
            /// </summary>
            public string Name { get { return name; } set { SetField(ref name, value, "Name"); } }

            /// <summary>
            /// Repository ID.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Repository path.
            /// </summary>
            public string Path { get { return "/"; } }


            private string username;
            private string password;
            private string address;

            /// <summary>
            /// Address.
            /// </summary>
            public string Address { get { return address; } }

            private Folder currentWorkingObject;
            private Queue<Folder> queue = new Queue<Folder>();

            private LoadingStatus status = LoadingStatus.START;

            /// <summary>
            /// Status.
            /// </summary>
            public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
            private bool threestate = false;

            /// <summary>
            /// Three state.
            /// </summary>
            public bool ThreeState { get { return threestate; } set { SetField(ref threestate, value, "ThreeState"); } }

            /// <summary>
            /// Tooltip.
            /// </summary>
            public string ToolTip { get { return "URL: \"" + address + "\"\r\nRepository ID: \"" + Id + "\""; } }
            private bool selected = false;

            /// <summary>
            /// Selected.
            /// </summary>
            public bool Selected
            {
                get { return selected; }
                set
                {
                    if (SetField(ref selected, value, "Selected"))
                    {
                        foreach (Folder child in Folder)
                        {
                            child.Enabled = selected;
                        }
                    }
                }
            }
            private bool automaticSyncAllNewSubfolder = true;

            /// <summary>
            /// Sync All.
            /// </summary>
            public bool SyncAllSubFolder
            {
                get { return automaticSyncAllNewSubfolder; }
                set
                {
                    if (SetField(ref automaticSyncAllNewSubfolder, value, "SyncAllSubFolder"))
                    {
                        if (automaticSyncAllNewSubfolder)
                        {
                            foreach (Folder subfolder in Folder)
                            {
                                if (subfolder.Selected == false)
                                {
                                    subfolder.IsIgnored = true;
                                }
                            }
                        }
                        else
                        {
                            foreach (Folder subfolder in Folder)
                            {
                                if (subfolder.IsIgnored)
                                {
                                    subfolder.IsIgnored = false;
                                    subfolder.Selected = false;
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Folder.
            /// </summary>
            public ObservableCollection<Folder> Folder
            {
                get { return _folder; }
            }

            /// <summary>
            /// Constructor.
            /// </summary>
            public CmisRepo(string username, string password, string address)
            {
                this.username = username;
                this.password = password;
                this.address = address;
                worker.DoWork += new DoWorkEventHandler(DoWork);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Finished);
                worker.WorkerSupportsCancellation = true;
                folderworker.DoWork += new DoWorkEventHandler(SubFolderWork);
                folderworker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(SubfolderFinished);
                folderworker.WorkerSupportsCancellation = true;
            }

            /// <summary>
            /// Loading folder.
            /// </summary>
            public void LoadingSubfolderAsync()
            {
                if (status == LoadingStatus.START)
                {
                    status = LoadingStatus.LOADING;
                    this.worker.RunWorkerAsync();
                }
            }

            /// <summary>
            /// Cancel loading.
            /// </summary>
            public void cancelLoadingAsync()
            {
                this.worker.CancelAsync();
                this.folderworker.CancelAsync();
            }

            private void DoWork(object sender, DoWorkEventArgs e)
            {
                BackgroundWorker worker = sender as BackgroundWorker;
                try
                {
                    e.Result = CmisUtils.GetSubfolderTree(Id, Path, address, username, password, -1);
                }
                catch (Exception)
                {
                    e.Result = CmisUtils.GetSubfolders(Id, Path, address, username, password);
                }
                if (worker.CancellationPending)
                    e.Cancel = true;
            }

            private void SubFolderWork(object sender, DoWorkEventArgs e)
            {
                BackgroundWorker worker = sender as BackgroundWorker;
                Folder f = queue.Dequeue();
                currentWorkingObject = f;
                currentWorkingObject.Status = LoadingStatus.LOADING;
                e.Result = CmisUtils.GetSubfolders(Id, f.Path, address, username, password);
                if (worker.CancellationPending)
                    e.Cancel = true;
            }

            private void SubfolderFinished(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    currentWorkingObject.Status = LoadingStatus.REQUEST_FAILURE;
                }
                else if (e.Cancelled)
                {
                    currentWorkingObject.Status = LoadingStatus.ABORTED;
                }
                else
                {
                    string[] subfolder = (string[])e.Result;
                    foreach (string f in subfolder)
                    {
                        Folder folder = new Folder()
                            {
                                Repo = this,
                                Path = f,
                                Name = f.Split('/')[f.Split('/').Length - 1],
                                Parent = currentWorkingObject,
                                Type = CmisTree.Folder.FolderType.REMOTE,
                                IsIgnored = currentWorkingObject.IsIgnored,
                                Selected = currentWorkingObject.Selected,
                                Enabled = currentWorkingObject.Enabled
                            };
                        currentWorkingObject.SubFolder.Add(folder);
                        this.queue.Enqueue(folder);
                    }
                    currentWorkingObject.Status = LoadingStatus.DONE;
                }
                if (queue.Count > 0 && !e.Cancelled && !folderworker.CancellationPending)
                {
                    folderworker.RunWorkerAsync();
                }
            }

            private void Finished(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    Status = LoadingStatus.REQUEST_FAILURE;
                }
                else if (e.Cancelled)
                {
                    Status = LoadingStatus.ABORTED;
                }
                else
                {
                    if (e.Result is CmisSync.Lib.Cmis.CmisUtils.FolderTree)
                    {
                        CmisUtils.FolderTree repotree = e.Result as CmisUtils.FolderTree;
                        foreach (CmisUtils.FolderTree repofolder in repotree.children)
                        {
                            Folder folder = new Folder(repofolder, this);
                            this.Folder.Add(folder);
                        }
                        Status = LoadingStatus.DONE;
                    }
                    else
                    {
                        if (e.Result == null) return;
                        string[] subfolder = (string[])e.Result;
                        foreach (string f in subfolder)
                        {
                            Folder folder = new Folder()
                            {
                                Repo = this,
                                Path = f,
                                Name = f.Split('/')[f.Split('/').Length - 1],
                                Parent = this,
                                Type = CmisTree.Folder.FolderType.REMOTE,
                                Enabled = this.selected
                            };
                            this.Folder.Add(folder);
                            this.queue.Enqueue(folder);
                        }
                        Status = LoadingStatus.DONE;
                        if (this.queue.Count > 0 && !this.worker.CancellationPending)
                        {
                            this.folderworker.RunWorkerAsync();
                        }
                    }
                }
            }


            /// <summary>
            /// Boiler plate.
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            /// Property changed.
            /// </summary>
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }

            /// <summary>
            /// Set field.
            /// </summary>
            protected bool SetField<T>(ref T field, T value, string propertyName)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }


        /// <summary>
        /// Folder.
        /// </summary>
        public class Folder : INotifyPropertyChanged
        {

            /// <summary>
            /// Ignore toggle.
            /// </summary>
            public class IgnoreToggleCommand : ICommand
            {
                /// <summary>
                /// Can execute changed.
                /// </summary>
                public event EventHandler CanExecuteChanged;

                /// <summary>
                /// Can execute.
                /// </summary>
                public bool CanExecute(Object parameter)
                {
                    Folder f = parameter as Folder;
                    if (f != null)
                        return true;
                    else
                        return false;
                }

                /// <summary>
                /// Execute.
                /// </summary>
                public void Execute(Object parameter)
                {
                    Folder f = parameter as Folder;
                    if (f != null)
                        f.IsIgnored = !f.ignored;
                }
            }


            /// <summary>
            /// Constructor.
            /// </summary>
            public Folder(CmisUtils.FolderTree tree, CmisRepo repo)
            {
                this.Path = tree.path;
                this.Repo = repo;
                this.Name = tree.Name;
                this.Type = FolderType.REMOTE;
                this.Status = LoadingStatus.DONE;
                this.Enabled = repo.Selected;
                foreach (CmisUtils.FolderTree t in tree.children)
                {
                    this.SubFolder.Add(new Folder(t, Repo) { Parent = this });
                }
            }

            /// <summary>
            /// Folder.
            /// </summary>
            public Folder() { }

            private CmisRepo repo;

            /// <summary>
            /// Parent.
            /// </summary>
            public object Parent { get; set; }

            /// <summary>
            /// Repository.
            /// </summary>
            public CmisRepo Repo { get { return repo; } set { SetField(ref repo, value, "Repo"); } }
            private bool threestates = false;

            /// <summary>
            /// Three states.
            /// </summary>
            public bool ThreeStates { get { return threestates; } set { SetField(ref threestates, value, "ThreeStates"); } }
            private bool? selected = true;

            /// <summary>
            /// Selected.
            /// </summary>
            public bool? Selected
            {
                get { return selected; }
                set
                {
                    if (SetField(ref selected, value, "Selected"))
                    {
                        if (selected == null)
                        {
                            this.ThreeStates = true;
                        }
                        else if (selected == true)
                        {
                            this.IsIgnored = false;
                            this.ThreeStates = false;
                            foreach (Folder subfolder in SubFolder)
                            {
                                subfolder.Selected = true;
                            }
                            Folder p = this.Parent as Folder;
                            while (p != null)
                            {
                                if (p.Selected == null || p.Selected == false)
                                {
                                    bool allSelected = true;
                                    foreach (Folder childOfParent in p.SubFolder)
                                    {
                                        if (childOfParent.selected != true)
                                        {
                                            allSelected = false;
                                            break;
                                        }
                                    }
                                    if (allSelected)
                                    {
                                        p.Selected = true;
                                    }
                                    else if (p.SubFolder.Count > 1)
                                    {
                                        p.ThreeStates = true;
                                        p.Selected = null;
                                        p.IsIgnored = false;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                p = p.Parent as Folder;
                            }
                        }
                        else
                        {
                            this.ThreeStates = false;
                            Folder p = this.Parent as Folder;
                            bool found = false;
                            while (p != null)
                            {
                                if (p.Selected == true)
                                {
                                    this.IsIgnored = true;
                                    found = true;
                                    p.Selected = null;
                                }
                                p = p.Parent as Folder;
                            }
                            if (!found)
                            {
                                if (this.Repo.SyncAllSubFolder)
                                {
                                    this.IsIgnored = true;
                                }
                            }
                        }
                    }
                }
            }
            private string name;

            /// <summary>
            /// Name.
            /// </summary>
            public string Name { get { return name; } set { SetField(ref name, value, "Name"); } }
            private string path;

            /// <summary>
            /// Path.
            /// </summary>
            public string Path { get { return path; } set { SetField(ref path, value, "Path"); } }
            private bool ignored = false;

            /// <summary>
            /// Is ignored.
            /// </summary>
            public bool IsIgnored
            {
                get { return ignored; }
                set
                {
                    if (SetField(ref ignored, value, "IsIgnored"))
                    {
                        if (ignored)
                        {
                            foreach (Folder child in SubFolder)
                            {
                                child.Selected = false;
                            }
                        }
                    }
                }
            }
            private bool enabled = true;

            /// <summary>
            /// Enabled.
            /// </summary>
            public bool Enabled
            {
                get { return enabled; }
                set
                {
                    if (SetField(ref enabled, value, "Enabled"))
                    {
                        foreach (Folder child in SubFolder)
                        {
                            child.Enabled = enabled;
                        }
                    }
                }
            }
            private LoadingStatus status = LoadingStatus.START;

            /// <summary>
            /// Status.
            /// </summary>
            public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
            private ObservableCollection<Folder> _subfolder = new ObservableCollection<Folder>();

            /// <summary>
            /// Sub folder.
            /// </summary>
            public ObservableCollection<Folder> SubFolder { get { return _subfolder; } }
            private FolderType folderType = FolderType.REMOTE;

            /// <summary>
            /// Type.
            /// </summary>
            public FolderType Type { get { return folderType; } set { SetField(ref folderType, value, "Type"); } }


            /// <summary>
            /// Folder type.
            /// </summary>
            public enum FolderType
            {

                /// <summary>
                /// Local.
                /// </summary>
                LOCAL,


                /// <summary>
                /// Remote.
                /// </summary>
                REMOTE,

                /// <summary>
                /// Both.
                /// </summary>
                BOTH
            }


            /// <summary>
            /// Property changed.
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            /// On property changed.
            /// </summary>
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }

            /// <summary>
            /// Set field.
            /// </summary>
            protected bool SetField<T>(ref T field, T value, string propertyName)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }

        /// <summary>
        /// Loading status.
        /// </summary>
        public enum LoadingStatus
        {

            /// <summary>
            /// START.
            /// </summary>
            START,

            /// <summary>
            /// LOADING.
            /// </summary>
            LOADING,

            /// <summary>
            /// ABORTED.
            /// </summary>
            ABORTED,

            /// <summary>
            /// REQUEST_FAILURE.
            /// </summary>
            REQUEST_FAILURE,

            /// <summary>
            /// DONE.
            /// </summary>
            DONE
        }

        /// <summary>
        /// StatusItem.
        /// </summary>
        public class StatusItem : INotifyPropertyChanged
        {
            private LoadingStatus status = LoadingStatus.LOADING;
            /// <summary>
            /// Status.
            /// </summary>
            public LoadingStatus Status { get { return status; } set { if (SetField(ref status, value, "Status")) OnPropertyChanged("Message"); } }
            /// <summary>
            /// Message.
            /// </summary>
            public string Message { get { return CmisSync.Properties_Resources.ResourceManager.GetString("LoadingStatus." + status.ToString(), CultureInfo.CurrentCulture); } }


            /// <summary>
            /// PropertyChanged.
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;
            /// <summary>
            /// OnPropertyChanged.
            /// </summary>
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }
            /// <summary>
            /// SetField.
            /// </summary>
            protected bool SetField<T>(ref T field, T value, string propertyName)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }

        /// <summary>
        /// Ignore status converter.
        /// </summary>
        [ValueConversion(typeof(bool), typeof(string))]
        public class IgnoreStatusToTextConverter : IValueConverter
        {
            /// <summary>
            /// Convert.
            /// </summary>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return ((bool)value) ? Properties_Resources.DoNotIgnoreFolder : Properties_Resources.IgnoreFolder;
            }

            /// <summary>
            /// Convert back.
            /// </summary>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Ignore to text decoration.
        /// </summary>
        [ValueConversion(typeof(bool), typeof(TextDecorations))]
        public class IgnoreToTextDecoration : IValueConverter
        {
            /// <summary>
            /// Convert.
            /// </summary>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                bool ignored = (bool)value;
                if (ignored)
                    return TextDecorations.Strikethrough;
                else
                    return null;
            }

            /// <summary>
            /// ConvertBack.
            /// </summary>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Loading status converter.
        /// </summary>
        [ValueConversion(typeof(LoadingStatus), typeof(string))]
        public class LoadingStatusToTextConverter : IValueConverter
        {
            /// <summary>
            /// Convert.
            /// </summary>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                LoadingStatus status = (LoadingStatus)value;
                switch (status)
                {
                    case LoadingStatus.LOADING:
                        return Properties_Resources.LoadingStatusLOADING;
                    case LoadingStatus.START:
                        return Properties_Resources.LoadingStatusSTART;
                    default:
                        return "";
                }
            }

            /// <summary>
            /// ConvertBack.
            /// </summary>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        /// <summary>
        /// ConvertBack.
        /// </summary>
        [ValueConversion(typeof(LoadingStatus), typeof(Brush))]
        public class LoadingStatusToBrushConverter : IValueConverter
        {
            private Brush startBrush = Brushes.LightGray;
            /// <summary>
            /// StartBrush.
            /// </summary>
            public Brush StartBrush { get { return startBrush; } set { startBrush = value; } }
            /// <summary>
            /// ConvertBack.
            /// </summary>
            private Brush loadingBrush = Brushes.Gray;
            /// <summary>
            /// LoadingBrush.
            /// </summary>
            public Brush LoadingBrush { get { return loadingBrush; } set { loadingBrush = value; } }
            private Brush abortBrush = Brushes.DarkGray;
            /// <summary>
            /// AbortBrush.
            /// </summary>
            public Brush AbortBrush { get { return abortBrush; } set { abortBrush = value; } }
            private Brush failureBrush = Brushes.Red;
            /// <summary>
            /// ConvertBack.
            /// </summary>
            public Brush FailureBrush { get { return failureBrush; } set { failureBrush = value; } }
            private Brush doneBrush = Brushes.Black;
            /// <summary>
            /// DoneBrush.
            /// </summary>
            public Brush DoneBrush { get { return doneBrush; } set { doneBrush = value; } }

            /// <summary>
            /// Convert.
            /// </summary>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                LoadingStatus status = (LoadingStatus)value;
                switch (status)
                {
                    case LoadingStatus.START:
                        return startBrush;
                    case LoadingStatus.LOADING:
                        return loadingBrush;
                    case LoadingStatus.ABORTED:
                        return abortBrush;
                    case LoadingStatus.REQUEST_FAILURE:
                        return failureBrush;
                    case LoadingStatus.DONE:
                        return doneBrush;
                    default:
                        return Brushes.Black;
                }
            }

            /// <summary>
            /// ConvertBack.
            /// </summary>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }


        /// <summary>
        /// Folder type converter.
        /// </summary>
        [ValueConversion(typeof(Folder.FolderType), typeof(Brush))]
        public class ForlderTypeToBrushConverter : IValueConverter
        {
            private Brush localFolderBrush = Brushes.LightGray;

            /// <summary>
            /// Local folder.
            /// </summary>
            public Brush LocalFolderBrush { get { return localFolderBrush; } set { localFolderBrush = value; } }
            private Brush remoteFolderBrush = Brushes.LightBlue;

            /// <summary>
            /// Remote folder.
            /// </summary>
            public Brush RemoteFolderBrush { get { return remoteFolderBrush; } set { remoteFolderBrush = value; } }
            private Brush bothFolderBrush = Brushes.LightGreen;

            /// <summary>
            /// Both folder.
            /// </summary>
            public Brush BothFolderBrush { get { return bothFolderBrush; } set { bothFolderBrush = value; } }

            /// <summary>
            /// Convert.
            /// </summary>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Folder.FolderType type = (Folder.FolderType)value;
                switch (type)
                {
                    case Folder.FolderType.LOCAL:
                        return localFolderBrush;
                    case Folder.FolderType.REMOTE:
                        return remoteFolderBrush;
                    case Folder.FolderType.BOTH:
                        return bothFolderBrush;
                    default:
                        return Brushes.White;
                }
            }

            /// <summary>
            /// Convert back.
            /// </summary>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }
    }
}
