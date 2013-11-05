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
        public class CmisRepoUtils
        {
            private BackgroundWorker worker = new BackgroundWorker();
            private BackgroundWorker folderworker = new BackgroundWorker();


            private Folder currentWorkingObject;
            private List<Folder> queue = new List<Folder>();

            /// <summary>
            /// Get the ignored folder list
            /// </summary>
            /// <returns></returns>
            public static List<string> GetIgnoredFolder(RootFolder repo)
            {
                List<string> result = new List<string>();
                foreach (Folder child in repo.Children)
                {
                    result.AddRange(Folder.GetIgnoredFolder(child));
                }
                return result;
            }

            /// <summary>
            /// Get the selected folder list
            /// </summary>
            /// <returns></returns>
            public static List<string> GetSelectedFolder(RootFolder repo)
            {
                List<string> result = new List<string>();
                foreach (Folder child in repo.Children)
                {
                    result.AddRange(Folder.GetSelectedFolder(child));
                }
                return result;
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="ignores">ignore folder list</param>
            /// <param name="localFolder">local repository folder</param>
            private CmisRepoUtils( List<string> ignores = null, string localFolder = null)
            {
                worker.DoWork += new DoWorkEventHandler(DoWork);
                worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Finished);
                worker.WorkerSupportsCancellation = true;
                folderworker.DoWork += new DoWorkEventHandler(SubFolderWork);
                folderworker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(SubfolderFinished);
                folderworker.WorkerSupportsCancellation = true;
            }

            public static BackgroundWorker LoadingSubfolderAsync(RootFolder targetRepo, CmisSync.Lib.Credentials.CmisRepoCredentials credentials , List<string> ignores = null, string localFolder = null)
            {
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += new DoWorkEventHandler(DoWork);
                worker.RunWorkerAsync(credentials);
                return worker;
            }

            private static Folder BuildFolder(Node node, string path)
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
                        foreach (Folder folder in node.Children)
                        {
                            if (folder.Name.Equals(name))
                            {
                                current = folder;
                                break;
                            }
                        }
                        if (current == null)
                        {
                            current = new Folder()
                            {
                                Parent = node,
                                Path = "/" + name,
                                Name = name,
                                Type = CmisTree.Folder.FolderType.NONE,
                                Enabled = node.Selected==true
                            };
                            node.Children.Add(current);
                        }
                    }
                    else
                    {
                        //  Folder in Folder.SubFolder
                        string pathSub = current.Path + "/" + name;
                        Folder folder = Folder.GetSubFolder(pathSub, current);
                        if (folder == null)
                        {
                            folder = SubfolderHandleFolder(pathSub, current, CmisTree.Folder.FolderType.NONE);
                        }
                        current = folder;
                    }
                }

                return current;
            }

            public static void BuildIgnoreFolder(RootFolder root, List<string> ignores)
            {
                if (null == ignores)
                {
                    return;
                }

                foreach (string ignore in ignores)
                {
                    Folder folder = BuildFolder(root, ignore);
                    folder.Selected = false;
                }
            }

            public static void BuildLocalFolder(Node node, string localFolder, string path)
            {
                if (null == localFolder)
                {
                    return;
                }

                foreach (DirectoryInfo dir in (new DirectoryInfo(localFolder)).GetDirectories())
                {
                    string pathSub = (path + "/" + dir.Name).Replace("//", "/");
                    Folder folder = BuildFolder(node, pathSub);
                    folder.AddType(CmisTree.Folder.FolderType.LOCAL);
                    BuildLocalFolder(node, dir.FullName, pathSub);
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

            private static void DoWork(object sender, DoWorkEventArgs e)
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
                    foreach (Folder subfolder in f.Children)
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
                foreach (Folder f in folder.Children)
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
                    Folder current = Folder.GetSubFolder(child.path, parent);
                    SubfolderHandleTree(child, current);
                }
            }

            private Folder SubfolderHandleFolder(string path, Folder parent, CmisTree.Folder.FolderType type)
            {
                Folder folder = Folder.GetSubFolder(path, parent);
                if (folder != null)
                {
                    folder.AddType(type);
                    return folder;
                }

                folder = new Folder()
                {
                    Path = path,
                    Name = path.Split('/')[path.Split('/').Length - 1],
                    Parent = parent,
                    Type = type,
                    IsIgnored = parent.IsIgnored,
                    Selected = (parent.Selected == false) ? false : true,
                    Enabled = parent.Enabled
                };
                parent.Children.Add(folder);
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
        }
    }
}
