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
                BuildIgnoreFolder(ignores);
                BuildLocalFolder(localFolder, "/");
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
                                Type = CmisTree.Folder.FolderType.NONE,
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
                            folder = SubfolderHandleFolder(pathSub, current, CmisTree.Folder.FolderType.NONE);
                        }
                        current = folder;
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
                    Folder folder = BuildFolder(ignore);
                    folder.Selected = false;
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
                    Folder folder = BuildFolder(pathSub);
                    folder.AddType(CmisTree.Folder.FolderType.LOCAL);
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
                if (folder.Type == CmisTree.Folder.FolderType.LOCAL || folder.Type == CmisTree.Folder.FolderType.NONE)
                {
                    folder.Status = LoadingStatus.DONE;
                }
                foreach (Folder f in folder.SubFolder)
                {
                    if (folder.Type == CmisTree.Folder.FolderType.LOCAL || folder.Type == CmisTree.Folder.FolderType.NONE)
                    {
                        SubfolderHandleLocal(f);
                    }
                }
            }

            private void SubfolderHandleTree(CmisUtils.FolderTree tree, Folder parent)
            {
                foreach (CmisUtils.FolderTree child in tree.children)
                {
                    Folder folder = SubfolderHandleFolder(child.path, parent, CmisTree.Folder.FolderType.REMOTE);
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

            private Folder SubfolderHandleFolder(string path, Folder parent, CmisTree.Folder.FolderType type)
            {
                Folder folder = parent.GetSubFolder(path);
                if (folder != null)
                {
                    folder.AddType(type);
                    return folder;
                }

                folder = new Folder()
                {
                    Repo = this,
                    Path = path,
                    Name = path.Split('/')[path.Split('/').Length - 1],
                    Parent = parent,
                    Type = type,
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
                            Folder folder = SubfolderHandleFolder(f, currentWorkingObject, CmisTree.Folder.FolderType.REMOTE);
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
                                    sub.AddType(CmisTree.Folder.FolderType.REMOTE);
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
    }
}
