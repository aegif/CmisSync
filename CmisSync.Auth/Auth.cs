using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;

namespace CmisSync.Auth
{
    /// <summary>
    /// This class allows one to connect to a CMIS repository.
    /// It can return a list of repositories, or directly a CMIS session if the repository is known.
    /// </summary>
    public class Auth
    {
        /// <summary>
        /// Connect to a CMIS server and get the list of repositories.
        /// </summary>
        public static IList<IRepository> GetCmisRepositories(Uri url, string user, string password)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = url.ToString();
            parameters[SessionParameter.User] = user;
            parameters[SessionParameter.Password] = Crypto.Deobfuscate(password);

            // Create session factory.
            SessionFactory factory = SessionFactory.NewInstance();

            // Return repositories.
            return factory.GetRepositories(parameters);
        }


        /// <summary>
        /// Connect to a CMIS server and get a CMIS session that can be used for further CMIS operations.
        /// </summary>
        public static ISession GetCmisSession(string url, string user, string password, string repositoryId)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = url;
            parameters[SessionParameter.User] = user;
            parameters[SessionParameter.Password] = password;
            parameters[SessionParameter.RepositoryId] = repositoryId;

            // Create session factory.
            SessionFactory factory = SessionFactory.NewInstance();

            // Return session.
            return factory.CreateSession(parameters);
        }
    }
}
