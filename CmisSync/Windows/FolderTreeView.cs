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

            private Folder currentWorkingObject;
            private Queue<Folder> queue = new Queue<Folder>();

            private LoadingStatus status = LoadingStatus.START;
            public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
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
                e.Result = CmisUtils.GetSubfolders(Id, Path, address, username, password);
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
                                IsIgnored = currentWorkingObject.IsIgnored
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
                    string[] subfolder = (string[])e.Result;
                    foreach (string f in subfolder)
                    {
                        Folder folder = new Folder()
                        {
                            Repo = this,
                            Path = f,
                            Name = f.Split('/')[f.Split('/').Length - 1],
                            Parent = this,
                            Type = CmisTree.Folder.FolderType.REMOTE
                        };
                        this.Folder.Add(folder);
                        this.queue.Enqueue(folder);
                    }
                    Status = LoadingStatus.DONE;
                    this.folderworker.RunWorkerAsync();
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

            private CmisRepo repo;
            public ICommand ToggleIgnoreCommand { get; set; }
            public object Parent { get; set; }
            public CmisRepo Repo { get { return repo; } set { SetField(ref repo, value, "Repo"); } }
            private bool selected = false;
            public bool Selected { get { return selected; } set { SetField(ref selected, value, "Selected"); } }
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
                                subfolder.IsIgnored = ignored;
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
