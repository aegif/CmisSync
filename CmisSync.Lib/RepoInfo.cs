using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using CmisSync.Lib.Cmis;
using CmisSync.Auth;
using CmisSync.Lib.Config;

namespace CmisSync.Lib
{
    /// <summary>
    /// All the info for a particular CmisSync synchronized folder.
    /// Contains local info, as well as remote info to connect to the CMIS folder.
    /// </summary>
    public class RepoInfo
    {
        /// <summary>
        /// Name of the local directory within the CmisSync directory.
        /// For instance: "User Homes"
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// Full path to the local database.
        /// For instance: C:\\Users\\win7pro32bit\\AppData\\Roaming\\cmissync\\User Homes.cmissync
        /// </summary>
        public string CmisDatabase { get; set; }


        /// <summary>
        /// Path on the remote server, starting from the root of the CMIS repository.
        /// For instance: /User Homes or /Sites/mysite/myfolder
        /// </summary>
        public string RemotePath { get; set; }


        private CmisRepoCredentials credentials = new CmisRepoCredentials();
        /// <summary>
        /// Address of the server's CMIS endpoint.
        /// For instance: http://192.168.0.1:8080/alfresco/cmisatom
        /// </summary>
        public Uri Address { get { return credentials.Address; } set { credentials.Address = value; } }


        /// <summary>
        /// CMIS user.
        /// </summary>
        public string User { get { return credentials.UserName; } set { credentials.UserName = value; } }


        /// <summary>
        /// CMIS password, hashed.
        /// For instance: AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAtiSvUCYn...
        /// </summary>
        public Password Password { get { return credentials.Password; } set { credentials.Password = value; } }


        /// <summary>
        /// Identifier of the CMIS repository within the CMIS server.
        /// For instance: 7d52d0bd-5108-4dba-8102-48b63b9d3541
        /// </summary>
        public string RepoID { get; set; }


        /// <summary>
        /// In case the user choose to put the synchronized folder outside of the CmisSync directory, this stores the path to it.
        /// </summary>
        public string TargetDirectory { get; set; }


        /// <summary>
        /// Poll interval, in milliseconds.
        /// CmisSync will sync this remote folder once during this interval of time.
        /// </summary>
        public double PollInterval { get; set; }

        /// <summary>
        /// Define if this repo is suspended or not ?
        /// </summary>
        public bool IsSuspended { get; set; }

        /// <summary>
        /// Define the last successed sync time
        /// </summary>
        public DateTime LastSuccessedSync { get; set; }

        /// <summary>
        /// Define if, at cmissync startup, first sync is base on last success time or on startup
        /// </summary>
        public bool SyncAtStartup { get; set; }

        /// <summary>
        /// All folders, which should be ignored on synchronization.
        /// </summary>
        private List<string> ignoredPaths = new List<string>();


        /// <summary>
        /// Chunk size
        /// If none zero, CmisSync will divide the document by chunk size for download/upload.
        /// </summary>
        public long ChunkSize { get; set; }

        /// <summary>
        /// Gets or sets the size of download chunks
        /// If size is set to none zero, a download will be chunked by the size.
        /// </summary>
        /// <value>
        /// The size of the download chunk.
        /// </value>
        public long DownloadChunkSize { get; set; }

        /// <summary>
        /// Gets or sets the max upload retries.
        /// </summary>
        /// <value>
        /// The max upload retries.
        /// </value>
        public long MaxUploadRetries { get; set; }

        /// <summary></summary>
        public long MaxDownloadRetries { get; set; }

        /// <summary></summary>
        public long MaxDeletionRetries { get; set; }

        public CmisProfile CmisProfile { get; set; }

        /// <summary>
        /// Simple constructor.
        /// </summary>
        public RepoInfo(string name, string cmisDatabaseFolder)
        {
            Name = name;
            name = name.Replace("\\", "_");
            name = name.Replace("/", "_");
            CmisDatabase = Path.Combine(cmisDatabaseFolder, name + ".cmissync");
            CmisProfile = new CmisProfile();
        }

        /// <summary>
        /// Full constructor.
        /// </summary>
        [Obsolete("Use other contructor outside of testings")]
        public RepoInfo(string name, string cmisDatabaseFolder, string remotePath, string address, string user, string password, string repoID, double pollInterval, Boolean isSuspended, DateTime lastSuccessedSync, bool syncAtStartup)
        {
            Name = name;
            name = name.Replace("\\", "_");
            name = name.Replace("/", "_");
            CmisDatabase = Path.Combine(cmisDatabaseFolder, name + ".cmissync");
            RemotePath = remotePath;
            Address = new Uri(address);
            User = user;
            Password = password;
            RepoID = repoID;
            TargetDirectory = Path.Combine(ConfigManager.CurrentConfig.FoldersPath, name);
            PollInterval = pollInterval;
            IsSuspended = isSuspended;
            LastSuccessedSync = lastSuccessedSync;
            SyncAtStartup = syncAtStartup;
            ChunkSize = 0;
            MaxUploadRetries = 2;
            MaxDownloadRetries = 2;
            MaxDeletionRetries = 2;
            CmisProfile = new CmisProfile();
        }

        /// <summary>
        /// Adds a new path to the list of paths, which should be ignored.
        /// It has to be a absolute path from the repoID on with a leading
        /// slash. Path separator must also be a slash.
        /// </summary>
        /// <param name="path"></param>
        public void addIgnorePath(string path)
        {
            if(!this.ignoredPaths.Contains(path) && !String.IsNullOrEmpty(path))
                this.ignoredPaths.Add(path);
        }

        /// <summary>
        /// All folders, which should be ignored on synchronization
        /// will be returned.
        /// </summary>
        /// <returns>all ignored folders</returns>
        public string[] getIgnoredPaths()
        {
            return ignoredPaths.ToArray();
        }

        /// <summary>
        /// If the given path should be ignored, TRUE will be returned,
        /// otherwise FALSE.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool isPathIgnored(string path)
        {
            if(Utils.IsInvalidFolderName(path.Replace("/", "").Replace("\"","")))
                return true;
            return !String.IsNullOrEmpty(ignoredPaths.Find(delegate(string ignore)
            {
                if (String.IsNullOrEmpty(ignore)) {
                    return false;
                }
                return path.StartsWith(ignore);
            }));
        }
    }
}
