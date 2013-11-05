using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Lib.Credentials
{
    /// <summary>
    /// Typical user credantials used for generic logins
    /// </summary>
    public class UserCredentials
    {
        /// <summary>
        /// User name
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// Password
        /// </summary>
        public CmisSync.Lib.RepoInfo.CmisPassword Password { get; set; }
    }

    /// <summary>
    /// Server Login for a specific Uri
    /// </summary>
    public class ServerCredentials : UserCredentials
    {
        /// <summary>
        /// Server Address and Path
        /// </summary>
        public Uri Address { get; set; }
    }

    /// <summary>
    /// Credentials needed to create a Session for a specific CMIS repository
    /// </summary>
    public class CmisRepoCredentials : ServerCredentials
    {
        /// <summary>
        /// Repository ID
        /// </summary>
        public string RepoId { get; set; }
    }
}
