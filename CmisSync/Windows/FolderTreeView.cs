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

        public class CmisRepo : INotifyPropertyChanged
        {
            private BackgroundWorker worker = new BackgroundWorker();
            private BackgroundWorker folderworker = new BackgroundWorker();
            private ObservableCollection<Folder> _folder = new ObservableCollection<Folder>();
            private string name;
            public string Name { get { return name; } set { SetField(ref name, value, "Name"); } }
            public string Id { get; set; }
            public string Path { get { return "/"; } }


            private string username;
            private string password;
            private string address;

            public string Address { get { return address; } }

            private Folder currentWorkingObject;
            private Queue<Folder> queue = new Queue<Folder>();

            private LoadingStatus status = LoadingStatus.START;
            public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
            private bool threestate = false;
            public bool ThreeState { get { return threestate; } set { SetField(ref threestate, value, "ThreeState"); } }
            private bool selected = false;
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
            public ObservableCollection<Folder> Folder
            {
                get { return _folder; }
            }

            public CmisRepo(string username, string password, string address)
            {
                this.username = username;
                this.password = password;
                this.address = address;
                worker.DoWork += new DoWorkEventHandler(DoWork);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Finished);
                folderworker.DoWork += new DoWorkEventHandler(SubFolderWork);
                folderworker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(SubfolderFinished);
            }

            public void asyncSubFolderLoading()
            {
                if (status == LoadingStatus.START)
                {
                    status = LoadingStatus.LOADING;
                    this.worker.RunWorkerAsync();
                }
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
            }

            private void SubFolderWork(object sender, DoWorkEventArgs e)
            {
                BackgroundWorker worker = sender as BackgroundWorker;
                Folder f = queue.Dequeue();
                currentWorkingObject = f;
                currentWorkingObject.Status = LoadingStatus.LOADING;
                e.Result = CmisUtils.GetSubfolders(Id, f.Path, address, username, password);
            }

            private void SubfolderFinished(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    currentWorkingObject.Status = LoadingStatus.REQUEST_FAILURE;
                }
                else if (e.Cancelled)
                    currentWorkingObject.Status = LoadingStatus.ABORTED;
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
                if (queue.Count > 0)
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
                        if (this.queue.Count > 0)
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

        public class Folder : INotifyPropertyChanged
        {
            public class IgnoreToggleCommand : ICommand
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
            }

            public Folder(CmisUtils.FolderTree tree, CmisRepo repo)
            {
                this.Path = path;
                this.Repo = repo;
                this.Name = tree.Name;
                this.Type = FolderType.REMOTE;
                this.Status = LoadingStatus.DONE;
                this.Enabled = repo.Selected;
                foreach (CmisUtils.FolderTree t in tree.children)
                {
                    this.SubFolder.Add(new Folder(t, Repo));
                }
            }
            public Folder() { }

            private CmisRepo repo;
            public object Parent { get; set; }
            public CmisRepo Repo { get { return repo; } set { SetField(ref repo, value, "Repo"); } }
            private bool threestates = false;
            public bool ThreeStates { get { return threestates; } set { SetField(ref threestates, value, "ThreeStates"); } }
            private bool? selected = true;
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
                            subfolder.ThreeStates = false;
                        }
                        Folder p = this.Parent as Folder;
                        bool found = false;
                        while (p != null)
                        {
                            if (p.Selected == null)
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
                                else
                                {
                                    break;
                                }
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
            public string Name { get { return name; } set { SetField(ref name, value, "Name"); } }
            private string path;
            public string Path { get { return path; } set { SetField(ref path, value, "Path"); } }
            private bool ignored = false;
            public bool IsIgnored
            {
                get { return ignored; }
                set
                {
                    if (SetField(ref ignored, value, "IsIgnored"))
                    {
                        if (ignored)
                        {
                            foreach (Folder subfolder in _subfolder)
                            {
                                subfolder.IsIgnored = ignored;
                                subfolder.Selected = false;
                            }
                        }
                        else
                        {
                            Folder parent = Parent as Folder;
                            while (parent != null)
                            {
                                if (parent.ignored)
                                    parent.IsIgnored = false;
                                else
                                    break;
                                parent = parent.Parent as Folder;
                            }
                        }
                    }
                }
            }
            private bool enabled = true;
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
            public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
            private ObservableCollection<Folder> _subfolder = new ObservableCollection<Folder>();
            public ObservableCollection<Folder> SubFolder { get { return _subfolder; } }
            private FolderType folderType = FolderType.REMOTE;
            public FolderType Type { get { return folderType; } set { SetField(ref folderType, value, "Type"); } }

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

        public enum LoadingStatus
        {
            START, LOADING, ABORTED, REQUEST_FAILURE, DONE
        }

        public class StatusItem : INotifyPropertyChanged
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
        }

        [ValueConversion(typeof(bool), typeof(string))]
        public class IgnoreStatusToTextConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return ((bool)value) ? Properties_Resources.DoNotIgnoreFolder : Properties_Resources.IgnoreFolder;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        [ValueConversion(typeof(bool), typeof(TextDecorations))]
        public class IgnoreToTextDecoration : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                bool ignored = (bool)value;
                if (ignored)
                    return TextDecorations.Strikethrough;
                else
                    return null;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

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

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        [ValueConversion(typeof(LoadingStatus), typeof(Brush))]
        public class LoadingStatusToBrushConverter : IValueConverter
        {
            private Brush startBrush = Brushes.LightGray;
            public Brush StartBrush { get { return startBrush; } set { startBrush = value; } }
            private Brush loadingBrush = Brushes.Gray;
            public Brush LoadingBrush { get { return loadingBrush; } set { loadingBrush = value; } }
            private Brush abortBrush = Brushes.DarkGray;
            public Brush AbortBrush { get { return abortBrush; } set { abortBrush = value; } }
            private Brush failureBrush = Brushes.Red;
            public Brush FailureBrush { get { return failureBrush; } set { failureBrush = value; } }
            private Brush doneBrush = Brushes.Black;
            public Brush DoneBrush { get { return doneBrush; } set { doneBrush = value; } }

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

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }

        [ValueConversion(typeof(Folder.FolderType), typeof(Brush))]
        public class ForlderTypeToBrushConverter : IValueConverter
        {
            private Brush localFolderBrush = Brushes.LightGray;
            public Brush LocalFolderBrush { get { return localFolderBrush; } set { localFolderBrush = value; } }
            private Brush remoteFolderBrush = Brushes.LightBlue;
            public Brush RemoteFolderBrush { get { return remoteFolderBrush; } set { remoteFolderBrush = value; } }
            private Brush bothFolderBrush = Brushes.LightGreen;
            public Brush BothFolderBrush { get { return bothFolderBrush; } set { bothFolderBrush = value; } }

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

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            { throw new NotSupportedException(); }
        }
    }
}
