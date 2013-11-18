using System;
using System.Collections.Generic;
using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;


namespace CmisSync.CmisTree
{
    public class NSNodeObject : NSObject {
        public Node Node{ get; set; }
    }


    [Register("CmisTreeDataSource")]
    public class CmisTreeDataSource : NSOutlineViewDataSource
    {
        private RootFolder[] Repositories;

        public CmisTreeDataSource (List<RootFolder> repositories)
        {
            this.Repositories = new RootFolder[repositories.Count];
            int i = 0;
            foreach (RootFolder repo in repositories) {
                this.Repositories[i] = repo;
                i++;
            }
        }

        [Export("outlineView:child:ofItem:")]
        public virtual NSObject GetChild(NSOutlineView outlineView, int childIndex, NSObject item)
        {
            if (item == null)
            {
                return new NSNodeObject(){Node = Repositories[childIndex]};
            }
            NSNodeObject node = item as NSNodeObject;
            if (node != null)
            {
                Node child = node.Node.Children[childIndex];
                return new NSNodeObject(){Node = child};
            }

            return null;
        }

        [Export("outlineView:isItemExpandable:")]
        public virtual bool ItemExpandable(NSOutlineView outlineView, NSObject item)
        {
            if (item == null)
                return false;
            NSNodeObject node = item as NSNodeObject;
            if (node != null)
            {
                if (node.Node.Children.Count > 0)
                    return true;
                else
                    return false;
            }
            return false;
        }

        [Export("outlineView:numberOfChildrenOfItem:")]
        public virtual int GetChildrenCount(NSOutlineView outlineView, NSObject item)
        {
            if (item == null)
                return Repositories.Length;
            NSNodeObject node = item as NSNodeObject;
            if (node != null)
                return node.Node.Children.Count;
            return 0;
        }

        [Export("outlineView:objectValueForTableColumn:byItem:")]
        public virtual NSObject GetObjectValue(NSOutlineView outlineView, NSTableColumn tableColumn, NSObject item)
        {
            NSNodeObject node = item as NSNodeObject;
            if (node != null)
                return (NSString)node.Node.Name;
            return (NSString)"No Node found";
        }
    }
}

