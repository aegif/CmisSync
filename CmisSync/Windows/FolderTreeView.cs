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
        /// Data Model for the WPF UI representing a CMIS repository
        /// </summary>
        public class CmisRepo : INotifyPropertyChanged
        {
            private BackgroundWorker worker = new BackgroundWorker();
            private BackgroundWorker folderworker = new BackgroundWorker();
            private ObservableCollection<Folder> _folder = new ObservableCollection<Folder>();
            private string name;
            /// <summary>
            /// Gets and sets the name of the repository. PropertyChangeListener will be notified on change
            /// </summary>
            public string Name { get { return name; } set { SetField(ref name, value, "Name"); } }
            /// <summary>
            /// Gets and sets the unique repository id. A change would not be propagated to listener
            /// </summary>
            public string Id { get; set; }
            /// <summary>
            /// Readonly Path of the repository.
            /// </summary>
            public string Path { get { return "/"; } }


            private string username;
            private string password;
            private string address;

            /// <summary>
            /// The URL of the repository. 
            /// </summary>
            public string Address { get { return address; } }

            private Folder currentWorkingObject;
            private Queue<Folder> queue = new Queue<Folder>();

            private LoadingStatus status = LoadingStatus.START;
            /// <summary>
            /// Status of the asynchronous loading process. If it is done, the direct sub folder of the repository has been loaded 
            /// </summary>
            public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
            private bool threestate = false;
            /// <summary>
            /// The repo can be set to be able to have three selection states. This enables/disables this support.
            /// </summary>
            public bool ThreeState { get { return threestate; } set { SetField(ref threestate, value, "ThreeState"); } }
            /// <summary>
            /// Tooltip informations about this repository. It returns the URL and the repository id
            /// </summary>
            public string ToolTip { get { return "URL: \"" + address + "\"\r\nRepository ID: \"" + Id + "\""; } }
            private bool selected = false;
            /// <summary>
            /// Sets this repository as selected or not. PropertyChangeListener will be informed about any changes
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
            /// If all subfolder should be synchronized, this is true, if not, new folder on the the root directory
            /// must be selected manually. 
            /// TODO this feature is whether tested not correctly implemented. Please do not set it, or fix it.
            /// </summary>
            public bool SyncAllSubFolder { get { return automaticSyncAllNewSubfolder; } set {
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
            } }
            /// <summary>
            /// Collection contains all folder, which are loaded. If the loading is in process, this could be empty or not in the final state
            /// </summary>
            public ObservableCollection<Folder> Folder
            {
                get { return _folder; }
            }
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="username">login username</param>
            /// <param name="password">password</param>
            /// <param name="address">URL of the repo location</param>
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
            /// Starts the subfolder loading of the CMIS Repo
            /// </summary>
            public void LoadingSubfolderAsync()
            {
                if (status == LoadingStatus.START)
                {
                    status = LoadingStatus.LOADING;
                    this.worker.RunWorkerAsync();
                }
            }

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
                    e.Result = CmisUtils.GetSubfolderTree(Id,Path,address,username,password,-1);
                } catch(Exception) {
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


            // boiler-plate
            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }
            protected bool SetField<T>(ref T field, T value, string propertyName)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }

        /// <summary>
        /// Folder data structure for WPF Control
        /// </summary>
        public class Folder : INotifyPropertyChanged
        {
/*            public class IgnoreToggleCommand : ICommand
            {
                public event EventHandler CanExecuteChanged;
                public bool CanExecute(Object parameter)
                {
                    Folder f = parameter as Folder;
                    if (f != null)
                        return true;
                    else
                        return false;
                }

                public void Execute(Object parameter)
                {
                    Folder f = parameter as Folder;
                    if (f != null)
                        f.IsIgnored = !f.ignored;
                }
            }*/
            /// <summary>
            /// Constructor if the whole FolderTree is loaded. It creates a folder tree recursivly.
            /// </summary>
            /// <param name="tree"></param>
            /// <param name="repo"></param>
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
                    this.SubFolder.Add(new Folder(t, Repo) { Parent = this});
                }
            }
            /// <summary>
            /// Default constructor. All properties must be manually set.
            /// </summary>
            public Folder() { }

            private CmisRepo repo;
            /// <summary>
            /// Parent Object of a Folder
            /// </summary>
            public object Parent { get; set; }
            /// <summary>
            /// Repository from where the folder has been loaded
            /// </summary>
            public CmisRepo Repo { get { return repo; } set { SetField(ref repo, value, "Repo"); } }
            private bool threestates = false;
            /// <summary>
            /// Gets and sets the ThreeStates capability of Selected Property.
            /// </summary>
            public bool ThreeStates { get { return threestates; } set { SetField(ref threestates, value, "ThreeStates"); } }
            private bool? selected = true;
            /// <summary>
            /// Sets and gets the Selected Property. If true is set, all children will also be selected,
            /// if false, none of the children is selected. If none, at least one of the children is not selected and at least one is selected
            /// </summary>
            public bool? Selected { get { return selected; } set {
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
                                else if (p.SubFolder.Count > 1) {
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
            } }
            private string name;
            /// <summary>
            /// The name of the folder
            /// </summary>
            public string Name { get { return name; } set { SetField(ref name, value, "Name"); } }
            private string path;
            /// <summary>
            /// The absolut path of the folder
            /// </summary>
            public string Path { get { return path; } set { SetField(ref path, value, "Path"); } }
            private bool ignored = false;
            /// <summary>
            /// Sets and gets the Ignored status of a folder
            /// </summary>
            public bool IsIgnored { get { return ignored; } set {
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
            } }
            private bool enabled = true;
            /// <summary>
            /// Sets and gets the Enabled state of a folder.
            /// If a state is changed, also its child folders are set to the same state.
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
            /// Loading status of a folder
            /// </summary>
            public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
            private ObservableCollection<Folder> _subfolder = new ObservableCollection<Folder>();
            /// <summary>
            /// All subfolder of this folder.
            /// </summary>
            public ObservableCollection<Folder> SubFolder { get { return _subfolder; } }
            private FolderType folderType = FolderType.REMOTE;
            /// <summary>
            /// The Type of a folder can be any FolderType
            /// </summary>
            public FolderType Type { get { return folderType; } set { SetField(ref folderType, value, "Type"); } }

            /// <summary>
            /// Enumaration of all possible Folder Types. It can be Remote, Local, or Both.
            /// </summary>
            public enum FolderType
            {
                LOCAL, REMOTE, BOTH
            }

            // boiler-plate
            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }
            protected bool SetField<T>(ref T field, T value, string propertyName)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }
        /// <summary>
        /// Status of a datatype, which children could be loaded asynchronous
        /// </summary>
        public enum LoadingStatus
        {
            START, LOADING, ABORTED, REQUEST_FAILURE, DONE
        }

/*        public class StatusItem : INotifyPropertyChanged
        {
            private LoadingStatus status = LoadingStatus.LOADING;
            public LoadingStatus Status { get { return status; } set { if (SetField(ref status, value, "Status")) OnPropertyChanged("Message"); } }
            public string Message { get { return CmisSync.Properties_Resources.ResourceManager.GetString("LoadingStatus." + status.ToString(), CultureInfo.CurrentCulture); ; } }


            // boiler-plate
            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }
            protected bool SetField<T>(ref T field, T value, string propertyName)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }*/

        /// <summary>
        /// Converter of the ignore Status of a folder which returns a string, discribing the ignore status
        /// </summary>
        [ValueConversion(typeof(bool), typeof(string))]
        public class IgnoreStatusToTextConverter : IValueConverter
        {
            /// <summary>
            /// Converts the given ignore status into a text message
            /// </summary>
            /// <param name="value"></param>
            /// <param name="targetType"></param>
            /// <param name="parameter"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return ((bool)value) ? Properties_Resources.DoNotIgnoreFolder : Properties_Resources.IgnoreFolder;
            }
            /// <summary>
            /// Is not supported
            /// </summary>
            /// <param name="value"></param>
            /// <param name="targetType"></param>
            /// <param name="parameter"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Converter to convert the ignore status to a text decoration
        /// </summary>
        [ValueConversion(typeof(bool), typeof(TextDecorations))]
        public class IgnoreToTextDecoration : IValueConverter
        {
            /// <summary>
            /// Converts the ignore status to a text decoration
            /// </summary>
            /// <param name="value"></param>
            /// <param name="targetType"></param>
            /// <param name="parameter"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                bool ignored = (bool)value;
                if (ignored)
                    return TextDecorations.Strikethrough;
                else
                    return null;
            }

            /// <summary>
            /// Not supported
            /// </summary>
            /// <param name="value"></param>
            /// <param name="targetType"></param>
            /// <param name="parameter"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Converter for the LoadingStatus to a cultrue depending string
        /// </summary>
        [ValueConversion(typeof(LoadingStatus), typeof(string))]
        public class LoadingStatusToTextConverter : IValueConverter
        {
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
            /// Not supported
            /// </summary>
            /// <param name="value"></param>
            /// <param name="targetType"></param>
            /// <param name="parameter"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Converts the LoadingStatus to a Color and gives the possibility to change the default colors
        /// </summary>
        [ValueConversion(typeof(LoadingStatus), typeof(Brush))]
        public class LoadingStatusToBrushConverter : IValueConverter
        {
            private Brush startBrush = Brushes.LightGray;
            /// <summary>
            /// Color of the LoadingStatus.START status
            /// </summary>
            public Brush StartBrush { get { return startBrush; } set { startBrush = value; } }
            private Brush loadingBrush = Brushes.Gray;
            /// <summary>
            /// Color of the LoadingStatus.LOADING status
            /// </summary>
            public Brush LoadingBrush { get { return loadingBrush; } set { loadingBrush = value; } }
            private Brush abortBrush = Brushes.DarkGray;
            /// <summary>
            /// Color of the LoadingStatus.ABORT status
            /// </summary>
            public Brush AbortBrush { get { return abortBrush; } set { abortBrush = value; } }
            private Brush failureBrush = Brushes.Red;
            /// <summary>
            /// Color of the LoadingStatus.FAILURE status
            /// </summary>
            public Brush FailureBrush { get { return failureBrush; } set { failureBrush = value; } }
            private Brush doneBrush = Brushes.Black;
            /// <summary>
            /// Color of the LoadingStatus.DONE status
            /// </summary>
            public Brush DoneBrush { get { return doneBrush; } set { doneBrush = value; } }

            /// <summary>
            /// Converts the given LoadingStatus to a Brush with the selected Color
            /// </summary>
            /// <param name="value"></param>
            /// <param name="targetType"></param>
            /// <param name="parameter"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
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
            /// Not supported
            /// </summary>
            /// <param name="value"></param>
            /// <param name="targetType"></param>
            /// <param name="parameter"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Converter for FolderType to Color convertion
        /// </summary>
        [ValueConversion(typeof(Folder.FolderType), typeof(Brush))]
        public class ForlderTypeToBrushConverter : IValueConverter
        {
            private Brush localFolderBrush = Brushes.LightGray;
            /// <summary>
            /// Color of FolderType.LOCAL
            /// </summary>
            public Brush LocalFolderBrush { get { return localFolderBrush; } set { localFolderBrush = value; } }
            private Brush remoteFolderBrush = Brushes.LightBlue;
            /// <summary>
            /// Color of FolderType.REMOTE
            /// </summary>
            public Brush RemoteFolderBrush { get { return remoteFolderBrush; } set { remoteFolderBrush = value; } }
            private Brush bothFolderBrush = Brushes.LightGreen;
            /// <summary>
            /// Color of FolderType.BOTH
            /// </summary>
            public Brush BothFolderBrush { get { return bothFolderBrush; } set { bothFolderBrush = value; } }
            /// <summary>
            /// Converts the given FolderType to a Brush with the selected color
            /// </summary>
            /// <param name="value"></param>
            /// <param name="targetType"></param>
            /// <param name="parameter"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
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
            /// Not supported
            /// </summary>
            /// <param name="value"></param>
            /// <param name="targetType"></param>
            /// <param name="parameter"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }
    }
}
