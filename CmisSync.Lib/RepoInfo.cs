using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CmisSync.Lib
{
    public class RepoInfo
    {
        /// Name of the local directory within the CmisSync directory.
        /// For instance: "User Homes"
        public string Name { get; set; }

        /// Full path to the local database.
        /// For instance: C:\\Users\\win7pro32bit\\AppData\\Roaming\\cmissync\\User Homes.cmissync
        public string CmisDatabase { get; set; }

        /// Path on the remote server, starting from the root of the CMIS repository.
        /// For instance: /User Homes or /Sites/mysite/myfolder
        public string RemotePath { get; set; }

        /// Address of the server's CMIS endpoint.
        /// For instance: http://192.168.0.1:8080/alfresco/cmisatom
        public Uri Address { get; set; }

        /// CMIS user
        public string User { get; set; }

        /// CMIS password, hashed.
        /// For instance: AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAtiSvUCYn...
        public string Password { get; set; }

        /// Identifier of the CMIS repository within the CMIS server.
        /// For instance: 7d52d0bd-5108-4dba-8102-48b63b9d3541
        public string RepoID { get; set; }

        /// In case the user choose to put the synchronized folder outside of the CmisSync directory, this stores the path to it.
        public string TargetDirectory { get; set; }

        /// Poll interval, in milliseconds.
        /// CmisSync will sync this remote folder once during this interval of time.
        public double PollInterval { get; set; }

        public RepoInfo(string name, string cmisDatabaseFolder)
        {
            Name = name;
            CmisDatabase = Path.Combine(cmisDatabaseFolder, name + ".cmissync");
        }

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
    }
}
