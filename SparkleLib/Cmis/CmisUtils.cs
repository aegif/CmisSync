using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS;
using DotCMIS.Exceptions;

namespace SparkleLib.Cmis
{
    public class CmisServer
    {
        public string url;
        public string[] repositories;
        public CmisServer(string url, string[] repositories)
        {
            this.url = url;
            this.repositories = repositories;
        }
    }

    public class CmisUtils
    {
        /**
         * Try to find the CMIS server associated to any URL.
         * Users can provide the URL of the web interface, and we have to return the CMIS URL
         * Returns the list of repositories as well.
         */
        static public CmisServer GetRepositoriesFuzzy(string url, string user, string password)
        {
            // Try the given URL
            string[] repositories = GetRepositories(url, user, password);
            if (repositories != null)
            {
                return new CmisServer(url, repositories);
            }

            // Extract protocol and server name or IP address
            string prefix = new Uri(url).GetLeftPart(UriPartial.Authority);

            // See https://github.com/nicolas-raoul/CmisSync/wiki/What-address for the list of ECM products prefixes
            string[] suffixes = {
                "/alfresco/cmisatom",
                "/cmis/resources/",
                "/emc-cmis-ea/resources/",
                "/files/basic/cmis/my/servicedoc",
                "/p8cmis/resources/Service",
                "/_vti_bin/ListData.svc",
                "/Nemaki/atom"
            };

            // Try all suffixes
            for (int i=0; i < suffixes.Length; i++)
            {
                string fuzzyUrl = prefix + suffixes[i];
                SparkleLogger.LogInfo("Sync", "Trying with " + fuzzyUrl);
                repositories = GetRepositories(fuzzyUrl, user, password);
                if (repositories != null)
                    return new CmisServer(fuzzyUrl, repositories);
            }
            return new CmisServer(url, repositories);
        }

        /**
         * Get the list of repositories of a CMIS server
         */
        static public string[] GetRepositories(string url, string user, string password)
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
            catch (CmisPermissionDeniedException e)
            {
                throw new CmisServerNotFoundException("CMIS server found, but permission denied. Please check username/password");
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

            string[] result = new string[repositories.Count];

            for (int i = 0; i < repositories.Count; i++)
            {
                result[i] = repositories.ElementAt(i).Id; // TODO displaying Name would be more user-friendly than Id
            }
            
            return result;
        }

        static public string[] getSubfolders(string repositoryId, string path,
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

            IFolder folder = (IFolder)session.GetObjectByPath("/" + path);
            //IFolder folder = (IFolder)session.GetObjectByPath("/files");
            SparkleLogger.LogInfo("Sync", "folder.Properties.Count:" + folder.Properties.Count);
            IItemEnumerable<ICmisObject> children = folder.GetChildren();
            foreach (ICmisObject obj in children)
            {
                if (obj is IFolder)
                {
                    IFolder subfolder = (IFolder)obj;
                    result.Add(subfolder.Path);
                }
            }
            return result.ToArray();
        }
    }
}
