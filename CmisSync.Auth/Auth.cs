using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using log4net;

namespace CmisSync.Auth
{
    /// <summary>
    /// This class allows one to connect to a CMIS repository.
    /// It can return a list of repositories, or directly a CMIS session if the repository is known.
    /// </summary>
    public class Auth
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Auth));

        /// <summary>
        /// Connect to a CMIS server and get the list of repositories.
        /// This method takes a obfuscated password, unlike the next method.
        /// </summary>
        public static IList<IRepository> GetCmisRepositories(Uri url, string user, string obfuscatedPassword)
        {
            Dictionary<string, string> parameters = GetParameters();
            parameters[SessionParameter.AtomPubUrl] = url.ToString();
            parameters[SessionParameter.User] = user;
            parameters[SessionParameter.Password] = Crypto.Deobfuscate(obfuscatedPassword);

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
            Dictionary<string, string> parameters = GetParameters();
            parameters[SessionParameter.AtomPubUrl] = url;
            parameters[SessionParameter.User] = user;
            parameters[SessionParameter.Password] = password;
            parameters[SessionParameter.RepositoryId] = repositoryId;

            // Create session factory.
            SessionFactory factory = SessionFactory.NewInstance();

            // Create session.
            ISession session = factory.CreateSession(parameters);
            Logger.Info("VendorName: " + session.RepositoryInfo.VendorName);
            Logger.Info("ProductName: " + session.RepositoryInfo.ProductName);
            Logger.Info("ProductVersion: " + session.RepositoryInfo.ProductVersion);
            Logger.Info("CmisVersionSupported: " + session.RepositoryInfo.CmisVersionSupported);
            Logger.Info("Name: " + session.RepositoryInfo.Name);
            Logger.Info("Description: " + session.RepositoryInfo.Description);
            return session;
        }


        private static Dictionary<string, string> GetParameters()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            // AtomPub is the most reliable/well-tested CMIS implementation for pretty much all servers.
            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            // Sets the Connect Timeout to infinite
            parameters[SessionParameter.ConnectTimeout] = "60000"; // One minute
            // Sets the Read Timeout to infinite
            parameters[SessionParameter.ReadTimeout] = "1200000"; // Twenty minutes (to allow for huge GetDescendants operations)
            return parameters;
        }
    }
}
