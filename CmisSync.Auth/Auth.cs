using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;

namespace CmisSync.Auth
{
    public class Auth
    {
        public static IList<IRepository> GetCmisRepositories(Uri url, string user, string password)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = url.ToString();
            parameters[SessionParameter.User] = user;
            parameters[SessionParameter.Password] = Crypto.Deobfuscate(password);

            // Create session factory.
            SessionFactory factory = SessionFactory.NewInstance();

            return factory.GetRepositories(parameters);
        }

        public static ISession GetCmisSession(string url, string repositoryId, string user, string password)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = url;
            parameters[SessionParameter.User] = user;
            parameters[SessionParameter.Password] = password;
            parameters[SessionParameter.RepositoryId] = repositoryId;
            SessionFactory factory = SessionFactory.NewInstance();
            return factory.CreateSession(parameters);
        }
    }
}
