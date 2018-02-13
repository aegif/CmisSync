using System;
using System.IO;
using CmisSync.Lib.Cmis;
using log4net;
using DotCMIS.Client;
using CmisSync.Lib.Database;


namespace CmisSync.Lib.Sync.SynchronizeItem
{
    /// <summary></summary>
    abstract public class SyncItem
    {
        // Log.
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(SyncItem));

        // The examples below are for this item:
        //
        // Local: C:\Users\nico\CmisSync\A Project\adir\afile.txt
        // Remote: /sites/aproject/adir/a<file
        //
        // Notice how:
        // - Slashes and antislashes can differ
        // - File names can differ
        // - Remote and local have different sets of fobidden characters
        //
        // For that reason, never convert a local path to a remote path (or vice versa) without checking the database.

        /// <summary>
        /// Local root of the collection.
        /// Example: C:\Users\nico\CmisSync\A Project
        /// </summary>
        protected string localRoot;

        /// <summary>
        /// Remote root of the collection.
        /// Example: /sites/aproject
        /// </summary>
        protected string remoteRoot;

        /// <summary>
        /// Local path of the item, relative to the local root
        /// Example: adir\afile.txt
        /// </summary>
        protected string localRelativePath;

        /// <summary>
        /// Remote path of the item, relative to the remote root
        /// Example: adir/a&lt;file
        /// </summary>
        protected string remoteRelativePath;

        /// <summary>
        /// Whether the item is a folder or a file.
        /// </summary>
        protected bool isFolder;
        public bool IsFolder
        {
            get
            {
                return isFolder;
            }
        }

        /// <summary>
        /// Reference to the CmisSync database.
        /// It is useful to get the remote path that matches a local path, or vice versa
        /// </summary>
        protected Database.Database database;

        /// <summary></summary>
        abstract public string LocalRelativePath
        {
            get;
        }

        /// <summary></summary>
        abstract public string RemoteRelativePath
        {
            get;
        }

        /// <summary></summary>
        abstract public string LocalPath
        {
            get;
        }

        /// <summary></summary>
        abstract public string RemotePath
        {
            get;
        }

        /// <summary></summary>
        abstract public string LocalLeafname
        {
            get;
        }

        /// <summary></summary>
        abstract public string RemoteLeafname
        {
            get;
        }

        /// <summary>
        /// Whether the file exists locally.
        /// </summary>
        virtual public bool FileExistsLocal()
        {
            bool exists = File.Exists(LocalPath);
            Logger.Debug("File.Exists(" + LocalPath + ") = " + exists);
            return exists;
        }
    }







}

