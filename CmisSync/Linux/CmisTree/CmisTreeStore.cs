using System;

using Gtk;

namespace CmisSync.CmisTree
{
    public class CmisTreeStore
    {
        public enum Column : int
        {
            ColumnNode = 0,
            ColumnName = 1,
            ColumnRoot = 2,
            ColumnSelected = 3,
            ColumnSelectedThreeState = 4,
            ColumnStatus = 5,
            NumberColumn = 6,
        };

        public TreeStore CmisStore { get; set; }

        private object lockCmisStore = new object();

        public CmisTreeStore ()
        {
            CmisStore = new TreeStore (typeof(Node), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(string));
        }

        public void UpdateCmisTree(RootFolder root)
        {
            lock (lockCmisStore)
            {
                TreeIter iter;
                if (CmisStore.GetIterFirst (out iter))
                {
                    do
                    {
                        string name = CmisStore.GetValue (iter, (int)Column.ColumnName) as string;
                        if (name == null)
                        {
                            Console.WriteLine ("UpdateCmisTree GetValue Error");
                            return;
                        }
                        if (name == root.Name)
                        {
                            UpdateCmisTreeNode (iter, root);
                            return;
                        }
                    } while (CmisStore.IterNext(ref iter));
                }
                iter = CmisStore.AppendNode ();
                UpdateCmisTreeNode (iter, root);
                return;
            }
        }

        private void UpdateCmisTreeNode (TreeIter iter, Node node)
        {
//            Node oldNode = CmisStore.GetValue (iter, (int)Column.ColumnNode) as Node;
//            if (oldNode != node)
//            {
//                CmisStore.SetValue (iter, (int)Column.ColumnNode, node);
//            }
//            string oldName = CmisStore.GetValue (iter, (int)Column.ColumnName) as string;
//            string newName = node.Name;
//            if (oldName != newName)
//            {
//                CmisStore.SetValue (iter, (int)Column.ColumnName, newName);
//            }
//            bool oldRoot = (bool)CmisStore.GetValue (iter, (int)Column.ColumnRoot);
//            bool newRoot = (node.Parent == null);
//            if (oldRoot != newRoot)
//            {
//                CmisStore.SetValue (iter, (int)Column.ColumnRoot, newRoot);
//            }
//            bool oldSelected = (bool)CmisStore.GetValue (iter, (int)Column.ColumnSelected);
//            bool newSelected = (node.Selected != false);
//            if (oldSelected != newSelected)
//            {
//                CmisStore.SetValue (iter, (int)Column.ColumnSelected, newSelected);
//            }
//            bool oldSelectedThreeState = (bool)CmisStore.GetValue (iter, (int)Column.ColumnSelectedThreeState);
//            bool newSelectedThreeState = (node.Selected == null);
//            if (oldSelectedThreeState != newSelectedThreeState)
//            {
//                CmisStore.SetValue (iter, (int)Column.ColumnSelectedThreeState, newSelectedThreeState);
//            }
//            string oldStatus = CmisStore.GetValue (iter, (int)Column.ColumnStatus) as string;
            string newStatus = "";
            switch (node.Status) {
            case LoadingStatus.START:
                newStatus = Properties_Resources.LoadingStatusSTART;
                break;
            case LoadingStatus.LOADING:
                newStatus = Properties_Resources.LoadingStatusLOADING;
                break;
            case LoadingStatus.ABORTED:
                newStatus = Properties_Resources.LoadingStatusABORTED;
                break;
            default:
                newStatus = "";
                break;
            }
//            if (oldStatus != newStatus)
//            {
//                CmisStore.SetValue (iter, (int)Column.ColumnStatus, newStatus);
//            }

            CmisStore.SetValues (iter, node, node.Name, node.Parent == null, node.Selected != false, node.Selected == null, newStatus);
            foreach (Node child in node.Children)
            {
                TreeIter iterChild;
                GetChild (iter, out iterChild, child);
                UpdateCmisTreeNode (iterChild, child);
            }
            return;
        }

        private void GetChild (TreeIter iterParent, out TreeIter iterChild, Node child)
        {
            TreeIter iter;
            if (CmisStore.IterChildren (out iter, iterParent))
            {
                do
                {
                    string name = CmisStore.GetValue (iter, (int)Column.ColumnName) as string;
                    Node node = CmisStore.GetValue (iter, (int)Column.ColumnNode) as Node;
                    if (name == child.Name)
                    {
                        if (node != child)
                        {
                            Console.WriteLine ("GetChild Error " + name);
                        }
                        iterChild = iter;
                        return;
                    }
                } while (CmisStore.IterNext(ref iter));
            }
            iterChild = CmisStore.AppendNode (iterParent);
        }
    }
}

