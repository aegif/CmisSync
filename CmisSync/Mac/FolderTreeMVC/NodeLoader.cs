using CmisSync.Auth;
using CmisSync.Lib.Cmis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace CmisSync.CmisTree
{
    /// <summary>
    /// Loads child nodes of the given root node
    /// </summary>
    public class AsyncNodeLoader
    {
        private RootFolder root;
        private CmisRepoCredentials repoCredentials;
        private BackgroundWorker worker;
        private Node actualNode;
        private Stack<Node> toBeLoaded = new Stack<Node>();
        private Object lockToBeLoaded = new object();
        private LoadChildrenDelegate method;
        private CheckChildrenDelegate check;

        /// <summary>
        /// Function for loading children of the given node
        /// </summary>
        /// <param name="credentials">Needed to authenticate on the server</param>
        /// <param name="root">Parent node, which children should be loaded</param>
        /// <returns>List of the found children nodes</returns>
        public delegate List<Node> LoadChildrenDelegate(CmisRepoCredentials credentials, Node root );

        /// <summary>
        /// Function for check load children finished of the given node
        /// </summary>
        public delegate bool CheckChildrenDelegate(Node root);

        /// <summary>
        /// Action after one Node is updated
        /// </summary>
        public event Action UpdateNodeEvent = delegate { };

        /// <summary>
        /// This class can be used to load children nodes asynchronous in another thread
        /// </summary>
        /// <param name="root">The repository Node</param>
        /// <param name="credentials">The server crendentials to load children for the given Cmis Repo</param>
        /// <param name="method">Function for loading nodes</param>
        public AsyncNodeLoader(RootFolder root, CmisRepoCredentials credentials, LoadChildrenDelegate method, CheckChildrenDelegate check)
        {
            this.root = root;
            repoCredentials = credentials;
            this.actualNode = null;
            this.worker = new BackgroundWorker();
            this.method = method;
            this.check = check;
            this.worker.WorkerReportsProgress = false;
            this.worker.WorkerSupportsCancellation = true;
            this.worker.DoWork += new DoWorkEventHandler(DoWork);
            this.worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Finished);
        }

        private void Load()
        {
            if (!this.worker.IsBusy)
            {
                try
                {
                    Node next = null;
                    lock(lockToBeLoaded) {
                        do {
                            next = this.toBeLoaded.Pop();
                        } while (check(next));
                    }
                    this.actualNode = next;
                    this.actualNode.Status = actualNode.Status!= LoadingStatus.DONE?LoadingStatus.LOADING:LoadingStatus.DONE;
                    this.worker.RunWorkerAsync();
                }
                catch (InvalidOperationException)
                { }
            }
        }

        /// <summary>
        /// Enqueues the given node to be loaded asynchronously
        /// </summary>
        /// <param name="node">to be loaded next</param>
        public void Load(Node node)
        {
            if (node.Path != null)
                lock (lockToBeLoaded) {
                    toBeLoaded.Push (node);
                }
            if (!this.worker.IsBusy)
                Load();
        }

        /// <summary>
        /// Cancels the async loading procedure
        /// </summary>
        public void Cancel()
        {
            this.worker.CancelAsync();
        }

        private void DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            e.Result = this.method(this.repoCredentials, this.actualNode);
            if (worker.CancellationPending)
                e.Cancel = true;
        }

        private void Finished(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                this.actualNode.Status = LoadingStatus.REQUEST_FAILURE;
            }
            else if (e.Cancelled)
            {
                this.actualNode.Status = LoadingStatus.ABORTED;
            }
            else
            {
                this.actualNode.Status = LoadingStatus.DONE;
                MergeFolderTrees(this.actualNode, e.Result as List<Node>);
            }
            UpdateNodeEvent ();
            Load();
        }

        /// <summary>
        /// Merges the given new child list into the existing childs of the given node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="children"></param>
        public static void MergeFolderTrees(Node node, List<Node> children)
        {
            foreach (Node newChild in children)
            {
                try {
                    Node equalNode = node.Children.First(x => x.Name.Equals(newChild.Name));
                    MergeFolderTrees(equalNode, newChild.Children.ToList());
                    MergeNewNodeIntoOldNode(equalNode, newChild);
                } catch ( InvalidOperationException ) {
                    if (node.Selected == false)
                        newChild.Selected = false;
                    node.Children.Add(newChild);
                    newChild.Parent = node;
                }
            }
        }

        /// <summary>
        /// Merges the state of the new node into the old node, excerpt the children
        /// </summary>
        /// <param name="oldNode"></param>
        /// <param name="newNode"></param>
        public static void MergeNewNodeIntoOldNode(Node oldNode, Node newNode)
        {
            oldNode.AddType(newNode.LocationType);
            oldNode.IsIllegalFileNameInPath = oldNode.IsIllegalFileNameInPath || newNode.IsIllegalFileNameInPath;
            oldNode.Path = oldNode.Path != null? oldNode.Path : newNode.Path;
            oldNode.Status = oldNode.Status != LoadingStatus.DONE? newNode.Status: LoadingStatus.DONE;
            oldNode.ThreeStates = oldNode.ThreeStates != newNode.ThreeStates ? true : newNode.ThreeStates;
//            oldNode.Selected = (oldNode.Selected == false || newNode.Selected == false) ? false : oldNode.Selected;
        }
    }

    /// <summary>
    /// Predefined Node Loader contains a few methods, which could be used for loading remote nodes
    /// </summary>
    public static class PredefinedNodeLoader
    {
        /// <summary>
        /// Loads a tree of remote sub folder with the depth of 2
        /// </summary>
        /// <param name="credentials"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        public static List<Node> LoadSubFolderDelegate(CmisRepoCredentials credentials, Node root)
        {
            CmisUtils.FolderTree tree = CmisUtils.GetSubfolderTree(credentials, root.Path, 2);
            List<Node> children = CreateSubTrees(tree.Children, null);
            foreach (Node child in children)
                child.Parent = root;
            return children;
        }

        /// <summary>
        /// Check if the tree for remote <c>root</c> is load finished for the depth of 2
        /// </summary>
        /// <returns><c>true</c>, if sub folder delegate was checked, <c>false</c> otherwise.</returns>
        public static bool CheckSubFolderDelegate(Node root)
        {
            return CheckSubFolder (root, 2);
        }

        private static bool CheckSubFolder(Node root, int level)
        {
            if (root.Status != LoadingStatus.DONE) {
                return false;
            }
            --level;
            if (level > 0) {
                foreach (Node child in root.Children) {
                    if (!CheckSubFolder (child, level - 1)) {
                        return false;
                    }
                }
            }
            return true;
        }

        /*
        /// <summary>
        /// Loads decendants of the given node with the depth of 2
        /// </summary>
        /// <param name="credentials"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        public static List<Node> LoadDecendantsDelegate(CmisRepoCredentials credentials, Node root)
        {
            throw new NotImplementedException();
        }*/

        private static List<Node> CreateSubTrees(List<CmisUtils.FolderTree> children, Node parent)
        {
            List<Node> result = new List<Node>();
            foreach (CmisUtils.FolderTree child in children)
            {
                Folder f = new Folder()
                {
                    Path = child.Path,
                    Name = child.Name,
                    Parent = parent,
                    LocationType = Node.NodeLocationType.REMOTE
                };
                if (child.Finished)
                {
                    f.Status = LoadingStatus.DONE;
                }
                if (child.Children != null)
                {
                    List<Node> subchildren = CreateSubTrees(child.Children, f);
                    foreach (Node subchild in subchildren)
                        f.Children.Add(subchild);
                }
                result.Add(f);
            }
            return result;
        }

    }
}
