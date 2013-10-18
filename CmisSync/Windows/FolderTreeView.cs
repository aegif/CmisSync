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
using System.IO;
using System.Diagnostics;

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
            private List<Folder> queue = new List<Folder>();

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
            /// The folder that is selected by user
            /// </summary>
            public Folder SelectFolder
            {
                get;
                set;
            }

            /// <summary>
            /// Get the ignored folder list
            /// </summary>
            /// <returns></returns>
            public List<string> GetIgnoredFolder()
            {
                List<string> result = new List<string>();
                foreach (Folder child in Folder)
                {
                    result.AddRange(child.GetIgnoredFolder());
                }
                return result;
            }

            /// <summary>
            /// Get the selected folder list
            /// </summary>
            /// <returns></returns>
            public List<string> GetSelectedFolder()
            {
                List<string> result = new List<string>();
                foreach (Folder child in Folder)
                {
                    result.AddRange(child.GetSelectedFolder());
                }
                return result;
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="username">login username</param>
            /// <param name="password">password</param>
            /// <param name="address">URL of the repo location</param>
            /// <param name="ignores">ignore folder list</param>
            /// <param name="localFolder">local repository folder</param>
            public CmisRepo(string username, string password, string address, List<string> ignores = null, string localFolder = null)
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
                BuildLocalFolder(localFolder,"/");
                BuildIgnoreFolder(ignores);
            }

            private Folder BuildFolder(string path)
            {
                if (String.IsNullOrEmpty(path))
                {
                    return null;
                }

                Folder current = null;
                string [] names = path.Split('/');
                foreach (string name in names)
                {
                    if (String.IsNullOrEmpty(name))
                    {
                        continue;
                    }
                    if (current == null)
                    {
                        //  Folder in CmisRepo.Folder
                        foreach (Folder folder in Folder)
                        {
                            if (folder.Name == name)
                            {
                                current = folder;
                                break;
                            }
                        }
                        if (current == null)
                        {
                            current = new Folder()
                            {
                                Repo = this,
                                Path = "/" + name,
                                Name = name,
                                Parent = this,
                                Type = CmisTree.Folder.FolderType.LOCAL,
                                Enabled = this.selected
                            };
                            Folder.Add(current);
                        }
                    }
                    else
                    {
                        //  Folder in Folder.SubFolder
                        string pathSub = current.Path + "/" + name;
                        Folder folder = current.GetSubFolder(pathSub);
                        if (folder == null)
                        {
                            folder = SubfolderHandleFolder(pathSub, current);
                        }
                        current = folder;
                        current.Type = CmisTree.Folder.FolderType.LOCAL;
                    }
                }

                return current;
            }

            private void BuildIgnoreFolder(List<string> ignores)
            {
                if (null == ignores)
                {
                    return;
                }

                foreach (string ignore in ignores)
                {
                    BuildFolder(ignore).Selected = false;
                }
            }

            private void BuildLocalFolder(string localFolder,string path)
            {
                if (null == localFolder)
                {
                    return;
                }

                foreach (DirectoryInfo dir in (new DirectoryInfo(localFolder)).GetDirectories())
                {
                    string pathSub = (path + "/" + dir.Name).Replace("//", "/");
                    BuildFolder(pathSub);
                    BuildLocalFolder(dir.FullName, pathSub);
                }
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

            /// <summary>
            /// Cancels the loading of the CMIS Repo
            /// </summary>
            public void cancelLoadingAsync()
            {
                this.worker.CancelAsync();
                this.folderworker.CancelAsync();
            }

            private void DoWork(object sender, DoWorkEventArgs e)
            {
                BackgroundWorker worker = sender as BackgroundWorker;
                //try
                //{
                //    e.Result = CmisUtils.GetSubfolderTree(Id, Path, address, username, password, -1);
                //}
                //catch (Exception)
                //{
                    e.Result = CmisUtils.GetSubfolders(Id, Path, address, username, password);
                //}
                if (worker.CancellationPending)
                    e.Cancel = true;
            }

            private void SubFolderWork(object sender, DoWorkEventArgs e)
            {
                BackgroundWorker worker = sender as BackgroundWorker;

                Folder f = SelectFolder;
                bool treeWork = false;
                if (f != null && f.Status == LoadingStatus.DONE)
                {
                    //  adjust the queue to handle the subfolders for the selected folder
                    int index = 0;
                    foreach (Folder subfolder in f.SubFolder)
                    {
                        if (subfolder.Status == LoadingStatus.START)
                        {
                            queue.Remove(subfolder);
                            queue.Insert(index, subfolder);
                            index++;
                        }
                    }
                    if (index >= 2)
                    {
                        for (int i = 0; i < index; ++i)
                        {
                            queue.RemoveAt(0);
                        }
                        treeWork = true;
                    }
                    else
                    {
                        f = queue[0];
                        queue.RemoveAt(0);
                    }
                }
                else if (f != null && f.Status == LoadingStatus.START)
                {
                    //  continue with this selected folder
                    queue.Remove(f);
                }
                else
                {
                    f = queue[0];
                    queue.RemoveAt(0);
                }

                currentWorkingObject = f;
                currentWorkingObject.Status = LoadingStatus.LOADING;
                if (treeWork)
                {
                    //Console.WriteLine("Handle tree " + f.Path);
                    e.Result = CmisUtils.GetSubfolderTree(Id, f.Path, address, username, password, 2);
                }
                else
                {
                    //Console.WriteLine("Handle " + f.Path);
                    e.Result = CmisUtils.GetSubfolders(Id, f.Path, address, username, password);
                }
                //System.Threading.Thread.Sleep(1000);
                if (worker.CancellationPending)
                    e.Cancel = true;
            }

            private void SubfolderHandleLocal(Folder folder)
            {
                if (folder.Type == CmisTree.Folder.FolderType.LOCAL)
                {
                    folder.Status = LoadingStatus.DONE;
                }
                foreach (Folder f in folder.SubFolder)
                {
                    if (f.Type == CmisTree.Folder.FolderType.LOCAL)
                    {
                        SubfolderHandleLocal(f);
                    }
                }
            }

            private void SubfolderHandleTree(CmisUtils.FolderTree tree, Folder parent)
            {
                foreach (CmisUtils.FolderTree child in tree.children)
                {
                    Folder folder = SubfolderHandleFolder(child.path, parent);
                    if (child.Finished)
                    {
                        queue.Remove(folder);
                        folder.Status = LoadingStatus.DONE;
                    }
                    if (folder.Status == LoadingStatus.START)
                    {
                        queue.Remove(folder);
                        queue.Add(folder);
                    }
                }

                if (tree.Finished)
                {
                    SubfolderHandleLocal(parent);
                }

                foreach (CmisUtils.FolderTree child in tree.children)
                {
                    Folder current = parent.GetSubFolder(child.path);
                    SubfolderHandleTree(child, current);
                }
            }

            private Folder SubfolderHandleFolder(string path, Folder parent)
            {
                Folder folder = parent.GetSubFolder(path);
                if (folder != null)
                {
                    if (folder.Type == CmisTree.Folder.FolderType.LOCAL)
                    {
                        folder.Type = CmisTree.Folder.FolderType.BOTH;
                    }
                    return folder;
                }

                folder = new Folder()
                {
                    Repo = this,
                    Path = path,
                    Name = path.Split('/')[path.Split('/').Length - 1],
                    Parent = parent,
                    Type = CmisTree.Folder.FolderType.REMOTE,
                    IsIgnored = parent.IsIgnored,
                    Selected = (parent.Selected == false) ? false : true,
                    Enabled = parent.Enabled
                };
                parent.SubFolder.Add(folder);
                return folder;
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
                    if (e.Result is CmisSync.Lib.Cmis.CmisUtils.FolderTree)
                    {
                        SubfolderHandleTree(e.Result as CmisUtils.FolderTree, currentWorkingObject);
                    }
                    else
                    {
                        string[] subfolder = (string[])e.Result;
                        foreach (string f in subfolder)
                        {
                            Folder folder = SubfolderHandleFolder(f, currentWorkingObject);
                            if (folder.Status == LoadingStatus.START)
                            {
                                this.queue.Remove(folder);
                                this.queue.Add(folder);
                            }
                        }
                        SubfolderHandleLocal(currentWorkingObject);
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
                            Folder folder = null;
                            foreach (Folder f in Folder)
                            {
                                if (f.Path == repofolder.path)
                                {
                                    folder = f;
                                    break;
                                }
                            }
                            this.Folder.Remove(folder);
                            folder = new Folder(repofolder, this, folder);
                            this.Folder.Add(folder);
                        }
                        foreach (Folder folder in this.Folder)
                        {
                            SubfolderHandleLocal(folder);
                        }
                        Status = LoadingStatus.DONE;
                    }
                    else
                    {
                        if (e.Result == null) return;
                        string[] subfolder = (string[])e.Result;
                        foreach (string f in subfolder)
                        {
                            Folder folder = null;
                            foreach (Folder sub in Folder)
                            {
                                if (f == sub.Path)
                                {
                                    if (sub.Type == CmisTree.Folder.FolderType.LOCAL)
                                    {
                                        sub.Type = CmisTree.Folder.FolderType.BOTH;
                                    }
                                    folder = sub;
                                    break;
                                }
                            }
                            if (folder == null)
                            {
                                folder = new Folder()
                                {
                                    Repo = this,
                                    Path = f,
                                    Name = f.Split('/')[f.Split('/').Length - 1],
                                    Parent = this,
                                    Type = CmisTree.Folder.FolderType.REMOTE,
                                    Enabled = this.selected
                                };
                                Folder.Add(folder);
                            }
                            if (folder.Status == LoadingStatus.START)
                            {
                                queue.Add(folder);
                            }
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
            /// Return the folder list when <c>Selected</c> is <c>false</c>
            /// </summary>
            /// <returns></returns>
            public List<string> GetIgnoredFolder()
            {
                List<string> result = new List<string>();
                if (IsIgnored)
                {
                    result.Add(Path);
                }
                else
                {
                    foreach (Folder child in SubFolder)
                        result.AddRange(child.GetIgnoredFolder());
                }
                return result;
            }


            /// <summary>
            /// Return the folder list when <c>Selected</c> is <c>true</c>
            /// </summary>
            /// <returns></returns>
            public List<string> GetSelectedFolder()
            {
                List<string> result = new List<string>();
                if (Selected == true)
                    result.Add(Path);
                else
                {
                    foreach (Folder child in SubFolder)
                        result.AddRange(child.GetSelectedFolder());
                }
                return result;
            }


            /// <summary>
            /// Constructor if the whole FolderTree is loaded. It creates a folder tree recursivly.
            /// </summary>
            /// <param name="tree"></param>
            /// <param name="repo"></param>
            public Folder(CmisUtils.FolderTree tree, CmisRepo repo, Folder old)
            {
                this.Path = tree.path;
                this.Repo = repo;
                this.Name = tree.Name;
                this.Type = FolderType.REMOTE;
                if (tree.Finished)
                {
                    this.Status = LoadingStatus.DONE;
                }
                else
                {
                    this.Status = LoadingStatus.START;
                    Debug.Assert(false, "Whole CMIS folder tree should be queried");
                }
                this.Enabled = repo.Selected;
                foreach (CmisUtils.FolderTree t in tree.children)
                {
                    Folder oldSub = null;
                    if (old != null)
                    {
                        oldSub = old.GetSubFolder(t.path);
                    }
                    this.SubFolder.Add(new Folder(t, Repo, oldSub) { Parent = this });
                }

                //  Support local folder in old
                if (old != null)
                {
                    if (old.Type != FolderType.LOCAL)
                    {
                        Debug.Assert(false, "old Folder should be local folder tree only");
                    }
                    this.Type = FolderType.BOTH;
                    this.Selected = old.Selected;
                    foreach (Folder o in old.SubFolder)
                    {
                        if (o.Type != FolderType.LOCAL)
                        {
                            Debug.Assert(false, "old Folder should be local folder tree only");
                            continue;
                        }
                        if (null == GetSubFolder(o.path))
                        {
                            this.SubFolder.Add(o);
                        }
                    }
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
            private bool expanded = false;
            /// <summary>
            /// Sets and gets the Expanded state of a folder.
            /// </summary>
            public bool Expanded
            {
                get { return enabled; }
                set
                {
                    expanded = value;
                    if (value)
                    {
                        this.Repo.SelectFolder = this;
                    }
                    else
                    {
                        if (this.Repo.SelectFolder == this)
                        {
                            this.Repo.SelectFolder = null;
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
            /// <summary>
            /// Get folder from <c>SubFolder</c> for the path
            /// </summary>
            public Folder GetSubFolder(string path)
            {
                foreach (Folder folder in SubFolder)
                {
                    if (folder.path == path)
                    {
                        return folder;
                    }
                }
                return null;
            }
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
