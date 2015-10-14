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
        public LoadingStatus Status = LoadingStatus.START;
        public bool? Selected = true;
        public NSCmisTree Parent = null;
        public IList<NSCmisTree> Children = new List<NSCmisTree> ();
        public string FullPath = string.Empty;

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
            FullPath = root.Path;
            Status = root.Status;
            Selected = root.Selected;
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
        public Node GetNode(Node root)
        {
            NSCmisTree cmis = this;
            Stack<string> paths = new Stack<string> ();
            do {
                paths.Push(cmis.Name);
                cmis = cmis.Parent;
            } while (cmis != null);

            Node node = root;
            do {
                string current = paths.Pop ();
                if (current != node.Name) {
                    return null;
                }
                if (paths.Count == 0) {
                    return node;
                }
                node = node.Children.First (x => x.Name.Equals (paths.Peek()));
            } while (true);
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
            UpdateItem (ref item);
//            Console.WriteLine ("GetChild " + item);
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

        public override bool ItemExpandable(NSOutlineView outlineView, NSObject item)
        {
            UpdateItem (ref item);
//            Console.WriteLine ("ItemExpandable " + item);
            if (item == null) {
                return Repositories.Count > 0;
            }
            NSCmisTree cmis = item as NSCmisTree;
            if (cmis == null) {
                Console.WriteLine ("ItemExpandable Error");
                return false;
            }
//            Console.WriteLine ("ItemExpandable " + cmis.Name + " " + cmis.Children.Count);
            if (cmis.Parent == null && cmis.Selected == false) {
                return false;
            }
            return cmis.Children.Count > 0;
        }

        // Get the number of children in "item"
        public override int GetChildrenCount(NSOutlineView outlineView, NSObject item)
        {
            UpdateItem (ref item);
//            Console.WriteLine ("GetChildrenCount " + item);
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

        public override NSObject GetObjectValue(NSOutlineView outlineView, NSTableColumn tableColumn, NSObject item)
        {
            UpdateItem (ref item);
//            Console.WriteLine ("GetObjectValue " + item);
            if (item == null) {
                return (NSString)"";
            }
            NSCmisTree cmis = item as NSCmisTree;
            if (cmis == null) {
                Console.WriteLine ("GetObjectValue Error");
                return (NSString)"";
            }
            if (tableColumn.Identifier.Equals("Name")) {
                // Console.WriteLine ("GetObjectValue " + item);
                return (NSString)cmis.Name;
                /*
                switch (cmis.Selected) {
                case true:
                    return new NSNumber (1);
                case false:
                    return new NSNumber (0);
                case null:
                    return new NSNumber (-1);
                }
                return (NSString)cmis.Name;
                */
            }
            if (tableColumn.Identifier.Equals ("Status")) {
                switch (cmis.Status) {
                case LoadingStatus.START:
                    return (NSString)Properties_Resources.LoadingStatusSTART;
                case LoadingStatus.LOADING:
                    return (NSString)Properties_Resources.LoadingStatusLOADING;
                case LoadingStatus.ABORTED:
                    return (NSString)Properties_Resources.LoadingStatusABORTED;
                default:
                    return (NSString)"";
                }
                return (NSString)"";
            }
            Console.WriteLine ("GetObjectValue Error");
            return (NSString)"";
        }

        public override void SetObjectValue(NSOutlineView outlineView, NSObject theObject, NSTableColumn tableColumn, NSObject item)
        {
            UpdateItem (ref item);
            Console.WriteLine ("SetObjectValue " + item + ": " + theObject);
            if (item == null) {
                Console.WriteLine ("SetObjectValue null Error");
                return;
            }
            NSCmisTree cmis = item as NSCmisTree;
            if (cmis == null) {
                Console.WriteLine ("SetObjectValue Error");
                return;
            }
            if (tableColumn.Identifier.Equals ("Name")) {
                NSNumber number = (NSNumber)theObject;
                if (number == null) {
                    Console.WriteLine ("SetObjectValue number Error");
                    return;
                }
                SelectedEvent (cmis, number.IntValue);
                return;
            }
            return;
        }

        public delegate void SetSelectedDelegate (NSCmisTree cmis, int selected);
        public event SetSelectedDelegate SelectedEvent = delegate(NSCmisTree cmis, int selected) { };
    }
}

