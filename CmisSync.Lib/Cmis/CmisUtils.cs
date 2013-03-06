using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS;
using DotCMIS.Exceptions;
using log4net;

namespace CmisSync.Lib.Cmis
{
    public class CmisServer
    {
        public string url;
        public Dictionary<string, string> repositories;
        public CmisServer(string url, Dictionary<string, string> repositories)
        {
            this.url = url;
            this.repositories = repositories;
        }
    }

    public class CmisUtils
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisUtils));

        /**
         * Try to find the CMIS server associated to any URL.
         * Users can provide the URL of the web interface, and we have to return the CMIS URL
         * Returns the list of repositories as well.
         */
        static public CmisServer GetRepositoriesFuzzy(string url, string user, string password)
        {
            // Try the given URL
            Dictionary<string, string> repositories = GetRepositories(url, user, password);
            if (repositories != null)
            {
                return new CmisServer(url, repositories);
            }

            // Extract protocol and server name or IP address
            string prefix = new Uri(url).GetLeftPart(UriPartial.Authority);

            // See https://github.com/nicolas-raoul/CmisSync/wiki/What-address for the list of ECM products prefixes
            string[] suffixes = {
                "/alfresco/cmisatom",
                "/alfresco/service/cmis",
                "/cmis/resources/",
                "/emc-cmis-ea/resources/",
                "/xcmis/rest/cmisatom",
                "/files/basic/cmis/my/servicedoc",
                "/p8cmis/resources/Service",
                "/_vti_bin/cmis/rest?getRepositories",
                "/Nemaki/atom",
                "/nuxeo/atom/cmis"
            };

            // Try all suffixes
            for (int i=0; i < suffixes.Length; i++)
            {
                string fuzzyUrl = prefix + suffixes[i];
                Logger.Info("Sync | Trying with " + fuzzyUrl);
                repositories = GetRepositories(fuzzyUrl, user, password);
                if (repositories != null)
                    return new CmisServer(fuzzyUrl, repositories);
            }
            return new CmisServer(url, repositories);
        }

        /**
         * Get the list of repositories of a CMIS server
         * Each item contains id + 
         */
        static public Dictionary<string,string> GetRepositories(string url, string user, string password)
        {


            // Create session factory.
            SessionFactory factory = SessionFactory.NewInstance();

            Dictionary<string, string> cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] =password;

            IList<IRepository> repositories;
            try
            {
                repositories = factory.GetRepositories(cmisParameters);
            }
            catch (DotCMIS.Exceptions.CmisPermissionDeniedException e)
            {
                //throw new CmisServerNotFoundException("CMIS server found, but permission denied. Please check username/password");
                throw new CmisSync.Lib.Cmis.CmisPermissionDeniedException("PermissionDenied");
            }
            catch (CmisRuntimeException e)
            {
                // No CMIS server at this address, or no connection.
                return null;
            }
            catch (CmisObjectNotFoundException e)
            {
                // No CMIS server at this address, or no connection.
                return null;
            }
            catch (CmisConnectionException e)
            {
                // No CMIS server at this address, or no connection.
                return null;
            }

            Dictionary<string,string> result = new Dictionary<string,string>();

            for (int i = 0; i < repositories.Count; i++)
            {
                result.Add(repositories.ElementAt(i).Id, repositories.ElementAt(i).Name);
            }
            
            return result;
        }

        static public string[] GetSubfolders(string repositoryId, string path,
            string address, string user, string password)
        {
            List<string> result = new List<string>();

            Dictionary<string, string> cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = address;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] = password;
            cmisParameters[SessionParameter.RepositoryId] = repositoryId;

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.CreateSession(cmisParameters);

            IFolder folder = (IFolder)session.GetObjectByPath(path);

            Logger.Info("Sync | folder.Properties.Count:" + folder.Properties.Count);
            IItemEnumerable<ICmisObject> children = folder.GetChildren();
            foreach (var subfolder in children.OfType<IFolder>())
            {
                result.Add(subfolder.Path);
            }
            return result.ToArray();
        }
    }
}
