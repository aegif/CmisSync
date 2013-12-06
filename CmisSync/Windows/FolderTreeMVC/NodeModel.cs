using CmisSync.Lib.Cmis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CmisSync.CmisTree
{

    /// <summary>
    /// Tree View Node
    /// </summary>
    public class Node : INotifyPropertyChanged
    {
        private Node parent;
        /// <summary>
        /// Parent Node
        /// </summary>
        [DefaultValue(null)]
        public Node Parent { get { return parent; }
            set {
                parent = value;
                if (parent != null && parent.IsIllegalFileNameInPath)
                    this.IsIllegalFileNameInPath = true;
            }
        }
        private bool threestates = false;
        /// <summary>
        /// Gets and sets the ThreeStates capability of Selected Property.
        /// </summary>
        public virtual bool ThreeStates { get { return threestates; } set { SetField(ref threestates, value, "ThreeStates"); } }
        private LoadingStatus status = LoadingStatus.START;
        /// <summary>
        /// Loading status of a folder
        /// </summary>
        public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
        private ObservableCollection<Node> children = new ObservableCollection<Node>();
        /// <summary>
        /// All subfolder of this folder.
        /// </summary>
        public ObservableCollection<Node> Children { get { return children; } }
        /// <summary>
        /// Get folder from <c>SubFolder</c> for the path
        /// </summary>
        private bool? selected = true;
        /// <summary>
        /// Sets and gets the Selected Property. If true is set, all children will also be selected,
        /// if false, none of the children is selected. If none, at least one of the children is not selected and at least one is selected
        /// </summary>
        public virtual bool? Selected
        {
            get { return selected; }
            set
            {
                if (SetField(ref selected, value, "Selected"))
                {
                    if (selected == null)
                    {
                        this.ThreeStates = true;
                    }
                    else if (selected == true)
                    {
                        this.ThreeStates = false;
                        foreach (Node child in Children)
                        {
                            child.Selected = true;
                        }
                        Node p = this.Parent;
                        while (p != null)
                        {
                                if (p.Selected == null || p.Selected == false)
                                {
                                    bool allSelected = true;
                                    foreach (Node childOfParent in p.Children)
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
                                    else
                                    {
                                        p.ThreeStates = true;
                                        p.Selected = null;
                                    }
                                }
                                p = p.Parent;
                            }
                        OnPropertyChanged("IsIgnored");
                    }
                    else
                    {
                        this.ThreeStates = false;
                        Node p = Parent;
                        while (p != null && p.Selected == true)
                        {
                            p.Selected = null;
                            p = p.Parent;
                        }
                        foreach (Node child in Children)
                        {
                            child.Selected = selected;
                        }
                        OnPropertyChanged("IsIgnored");
                    }
                }
            }
        }

        private string name;
        /// <summary>
        /// The name of the folder
        /// </summary>
        public string Name { get { return name; } set { SetField(ref name, value, "Name"); } }
        private string path;
        /// <summary>
        /// The absolut path of the folder
        /// </summary>
        public virtual string Path { get { return path; } set { SetField(ref path, value, "Path"); } }
        /// <summary>
        /// Sets and gets the Ignored status of a folder
        /// </summary>
        public bool IsIgnored { get { return Selected == false; } }

        private bool illegalFileNameInPath = false;
        /// <summary>
        /// If the path or name contains any illegal Pattern, switch prevends from synchronization, this property is set to true
        /// </summary>
        public bool IsIllegalFileNameInPath { get { return illegalFileNameInPath; } 
            set {
                SetField(ref illegalFileNameInPath, value, "IsIllegalFileNameInPath");
                if(illegalFileNameInPath)
                {
                    foreach (Node child in children)
                        child.IsIllegalFileNameInPath = true;
                }
            }
        }

        private bool enabled = true;
        /// <summary>
        /// Sets and gets the Enabled state of a node.
        /// If a state is changed, also its child node are set to the same state.
        /// </summary>
        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (SetField(ref enabled, value, "Enabled"))
                {
                    foreach (Node child in children)
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
            get { return expanded; }
            set { expanded = value; }
        }

        private NodeLocationType locationType = NodeLocationType.REMOTE;
        /// <summary>
        /// The location type of a folder can be any NodeLocationType
        /// </summary>
        public NodeLocationType LocationType { get { return locationType; } set { SetField(ref locationType, value, "LocationType"); } }
        /// <summary>
        /// Add a location type
        /// <c>NodeLocationType.REMOTE</c> + <c>NodeLocationType.LOCAL</c> = <c>NodeLocationType.BOTH</c>
        /// </summary>
        /// <param name="type"></param>
        public void AddType(NodeLocationType type)
        {
            switch (locationType)
            {
                case NodeLocationType.NONE:
                    LocationType = type;
                    break;
                case NodeLocationType.LOCAL:
                    if (type == NodeLocationType.REMOTE || type == NodeLocationType.BOTH)
                        LocationType = NodeLocationType.BOTH;
                    break;
                case NodeLocationType.REMOTE:
                    if (type == NodeLocationType.LOCAL || type == NodeLocationType.BOTH)
                        LocationType = NodeLocationType.BOTH;
                    break;
            }
        }

        /// <summary>
        /// Enumaration of all possible location Types for a Node. It can be Remote, Local, or Both.
        /// </summary>
        public enum NodeLocationType
        {
            /// <summary>
            /// The node does not exists remote or local
            /// </summary>
            NONE,
            /// <summary>
            /// The node exists locally
            /// </summary>
            LOCAL,
            /// <summary>
            /// The node exists remotely
            /// </summary>
            REMOTE,
            /// <summary>
            /// The node exists locally and remotely
            /// </summary>
            BOTH
        }

        // boiler-plate
        /// <summary>
        /// If any property changes, this event will be informed
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Execute this if the property with the given propertyName has been changed.
        /// </summary>
        /// <param name="propertyName"></param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
        /// <summary>
        /// Helper Method to change a property and this method informs the PropertyChangeEventHandler
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Tooltip informations about this node. It returns the Path of the node
        /// </summary>
        public virtual string ToolTip { get { return Path; } }

    }

    /// <summary>
    /// Root node for a synchronized folder. It contains the local and the remote path
    /// </summary>
    public class RootFolder : Node
    {

        private string address;

        /// <summary>
        /// Gets and sets the unique repository id. A change would not be propagated to listener
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Readonly Path of the repository.
        /// </summary>
        public override string Path { get { return "/"; } }

        private string localPath;

        /// <summary>
        /// Local path to the synchronization root folder
        /// </summary>
        public string LocalPath { get { return localPath; } set { SetField(ref localPath, value, "LocalPath"); } }

        /// <summary>
        /// The URL of the repository. 
        /// </summary>
        public string Address { get { return address; } set { SetField(ref address, value, "Address"); } }
        /// <summary>
        /// Tooltip informations about this repo. It returns the Id and the address of the repo
        /// </summary>
        public override string ToolTip { get { return "URL: \"" + Address + "\"\r\nRepository ID: \"" + Id + "\""; } }

        /// <summary>
        /// Overrides the ThreeStates base method to read only
        /// </summary>
        public override bool ThreeStates { get { return false; } set { base.ThreeStates = false; } }

        /// <summary>
        /// Overrides the selection mode of node by returning only false and true
        /// Other possiblities could be possible in the future, but at the moment, only selected or not are valid results
        /// </summary>
        public override bool? Selected { get { return base.Selected != false; } set { base.Selected = value; } }
    }

    /// <summary>
    /// Folder data structure for WPF Control
    /// </summary>
    public class Folder : Node
    {

        /// <summary>
        /// Default constructor. All properties must be manually set.
        /// </summary>
        public Folder() { }

        /// <summary>
        /// Get folder from <c>SubFolder</c> for the path
        /// </summary>
        public static Folder GetSubFolder(string path, Folder f)
        {
            foreach (Folder folder in f.Children)
            {
                if (folder.Path.Equals(f.Path))
                {
                    return folder;
                }
            }
            return null;
        }

        /// <summary>
        /// Return the folder list when <c>Selected</c> is <c>false</c>
        /// </summary>
        /// <returns></returns>
        public static List<string> GetIgnoredFolder(Folder f)
        {
            List<string> result = new List<string>();
            if (f.IsIgnored)
            {
                result.Add(f.Path);
            }
            else
            {
                foreach (Folder child in f.Children)
                    result.AddRange(GetIgnoredFolder(child));
            }
            return result;
        }


        /// <summary>
        /// Return the folder list when <c>Selected</c> is <c>true</c>
        /// </summary>
        /// <returns></returns>
        public static List<string> GetSelectedFolder(Folder f)
        {
            List<string> result = new List<string>();
            if (f.Selected == true)
                result.Add(f.Path);
            else
            {
                foreach (Folder child in f.Children)
                    result.AddRange(GetSelectedFolder(child));
            }
            return result;
        }
    }

}
