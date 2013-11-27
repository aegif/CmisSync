using System;
using System.Collections.Generic;
using System.Linq;

using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;

using log4net;

namespace CmisSync.CmisTree
{
    public class NSCmisTree : NSObject {
        public string Name = string.Empty;
        public NSCmisTree Parent = null;
        public IList<NSCmisTree> Children = new List<NSCmisTree> ();
        public override string ToString()
        {
            string path = Name;
            NSCmisTree parent = Parent;
            while (parent != null) {
                path = parent.Name + "/" + path;
                parent = parent.Parent;
            }
            return path;
        }
        public NSCmisTree(Node root)
        {
            Name = root.Name;
            Parent = null;
            foreach (Node node in root.Children) {
                NSCmisTree child = new NSCmisTree (node);
                child.Parent = this;
                Children.Add (child);
            }
        }
        public NSCmisTree()
        {
        }
    }


    [Register("CmisTreeDataSource")]
    public class CmisTreeDataSource : NSOutlineViewDataSource
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(CmisTreeDataSource));

        protected IList<NSCmisTree> Repositories = new List<NSCmisTree>();
        protected Object LockRepositories = new Object();

        public void UpdateCmisTree(RootFolder root)
        {
            lock (LockRepositories) {
                for (int i = 0; i < Repositories.Count; ++i) {
                    if (Repositories [i].Name == root.Name) {
                        Repositories [i] = new NSCmisTree (root);
                        return;
                    }
                }
                Repositories.Add (new NSCmisTree (root));
            }
        }

        public CmisTreeDataSource (List<RootFolder> repositories)
        {
            foreach (RootFolder root in repositories) {
                UpdateCmisTree(root);
            }
        }

        private void UpdateItem(ref NSObject item)
        {
            lock (LockRepositories) {
                if (item == null) {
                    return;
                }
                NSCmisTree cmis = item as NSCmisTree;
                if (cmis == null) {
                    return;
                }

                Stack<string> paths = new Stack<string> ();
                do {
                    paths.Push(cmis.Name);
                    cmis = cmis.Parent;
                } while (cmis != null);

                string current = paths.Pop ();
                cmis = Repositories.First (x => x.Name.Equals (current));
                if (cmis == null) {
                    return;
                }

                while (paths.Count > 0) {
                    current = paths.Pop ();
                    cmis = cmis.Children.First (x => x.Name.Equals (current));
                    if (cmis == null) {
                        return;
                    }
                }

                item = cmis;
            }
        }

        public override NSObject GetChild(NSOutlineView outlineView, int childIndex, NSObject item)
        {
            lock (LockRepositories) {
                UpdateItem (ref item);
                Console.WriteLine ("GetChild " + item);
                if (item == null) {
                    return Repositories [childIndex];
                }
                NSCmisTree cmis = item as NSCmisTree;
                if (cmis == null) {
                    Console.WriteLine ("GetChild Error");
                    return null;
                }
                return cmis.Children [childIndex];
            }
        }

        public override bool ItemExpandable(NSOutlineView outlineView, NSObject item)
        {
            lock (LockRepositories) {
                UpdateItem (ref item);
                Console.WriteLine ("ItemExpandable " + item);
                if (item == null) {
                    return Repositories.Count > 0;
                }
                NSCmisTree cmis = item as NSCmisTree;
                if (cmis == null) {
                    Console.WriteLine ("ItemExpandable Error");
                    return false;
                }
                Console.WriteLine ("ItemExpandable " + cmis.Name + " " + cmis.Children.Count);
                return cmis.Children.Count > 0;
            }
        }

        public override int GetChildrenCount(NSOutlineView outlineView, NSObject item)
        {
            lock (LockRepositories) {
                UpdateItem (ref item);
                Console.WriteLine ("GetChildrenCount " + item);
                if (item == null) {
                    return Repositories.Count;
                }
                NSCmisTree cmis = item as NSCmisTree;
                if (cmis == null) {
                    Console.WriteLine ("GetChildrenCount Error");
                    return 0;
                }
                return cmis.Children.Count;
            }
        }

        public override NSObject GetObjectValue(NSOutlineView outlineView, NSTableColumn tableColumn, NSObject item)
        {
            lock (LockRepositories) {
                UpdateItem (ref item);
                Console.WriteLine ("GetObjectValue " + item);
                if (item == null) {
                    return (NSString)"remote";
                }
                NSCmisTree cmis = item as NSCmisTree;
                if (cmis == null) {
                    Console.WriteLine ("GetObjectValue Error");
                    return (NSString)"";
                }
                return (NSString)cmis.Name;
            }
        }

        /*[Export("outlineView:setObjectValue:forTableColumn:byItem:")]
        public virtual void SetObjectValue(NSOutlineView outlineView, NSObject theObject, NSTableColumn tableColumn, NSObject item)
        {
        }*/
    }
}

