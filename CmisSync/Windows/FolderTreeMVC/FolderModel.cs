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
    /// Folder data structure for WPF Control
    /// </summary>
    public class Folder : INotifyPropertyChanged
    {

        /// <summary>
        /// Return the folder list when <c>Selected</c> is <c>false</c>
        /// </summary>
        /// <returns></returns>
        public List<string> GetIgnoredFolder()
        {
            List<string> result = new List<string>();
            if (IsIgnored)
            {
                result.Add(Path);
            }
            else
            {
                foreach (Folder child in SubFolder)
                    result.AddRange(child.GetIgnoredFolder());
            }
            return result;
        }


        /// <summary>
        /// Return the folder list when <c>Selected</c> is <c>true</c>
        /// </summary>
        /// <returns></returns>
        public List<string> GetSelectedFolder()
        {
            List<string> result = new List<string>();
            if (Selected == true)
                result.Add(Path);
            else
            {
                foreach (Folder child in SubFolder)
                    result.AddRange(child.GetSelectedFolder());
            }
            return result;
        }


        /// <summary>
        /// Constructor if the whole FolderTree is loaded. It creates a folder tree recursivly.
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="repo"></param>
        public Folder(CmisUtils.FolderTree tree, CmisRepo repo, Folder old)
        {
            this.Path = tree.path;
            this.Repo = repo;
            this.Name = tree.Name;
            this.Type = FolderType.REMOTE;
            if (tree.Finished)
            {
                this.Status = LoadingStatus.DONE;
            }
            else
            {
                this.Status = LoadingStatus.START;
                Debug.Assert(false, "Whole CMIS folder tree should be queried");
            }
            this.Enabled = repo.Selected;
            foreach (CmisUtils.FolderTree t in tree.children)
            {
                Folder oldSub = null;
                if (old != null)
                {
                    oldSub = old.GetSubFolder(t.path);
                }
                this.SubFolder.Add(new Folder(t, Repo, oldSub) { Parent = this });
            }

            //  Support local folder in old
            if (old != null)
            {
                this.AddType(old.Type);
                this.Selected = old.Selected;
                foreach (Folder o in old.SubFolder)
                {
                    if (null == GetSubFolder(o.path))
                    {
                        this.SubFolder.Add(o);
                    }
                }
            }
        }
        /// <summary>
        /// Default constructor. All properties must be manually set.
        /// </summary>
        public Folder() { }

        private CmisRepo repo;
        /// <summary>
        /// Parent Object of a Folder
        /// </summary>
        public object Parent { get; set; }
        /// <summary>
        /// Repository from where the folder has been loaded
        /// </summary>
        public CmisRepo Repo { get { return repo; } set { SetField(ref repo, value, "Repo"); } }
        private bool threestates = false;
        /// <summary>
        /// Gets and sets the ThreeStates capability of Selected Property.
        /// </summary>
        public bool ThreeStates { get { return threestates; } set { SetField(ref threestates, value, "ThreeStates"); } }
        private bool? selected = true;
        /// <summary>
        /// Sets and gets the Selected Property. If true is set, all children will also be selected,
        /// if false, none of the children is selected. If none, at least one of the children is not selected and at least one is selected
        /// </summary>
        public bool? Selected
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
                        foreach (Folder subfolder in SubFolder)
                        {
                            subfolder.Selected = true;
                        }
                        Folder p = this.Parent as Folder;
                        while (p != null)
                        {
                            if (p.Selected == null || p.Selected == false)
                            {
                                bool allSelected = true;
                                foreach (Folder childOfParent in p.SubFolder)
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
                                else if (p.SubFolder.Count > 1)
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
                            p = p.Parent as Folder;
                        }
                    }
                    else
                    {
                        this.ThreeStates = false;
                        Folder p = this.Parent as Folder;
                        bool found = false;
                        while (p != null)
                        {
                            if (p.Selected == true)
                            {
                                this.IsIgnored = true;
                                found = true;
                                p.Selected = null;
                            }
                            p = p.Parent as Folder;
                        }
                        if (!found)
                        {
                            if (this.Repo.SyncAllSubFolder)
                            {
                                this.IsIgnored = true;
                            }
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
        public string Path { get { return path; } set { SetField(ref path, value, "Path"); } }
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
                        foreach (Folder child in SubFolder)
                        {
                            child.Selected = false;
                        }
                    }
                }
            }
        }
        private bool enabled = true;
        /// <summary>
        /// Sets and gets the Enabled state of a folder.
        /// If a state is changed, also its child folders are set to the same state.
        /// </summary>
        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (SetField(ref enabled, value, "Enabled"))
                {
                    foreach (Folder child in SubFolder)
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
            set
            {
                expanded = value;
                if (value)
                {
                    this.Repo.SelectFolder = this;
                }
                else
                {
                    if (this.Repo.SelectFolder == this)
                    {
                        this.Repo.SelectFolder = null;
                    }
                }
            }
        }
        private LoadingStatus status = LoadingStatus.START;
        /// <summary>
        /// Loading status of a folder
        /// </summary>
        public LoadingStatus Status { get { return status; } set { SetField(ref status, value, "Status"); } }
        private ObservableCollection<Folder> _subfolder = new ObservableCollection<Folder>();
        /// <summary>
        /// All subfolder of this folder.
        /// </summary>
        public ObservableCollection<Folder> SubFolder { get { return _subfolder; } }
        /// <summary>
        /// Get folder from <c>SubFolder</c> for the path
        /// </summary>
        public Folder GetSubFolder(string path)
        {
            foreach (Folder folder in SubFolder)
            {
                if (folder.path == path)
                {
                    return folder;
                }
            }
            return null;
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

        // boiler-plate
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

}
