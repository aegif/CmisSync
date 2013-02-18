using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SparkleLib
{
    public class SparkleRepoInfo
    {
        private string cmisdatase;
        public string CmisDatabase { get { return cmisdatase; } set { cmisdatase = value; } }

        private string remotepath;
        public string RemotePath { get { return remotepath; } set { remotepath = value; } }

        private Uri address;
        public Uri Address { get { return address; } set { address = value; } }

        private string user;
        public string User { get { return user; } set { user = value; } }

        private string password;
        public string Password { get { return password; } set { password = value; } }

        private string repoid;
        public string RepoID { get { return repoid; } set { repoid = value; } }

        // For other backend ?
        private string fingerprint;
        public string Fingerprint { get { return fingerprint; } set { fingerprint = value; } }

        private bool fetchpriorhistory;
        public bool FetchPriorHistory { get { return fetchpriorhistory; } set { fetchpriorhistory = value; } }

        private string targetdirectory;
        public string TargetDirectory { get { return targetdirectory; } set { targetdirectory = value; } }

        private string identifier;
        public string Identifier { get { return identifier; } set { identifier = value; } }

        private string backend;
        public string Backend { get { return "Cmis"; } }

        public SparkleRepoInfo(string folderName, string cmisDatabasePath )
        {
            targetdirectory = folderName;
            cmisdatase = Path.Combine(cmisDatabasePath, targetdirectory + ".cmissync");
        }
    }
}
