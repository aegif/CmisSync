using CmisSync.Lib.Credentials;
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
        private LoadChildrenDelegate method;

        public delegate List<Node> LoadChildrenDelegate(CmisRepoCredentials credentials, Node root );

        public AsyncNodeLoader(RootFolder root, CmisRepoCredentials credentials, LoadChildrenDelegate method)
        {
            this.root = root;
            repoCredentials = credentials;
            this.actualNode = null;
            this.worker = new BackgroundWorker();
            this.method = method;
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
                    Node next = this.toBeLoaded.Pop();
                    this.actualNode = next;
                    this.actualNode.Status = LoadingStatus.LOADING;
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
            if(node.Status != LoadingStatus.DONE)
                toBeLoaded.Push(node);
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
                    MergeNewNodeIntoOldNode(equalNode, newChild);
                    MergeFolderTrees(equalNode, newChild.Children.ToList());
                } catch ( InvalidOperationException ) {
                    node.Children.Add(newChild);
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
            oldNode.IsIgnored = oldNode.IsIgnored || newNode.IsIgnored;
            oldNode.Status = newNode.Status;
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
            CmisUtils.NodeTree tree = CmisUtils.GetSubfolderTree(credentials, root.Path, 2);
            List<Node> children = CreateSubTrees(tree.Children, null);
            foreach (Node child in children)
                child.Parent = root;
            return children;
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

        private static List<Node> CreateSubTrees(List<CmisUtils.NodeTree> children, Node parent)
        {
            List<Node> result = new List<Node>();
            foreach (CmisUtils.NodeTree child in children)
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
