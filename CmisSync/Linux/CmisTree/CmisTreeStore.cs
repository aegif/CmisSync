using System;

using Gtk;

namespace CmisSync.CmisTree
{
    public class CmisTreeStore
    {
        public CmisTreeStore ()
        {
            CmisStore = new TreeStore (typeof(string), typeof(Node));
        }
        public void UpdateCmisTree(RootFolder root)
        {
            TreeIter iter;
            if (CmisStore.GetIterFirst (out iter))
            {
                do
                {
                    string name = CmisStore.GetValue(iter, 0) as string;
                    if (name == null)
                    {
                        Console.WriteLine("UpdateCmisTree GetValue Error");
                        return;
                    }
                    if (name == root.Name)
                    {
                        UpdateCmisTreeNode(iter, root);
                        return;
                    }
                } while (CmisStore.IterNext(ref iter));
            }
            iter = CmisStore.AppendNode ();
            UpdateCmisTreeNode(iter, root);
            return;
        }
        public TreeStore CmisStore { get; set; }

        private void UpdateCmisTreeNode (TreeIter iter, Node node)
        {
            CmisStore.SetValues (iter, node.Name, node);
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
                    string name = CmisStore.GetValue (iter, 0) as string;
                    Node node = CmisStore.GetValue (iter, 1) as Node;
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
            CmisStore.SetValues (iterChild, child.Name, child);
        }
    }
}

