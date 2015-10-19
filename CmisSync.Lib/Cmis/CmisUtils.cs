using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Exceptions;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CmisSync.Lib.Auth;
using CmisSync.Lib;
using System.Collections.ObjectModel;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// Useful CMIS methods.
    /// </summary>
    public static class CmisUtils
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisUtils));

        static public ReadOnlyCollection<Config.SyncConfig.RemoteRepository> GetRepositories(Config.SyncConfig.Account account)
        {
            return GetRepositories(account, false);
        }

        /// <summary>
        /// Try to find the CMIS server associated to any URL.
        /// Users can provide the URL of the web interface, and we have to return the CMIS URL
        /// Returns the list of repositories as well.
        /// </summary>
        static public ReadOnlyCollection<Config.SyncConfig.RemoteRepository> GetRepositories(Config.SyncConfig.Account account, bool discoverCorrectUriAndUpdate)
        {
            List<Config.SyncConfig.RemoteRepository> repositories = null;
            Exception firstException = null;

            // Try the given URL, maybe user directly entered the CMIS AtomPub endpoint URL.
            try
            {
                repositories = doGetRepositories(account);
            }
            catch (CmisRuntimeException e)
            {
                if (e.Message == "ConnectFailure")
                {
                    throw new NetworkException(e);
                }
                firstException = e;
            }
            catch (Exception e)
            {
                // Save first Exception and try other possibilities.
                firstException = e;
            }
            if (repositories != null)
            {
                // Found!
                return new ReadOnlyCollection<Config.SyncConfig.RemoteRepository>(repositories);
            }

            if (discoverCorrectUriAndUpdate == false)
            {
                throw firstException;
            }

            // Extract protocol and server name or IP address
            string prefix = account.RemoteUrl.GetLeftPart(UriPartial.Authority);

            // See https://github.com/aegif/CmisSync/wiki/What-address for the list of ECM products prefixes
            // Please send us requests to support more CMIS servers: https://github.com/aegif/CmisSync/issues
            string[] suffixes = {
                "/alfresco/api/-default-/public/cmis/versions/1.1/atom", // Alfresco 4.2 CMIS 1.1
                "/alfresco/api/-default-/public/cmis/versions/1.0/atom", // Alfresco 4.2 CMIS 1.0
                "/alfresco/cmisatom", // Alfresco 4.0 and 4.1
                "/alfresco/service/cmis", // Alfresco 3.x
                "/cmis/atom11", // OpenDataSpace
                "/rest/private/cmisatom/", // eXo Platform
                "/xcmis/rest/cmisatom", // xCMIS
                "/files/basic/cmis/my/servicedoc", // IBM Connections
                "/p8cmis/resources/Service", // IBM FileNet
                "/_vti_bin/cmis/rest?getRepositories", // Microsoft SharePoint
                "/nemakiware/atom/bedroom", // NemakiWare  TODO: different port, typically 8080 for Web UI and 3000 for CMIS
                "/nuxeo/atom/cmis", // Nuxeo
                "/cmis/atom",
                "/cmis/resources/", // EMC Documentum
                "/emc-cmis-ea/resources/", // EMC Documentum
                "/emc-cmis-weblogic/resources/", // EMC Documentum
                "/emc-cmis-wls/resources/", // EMC Documentum
                "/emc-cmis-was61/resources/", // EMC Documentum
                "/emc-cmis-wls1030/resources/", // EMC Documentum
                "/docushare/ds_mobile_connector/atom", // Xerox DocuShare
                "/documents/ds_mobile_connector/atom" // Xerox DocuShare  TODO: can be anything instead of "documents"
            };
            Uri originalUrl = account.RemoteUrl;
            string bestUrl = null;
            // Try all suffixes
            for (int i = 0; i < suffixes.Length; i++)
            {
                string fuzzyUrl = prefix + suffixes[i];
                Logger.Info("Sync | Trying with " + fuzzyUrl);
                try
                {
                    account.RemoteUrl = new Uri(fuzzyUrl);
                    repositories = doGetRepositories(account);
                }
                catch (CmisPermissionDeniedException e)
                {
                    firstException = e;
                    bestUrl = fuzzyUrl;
                }
                catch (Exception e)
                {
                    // Do nothing, try other possibilities.
                    Logger.Debug(e.Message);
                }
                if (repositories != null)
                {
                    // Found!
                    return new ReadOnlyCollection<Config.SyncConfig.RemoteRepository>(repositories);
                }
            }

            //restore the original url
            account.RemoteUrl = originalUrl;
            // Not found. Return also the first exception to inform the user correctly
            throw new ServerNotFoundException(firstException)
            {
                BestTryedUrl = new Uri(bestUrl)
            };
        }


        /// <summary>
        /// Get the list of repositories of a CMIS server
        /// Each item contains id +
        /// </summary>
        /// <returns>The list of repositories. Each item contains the identifier and the human-readable name of the repository.</returns>
        static private List<Config.SyncConfig.RemoteRepository> doGetRepositories(Config.SyncConfig.Account account)
        {
            // If no URL was provided, return empty result.
            if (account.RemoteUrl == null)
            {
                throw new System.ArgumentException();
            }

            IList<IRepository> repositories;
            try
            {
                repositories = Auth.Auth.GetCmisRepositories(account.RemoteUrl, account.Credentials.UserName, account.Credentials.Password);
            }
            catch (CmisPermissionDeniedException e)
            {
                Logger.Error("CMIS server found, but permission denied. Please check username/password. ", e);
                throw;
            }
            catch (CmisRuntimeException e)
            {
                Logger.Error("No CMIS server at this address, or no connection. ", e);
                throw;
            }
            catch (CmisObjectNotFoundException e)
            {
                Logger.Error("No CMIS server at this address, or no connection. ", e);
                throw;
            }
            catch (CmisConnectionException e)
            {
                Logger.Error("No CMIS server at this address, or no connection. ", e);
                throw;
            }
            catch (CmisInvalidArgumentException e)
            {
                Logger.Error("Invalid URL, maybe Alfresco Cloud? ", e);
                throw;
            }

            List<Config.SyncConfig.RemoteRepository> result = new List<Config.SyncConfig.RemoteRepository>();
            // Populate the result list with identifier and name of each repository.
            foreach (IRepository repo in repositories)
            {
                // Repo name is sometimes empty (ex: Alfresco), in such case show the repo id instead.
                string name = repo.Name.Length == 0 ? repo.Id : repo.Name;

                result.Add(new Config.SyncConfig.RemoteRepository(account, repo.Id, name));
            }

            return result;
        }


        /// <summary>
        /// Get the sub-folders of a particular CMIS folder.
        /// </summary>
        /// <returns>Full path of each sub-folder, including leading slash.</returns>
        static public string[] GetSubfolders(string repositoryId, string path,
            Uri url, UserCredentials credentials)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path");
            }

            List<string> result = new List<string>();

            // Connect to the CMIS repository.
            ISession session = Auth.Auth.GetCmisSession(url, credentials, repositoryId);

            // Get the folder.
            IFolder folder;
            try
            {
                folder = (IFolder)session.GetObjectByPath(path);
            }
            catch (Exception ex)
            {
                Logger.Warn(String.Format("CmisUtils | exception when session GetObjectByPath for {0}", path), ex);
                return result.ToArray();
            }

            // Debug the properties count, which allows to check whether a particular CMIS implementation is compliant or not.
            // For instance, IBM Connections is known to send an illegal count.
            Logger.Info("CmisUtils | folder.Properties.Count:" + folder.Properties.Count.ToString());

            // Get the folder's sub-folders.
            IItemEnumerable<ICmisObject> children = folder.GetChildren();

            // Return the full path of each of the sub-folders.
            foreach (var subfolder in children.OfType<IFolder>())
            {
                result.Add(subfolder.Path);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Guess the web address where files can be seen using a browser.
        /// Not bulletproof. It depends on the server, and on some servers there is no web UI at all.
        /// </summary>
        static public string GetBrowsableURL(Config.SyncConfig.SyncFolder repo)
        {
            if (null == repo)
            {
                throw new ArgumentNullException("repo");
            }

            // Case of Alfresco.
            string suffix1 = "alfresco/cmisatom";
            string suffix2 = "alfresco/service/cmis";
            if (repo.Account.RemoteUrl.AbsoluteUri.EndsWith(suffix1) || repo.Account.RemoteUrl.AbsoluteUri.EndsWith(suffix2))
            {
                // Detect suffix length.
                int suffixLength = 0;
                if (repo.Account.RemoteUrl.AbsoluteUri.EndsWith(suffix1))
                    suffixLength = suffix1.Length;
                if (repo.Account.RemoteUrl.AbsoluteUri.EndsWith(suffix2))
                    suffixLength = suffix2.Length;

                string root = repo.Account.RemoteUrl.AbsoluteUri.Substring(0, repo.Account.RemoteUrl.AbsoluteUri.Length - suffixLength);
                if (repo.RemotePath.StartsWith("/Sites"))
                {
                    // Case of Alfresco Share.

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
                        sharePath = sharePath.Replace("%2b", "%20");
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
                    // Case of Alfresco Web Client. Difficult to build a direct URL, so return root.
                    return root;
                }
            }
            else
            {
                // Another server was detected, try to open the thinclient url, otherwise try to open the repo path

                try
                {
                    // Connect to the CMIS repository.
                    ISession session = Auth.Auth.GetCmisSession(repo.Account.RemoteUrl, repo.Account.Credentials, repo.RepositoryId);

                    if (session.RepositoryInfo.ThinClientUri == null
                        || String.IsNullOrEmpty(session.RepositoryInfo.ThinClientUri.ToString()))
                    {
                        Logger.Error("CmisUtils GetBrowsableURL | Repository does not implement ThinClientUri: " + repo.Account.RemoteUrl.AbsoluteUri);
                        return repo.Account.RemoteUrl.AbsoluteUri + repo.RemotePath;
                    }
                    else
                    {
                        // Return CmisServer-provided thin URL.
                        return session.RepositoryInfo.ThinClientUri.ToString();
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("CmisUtils GetBrowsableURL | Exception " + e.Message, e);
                    // Server down or authentication problem, no way to know the right URL, so just open server.
                    return repo.Account.RemoteUrl.AbsoluteUri + repo.RemotePath;
                }
            }
        }


        /// <summary>
        /// Get the value of a property of a CMIS document.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="id">Name of the property, for instance cmis:lastModifiedBy</param>
        /// <returns>property, or null if no such property</returns>
        public static string GetProperty(IDocument document, string id)
        {
            IEnumerator<IProperty> e = document.Properties.GetEnumerator();
            while (e.MoveNext())
            {
                IProperty property = e.Current;
                if (property.Id.Equals(id))
                {
                    return (string)property.Value;
                }
            }
            return null;
        }


        /// <summary>
        /// Get the latest ChangeLog token.
        /// Alfresco sends a null token if no change has ever happened. In that case, return empty string. See https://issues.alfresco.com/jira/browse/ALF-21276
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static string GetChangeLogToken(ISession session)
        {
            //FIXME: should not clear cache!
            session.Clear(); // Clear all caches.
            session.Binding.GetRepositoryService().GetRepositoryInfos(null);
            string token = session.Binding.GetRepositoryService().GetRepositoryInfo(session.RepositoryInfo.Id, null).LatestChangeLogToken;
            Logger.Debug("Server token:" + token);
            return token ?? string.Empty;
        }

        public static void TestAccount(Config.SyncConfig.Account account)
        {
            //TODO: can we perform a simplier test?
            ReadOnlyCollection<Config.SyncConfig.RemoteRepository> repositories = CmisUtils.GetRepositories(account, true);
        }
    }

    public class CmisPath
    {
        /// <summary>Character that separates two folders in a CMIS path.</summary>
        public static char DirectorySeparatorChar = '/';

        /// <summary>
        /// Equivalent of .NET Path.Combine, but for CMIS paths.
        /// CMIS paths always use forward slashes.
        /// </summary>
        public static string Combine(string path1, string path2)
        {
            if (String.IsNullOrEmpty(path1))
                return path2;

            if (String.IsNullOrEmpty(path2))
                return path1;

            if (path1.EndsWith(DirectorySeparatorChar.ToString())) {
                path1 = path1.Remove(path1.Length - 1, 1);
            }

            return path1 + DirectorySeparatorChar + path2;
        }

        internal static string GetFileName(string path)
        {
            return path.Substring(path.LastIndexOf(DirectorySeparatorChar) + 1);
        }

        internal static string GetDirectoryName(string path)
        {
            return GetFileName(path);
        }

        internal static bool IsPathRooted(string remotePath)
        {
            return remotePath.StartsWith(DirectorySeparatorChar.ToString());
        }
    }
}
