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
        /// <summary>
        /// Parent Node
        /// </summary>
        [DefaultValue(null)]
        public Node Parent { get; set; }
        private bool threestates = false;
        /// <summary>
        /// Gets and sets the ThreeStates capability of Selected Property.
        /// </summary>
        public bool ThreeStates { get { return threestates; } set { SetField(ref threestates, value, "ThreeStates"); } }
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
                        this.IsIgnored = false;
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
                                    else if (p.Children.Count > 1)
                                    {
                                        p.ThreeStates = true;
                                        p.Selected = null;
                                        p.IsIgnored = false;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                p = p.Parent;
                            }
                    }
                    else
                    {
                        this.ThreeStates = false;
                        Node p = Parent;
                        while (p != null)
                        {
                            if (p.Selected == true)
                            {
                                IsIgnored = true;
                                p.Selected = null;
                            }
                            p = p.Parent;
                        }
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
        private bool ignored = false;
        /// <summary>
        /// Sets and gets the Ignored status of a folder
        /// </summary>
        public bool IsIgnored
        {
            get { return ignored; }
            set
            {
                if (SetField(ref ignored, value, "IsIgnored"))
                {
                    if (ignored)
                    {
                        foreach (Node child in Children)
                        {
                            child.Selected = false;
                        }
                    }
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
            get { return enabled; }
            set { expanded = value; }
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
        public string Address { get { return address; } }
        /// <summary>
        /// Tooltip informations about this repo. It returns the Id and the address of the repo
        /// </summary>
        public override string ToolTip { get { return "URL: \"" + Address + "\"\r\nRepository ID: \"" + Id + "\""; } }

        private bool automaticSyncAllNewSubfolder = true;
        /// <summary>
        /// If all subfolder should be synchronized, this is true, if not, new folder on the the root directory
        /// must be selected manually. 
        /// TODO this feature is whether tested not correctly implemented. Please do not set it, or fix it.
        /// </summary>
        public bool SyncAllSubFolder
        {
            get { return automaticSyncAllNewSubfolder; }
            set
            {
                if (SetField(ref automaticSyncAllNewSubfolder, value, "SyncAllSubFolder"))
                {
                    if (automaticSyncAllNewSubfolder)
                    {
                        foreach (Node subfolder in Children)
                        {
                            if (subfolder.Selected == false)
                            {
                                subfolder.IsIgnored = true;
                            }
                        }
                    }
                    else
                    {
                        foreach (Node subfolder in Children)
                        {
                            if (subfolder.IsIgnored)
                            {
                                subfolder.IsIgnored = false;
                                subfolder.Selected = false;
                            }
                        }
                    }
                }
            }
        }
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
        private FolderType folderType = FolderType.REMOTE;
        /// <summary>
        /// The Type of a folder can be any FolderType
        /// </summary>
        public FolderType Type { get { return folderType; } set { SetField(ref folderType, value, "Type"); } }
        /// <summary>
        /// Add a type
        /// <c>FolderType.REMOTE</c> + <c>FolderType.LOCAL</c> = <c>FolderType.BOTH</c>
        /// </summary>
        /// <param name="type"></param>
        public void AddType(FolderType type)
        {
            if (folderType == FolderType.BOTH)
            {
                return;
            }
            if (folderType == FolderType.NONE)
            {
                Type = type;
                return;
            }
            if (type == FolderType.BOTH)
            {
                Type = type;
                return;
            }
            if (folderType == FolderType.LOCAL)
            {
                if (type == FolderType.REMOTE)
                {
                    Type = FolderType.BOTH;
                }
                return;
            }
            if (folderType == FolderType.REMOTE)
            {
                if (type == FolderType.LOCAL)
                {
                    Type = FolderType.BOTH;
                }
                return;
            }
        }

        /// <summary>
        /// Enumaration of all possible Folder Types. It can be Remote, Local, or Both.
        /// </summary>
        public enum FolderType
        {
            NONE, LOCAL, REMOTE, BOTH
        }
    }

}
