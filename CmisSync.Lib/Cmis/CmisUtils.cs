using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS;
using DotCMIS.Exceptions;
using log4net;
using System.Web;

namespace CmisSync.Lib.Cmis
{
    public class CmisServer
    {
        public Uri Url { get; private set; }
        public Dictionary<string, string> Repositories { get; private set; }
        public CmisServer(Uri url, Dictionary<string, string> repositories)
        {
            Url = url;
            Repositories = repositories;
        }
    }

    public static class CmisUtils
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisUtils));

        /**
         * Try to find the CMIS server associated to any URL.
         * Users can provide the URL of the web interface, and we have to return the CMIS URL
         * Returns the list of repositories as well.
         */
        static public CmisServer GetRepositoriesFuzzy(Uri url, string user, string password)
        {
            Dictionary<string, string> repositories = null;

            // Try the given URL, maybe user directly entered the CMIS AtomPub endpoint URL.
            try
            {
                repositories = GetRepositories(url, user, password);
            }
            catch (CmisPermissionDeniedException ex)
            {
                // Do nothing.
            }
            if (repositories != null)
            {
                return new CmisServer(url, repositories);
            }

            // Extract protocol and server name or IP address
            string prefix = url.GetLeftPart(UriPartial.Authority);

            // See https://github.com/nicolas-raoul/CmisSync/wiki/What-address for the list of ECM products prefixes
            string[] suffixes = {
                "/alfresco/cmisatom",
                "/alfresco/service/cmis",
                "/cmis/atom",
                "/cmis/resources/",
                "/emc-cmis-ea/resources/",
                "/xcmis/rest/cmisatom",
                "/files/basic/cmis/my/servicedoc",
                "/p8cmis/resources/Service",
                "/_vti_bin/cmis/rest?getRepositories",
                "/Nemaki/atom/bedroom",
                "/nuxeo/atom/cmis"
            };

            // Try all suffixes
            for (int i=0; i < suffixes.Length; i++)
            {
                string fuzzyUrl = prefix + suffixes[i];
                Logger.Info("Sync | Trying with " + fuzzyUrl);
                try
                {
                    repositories = GetRepositories(new Uri(fuzzyUrl), user, password);
                }
                catch (CmisPermissionDeniedException ex)
                {
                    // Do nothing.
                }
                if (repositories != null)
                    return new CmisServer(new Uri(fuzzyUrl), repositories);
            }
            return new CmisServer(url, repositories);
        }

        /**
         * Get the list of repositories of a CMIS server
         * Each item contains id + 
         */
        static public Dictionary<string,string> GetRepositories(Uri url, string user, string password)
        {
            Dictionary<string,string> result = new Dictionary<string,string>();

            if (null == url)
            {
                return result;
            }
            
            // Create session factory.
            SessionFactory factory = SessionFactory.NewInstance();

            Dictionary<string, string> cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url.ToString();
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] =password;

            IList<IRepository> repositories;
            try
            {
                repositories = factory.GetRepositories(cmisParameters);
            }
            catch (DotCMIS.Exceptions.CmisPermissionDeniedException e)
            {
                Logger.Error("CMIS server found, but permission denied. Please check username/password. " + Utils.ToLogString(e));
                throw new CmisSync.Lib.Cmis.CmisPermissionDeniedException("PermissionDenied");
            }
            catch (CmisRuntimeException e)
            {
                Logger.Error("No CMIS server at this address, or no connection. " + Utils.ToLogString(e));
                return null;
            }
            catch (CmisObjectNotFoundException e)
            {
                Logger.Error("No CMIS server at this address, or no connection. " + Utils.ToLogString(e));
                return null;
            }
            catch (CmisConnectionException e)
            {
                Logger.Error("No CMIS server at this address, or no connection. " + Utils.ToLogString(e));
                return null;
            }
            catch (CmisInvalidArgumentException e)
            {
                Logger.Error("Invalid URL, maybe Alfresco Cloud? " + Utils.ToLogString(e));
                return null;
            }

            foreach (IRepository repo in repositories)
            {
                result.Add(repo.Id, repo.Name);
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

            Logger.Info("CmisUtils | folder.Properties.Count:" + folder.Properties.Count.ToString());
            IItemEnumerable<ICmisObject> children = folder.GetChildren();

            // Debug that leads to crash on eXo Platform
            //long num = children.TotalNumItems;
            //Logger.Debug("CmisUtils | children number: " + num);
            //foreach (var child in children)
            //{
            //    Logger.Debug("CmisUtils | child: " + child);
            //}

            foreach (var subfolder in children.OfType<IFolder>())
            {
                result.Add(subfolder.Path);
            }
            return result.ToArray();
        }

        // Guess the web address where files can be seen using a browser.
        // Not bulletproof. It depends on the server, and there is no web UI at all.
        static public string GetBrowsableURL(RepoInfo repo)
        {
            if (null == repo)
            {
                throw new ArgumentNullException("repo");
            }

            if (repo.Address.AbsoluteUri.EndsWith("alfresco/cmisatom"))
            {
                // Alfresco
                string root = repo.Address.AbsoluteUri.Substring(0, repo.Address.AbsoluteUri.Length - "alfresco/cmisatom".Length);
                if (repo.RemotePath.StartsWith("/Sites"))
                {
                    // Alfresco Share
                    // Example RemotePath: /Sites/thesite
                    // Result: http://server/share/page/site/thesite/documentlibrary
                    // Example RemotePath: /Sites/thesite/documentLibrary/somefolder/anotherfolder
                    // Result: http://server/share/page/site/thesite/documentlibrary#filter=path|%2Fsomefolder%2Fanotherfolder
                    // Example RemotePath: /Sites/s1/documentLibrary/éß和ệ
                    // Result: http://server/share/page/site/s1/documentlibrary#filter=path|%2F%25E9%25DF%25u548C%25u1EC7
                    // Example RemotePath: /Sites/s1/documentLibrary/a#bc/éß和ệ
                    // Result: http://server/share/page/site/thesite/documentlibrary#filter=path%7C%2Fa%2523bc%2F%25E9%25DF%25u548C%25u1EC7%7C
                    string path = repo.RemotePath.Substring("/Sites/".Length);
                    if (path.Contains("documentLibrary"))
                    {
                        int firstSlashPosition = path.IndexOf('/');
                        string siteName = path.Substring(0, firstSlashPosition);
                        string pathWithinSite = path.Substring(firstSlashPosition + "/documentLibrary".Length);
                        string escapedPathWithinSite = HttpUtility.UrlEncode(pathWithinSite);
                        string reescapedPathWithinSite = HttpUtility.UrlEncode(escapedPathWithinSite);
                        string sharePath = reescapedPathWithinSite.Replace("%252f", "%2F");
                        return root + "share/page/site/" + siteName + "/documentlibrary#filter=path|" + sharePath;
                    }
                    else
                    {
                        // Site name only.
                        return root + "share/page/site/" + path + "/documentlibrary";
                    }
                }
                else
                {
                    // Alfresco Web Client
                    return root;
                }
            }
            else
            {
                return repo.Address.AbsoluteUri + repo.RemotePath;
            }
        }
    }
}
