using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS.Client;
using log4net;
using System.IO;
using CmisSync.Lib.Utilities.FileUtilities;
using CmisSync.Auth;

namespace CmisSync.Lib.Cmis
{
    public class CmisProfileRefactor
    {
        /// <summary>
        /// Log.
        /// </summary>
        protected static readonly ILog Logger = LogManager.GetLogger (typeof (CmisProfileRefactor));


        public Uri RemoteUri;

        public string User;

        public Password Password;

        public string RepoID;

        public CmisProperties CmisProperties;

        public CmisProfileRefactor (RepoInfo repoInfo)
        {
            RemoteUri = repoInfo.Address;
            RepoID = repoInfo.RepoID;
            User = repoInfo.User;
            Password = repoInfo.Password;
            CmisProperties = new CmisProperties ();
        }

    }
}
