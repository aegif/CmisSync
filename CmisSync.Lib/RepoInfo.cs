using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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


        /// <summary>
        /// Address of the server's CMIS endpoint.
        /// For instance: http://192.168.0.1:8080/alfresco/cmisatom
        /// </summary>
        public Uri Address { get; set; }


        /// <summary>
        /// CMIS user.
        /// </summary>
        public string User { get; set; }


        /// <summary>
        /// CMIS password, hashed.
        /// For instance: AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAtiSvUCYn...
        /// </summary>
        public string Password { get; set; }


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
        /// All folders, which should be ignored on synchronization.
        /// </summary>
        private List<string> ignoredPaths = new List<string>();

        /// <summary>
        /// Simple constructor.
        /// </summary>
        public RepoInfo(string name, string cmisDatabaseFolder)
        {
            Name = name;
            CmisDatabase = Path.Combine(cmisDatabaseFolder, name + ".cmissync");
        }


        /// <summary>
        /// Full constructor.
        /// </summary>
        public RepoInfo(string name, string cmisDatabaseFolder, string remotePath, string address, string user, string password, string repoID, double pollInterval)
        {
            Name = name;
            CmisDatabase = Path.Combine(cmisDatabaseFolder, name + ".cmissync");
            RemotePath = remotePath;
            Address = new Uri(address);
            User = user;
            Password = password;
            RepoID = repoID;
            TargetDirectory = Path.Combine(ConfigManager.CurrentConfig.FoldersPath, name);
            PollInterval = pollInterval;
        }

        /// <summary>
        /// Adds a new path to the list of paths, which should be ignored.
        /// It has to be a absolute path from the repoID on with a leading
        /// slash. Path separator must also be a slash.
        /// </summary>
        /// <param name="path"></param>
        public void addIgnorePath(string path)
        {
            if(!this.ignoredPaths.Contains(path))
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
            return ignoredPaths.Contains(path);
        }

    }
}
