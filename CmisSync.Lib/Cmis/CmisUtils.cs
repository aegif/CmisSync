﻿using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Exceptions;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CmisSync.Auth;
using System.IO;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// Data object representing a CMIS server.
    /// </summary>
    public class CmisServer
    {
        /// <summary>
        /// URL of the CMIS server.
        /// </summary>
        public Uri Url { get; private set; }

        /// <summary>
        /// Repositories contained in the CMIS server.
        /// </summary>
        public Dictionary<string, string> Repositories { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public CmisServer(Uri url, Dictionary<string, string> repositories)
        {
            Url = url;
            Repositories = repositories;
        }
    }

    /// <summary>
    /// Useful CMIS methods.
    /// </summary>
    public static class CmisUtils
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisUtils));

        /// <summary>Character that separates two folders in a CMIS path (/).</summary>
        public static char CMIS_FILE_SEPARATOR = '/';

        /// <summary>
        /// Try to find the CMIS server associated to any URL.
        /// Users can provide the URL of the web interface, and we have to return the CMIS URL
        /// Returns the list of repositories as well.
        /// </summary>
        static public Tuple<CmisServer, Exception> GetRepositoriesFuzzy(ServerCredentials credentials)
        {
            Dictionary<string, string> repositories = null;
            Exception firstException = null;

            // Try the given URL, maybe user directly entered the CMIS AtomPub endpoint URL.
            try
            {
                repositories = GetRepositories(credentials);
            }
            catch (CmisRuntimeException e)
            {
                if (e.Message == "ConnectFailure")
                    return new Tuple<CmisServer, Exception>(new CmisServer(credentials.Address, null), new ServerNotFoundException(e.Message, e));
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
                return new Tuple<CmisServer, Exception>(new CmisServer(credentials.Address, repositories), null);
            }

            // Extract protocol and server name or IP address
            string prefix = credentials.Address.GetLeftPart(UriPartial.Authority);

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
                "/logicaldoc/service/cmis", // LogicalDOC
                "/_vti_bin/cmis/rest?getRepositories", // Microsoft SharePoint
                "/nemakiware/core/atom", // NemakiWare  TODO: different port, typically 8080 for Web UI and 3000 for CMIS
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
            string bestUrl = null;
            // Try all suffixes
            for (int i=0; i < suffixes.Length; i++)
            {
                string fuzzyUrl = prefix + suffixes[i];
                Logger.Info("Sync | Trying with " + fuzzyUrl);
                try
                {
                    ServerCredentials cred = new ServerCredentials()
                    {
                        UserName = credentials.UserName,
                        Password = credentials.Password.ToString(),
                        Address = new Uri(fuzzyUrl)
                    };
                    repositories = GetRepositories(cred);
                }
                catch (CmisPermissionDeniedException e)
                {
                    firstException = new PermissionDeniedException(e.Message, e);
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
                    return new Tuple<CmisServer, Exception>( new CmisServer(new Uri(fuzzyUrl), repositories), null);
                }
            }

            // Not found. Return also the first exception to inform the user correctly
            return new Tuple<CmisServer,Exception>(new CmisServer(bestUrl==null?credentials.Address:new Uri(bestUrl), null), firstException);
        }


        /// <summary>
        /// Get the list of repositories of a CMIS server
        /// Each item contains id +
        /// </summary>
        /// <returns>The list of repositories. Each item contains the identifier and the human-readable name of the repository.</returns>
        static public Dictionary<string,string> GetRepositories(ServerCredentials credentials)
        {
            Dictionary<string,string> result = new Dictionary<string,string>();

            // If no URL was provided, return empty result.
            if (credentials.Address == null )
            {
                return result;
            }

            IList<IRepository> repositories;
            try
            {
                repositories = Auth.Authentication.GetCmisRepositories(credentials.Address, credentials.UserName, credentials.Password.ToString());
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

            // Populate the result list with identifier and name of each repository.
            foreach (IRepository repo in repositories)
            {
                // Repo name is sometimes empty (ex: Alfresco), in such case show the repo id instead.
                string name = repo.Name.Length == 0 ? repo.Id : repo.Name;

                result.Add(repo.Id, name);
            }

            return result;
        }


        /// <summary>
        /// Get the sub-folders of a particular CMIS folder.
        /// </summary>
        /// <returns>Full path of each sub-folder, including leading slash.</returns>
        static public string[] GetSubfolders(string repositoryId, string path,
            string url, string user, string password)
        {
            List<string> result = new List<string>();

            // Connect to the CMIS repository.
            ISession session = Auth.Authentication.GetCmisSession(url, user, password, repositoryId);

            // Get the folder.
            IFolder folder;
            try
            {
                folder = (IFolder)session.GetObjectByPath(path, true);
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
        /// Folder tree.
        /// </summary>
        public class FolderTree
        {
            /// <summary>
            /// Children.
            /// </summary>
            public List<FolderTree> Children = new List<FolderTree>();

            /// <summary>
            /// Folder path.
            /// </summary>
            public string Path;

            /// <summary>
            /// Folder name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// 
            /// </summary>
            public bool Finished { get; set; }

            /// <summary>
            /// Constructor.
            /// </summary>
            public FolderTree(IList<ITree<IFileableCmisObject>> trees, IFolder folder, int depth)
            {
                this.Path = folder.Path;
                this.Name = folder.Name;
                if (depth == 0)
                {
                    this.Finished = false;
                }
                else
                {
                    this.Finished = true;
                }

                if (trees != null)
                {
                    foreach (ITree<IFileableCmisObject> tree in trees)
                    {
                        Folder f = tree.Item as Folder;
                        if (f != null)
                            this.Children.Add(new FolderTree(tree.Children, f, depth - 1));
                    }
                }
            }
        }

        /// <summary>
        /// Get the sub-folders of a particular CMIS folder.
        /// </summary>
        /// <returns>Full path of each sub-folder, including leading slash.</returns>
        static public FolderTree GetSubfolderTree(CmisRepoCredentials credentials, string path, int depth)
        {
            // Connect to the CMIS repository.
            ISession session = Auth.Authentication.GetCmisSession(credentials.Address.ToString(), credentials.UserName, credentials.Password.ToString(), credentials.RepoId);

            // Get the folder.
            IFolder folder;
            try
            {
                folder = (IFolder)session.GetObjectByPath(path, true);
            }
            catch (Exception ex)
            {
                Logger.Warn(String.Format("CmisUtils | exception when session GetObjectByPath for {0}", path), ex);
                throw;
            }

            // Debug the properties count, which allows to check whether a particular CMIS implementation is compliant or not.
            // For instance, IBM Connections is known to send an illegal count.
            Logger.Info("CmisUtils | folder.Properties.Count:" + folder.Properties.Count.ToString());
            try
            {
                IList<ITree<IFileableCmisObject>> trees = folder.GetFolderTree(depth);
                return new FolderTree(trees, folder, depth);
            }
            catch (Exception e)
            {
                Logger.Info("CmisUtils getSubFolderTree | Exception " + e.Message, e);
                throw;
            }
        }


        /// <summary>
        /// Guess the web address where files can be seen using a browser.
        /// Not bulletproof. It depends on the server, and on some servers there is no web UI at all.
        /// </summary>
        static public string GetBrowsableURL(RepoInfo repo)
        {
            if (null == repo)
            {
                throw new ArgumentNullException("repo");
            }

            // Case of Alfresco.
            string suffix1 = "alfresco/cmisatom";
            string suffix2 = "alfresco/service/cmis";
            if (repo.Address.AbsoluteUri.EndsWith(suffix1) || repo.Address.AbsoluteUri.EndsWith(suffix2))
            {
                // Detect suffix length.
                int suffixLength = 0;
                if (repo.Address.AbsoluteUri.EndsWith(suffix1))
                    suffixLength = suffix1.Length;
                if (repo.Address.AbsoluteUri.EndsWith(suffix2))
                    suffixLength = suffix2.Length;

                string root = repo.Address.AbsoluteUri.Substring(0, repo.Address.AbsoluteUri.Length - suffixLength);
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
                    ISession session = Auth.Authentication.GetCmisSession(repo.Address.ToString(), repo.User, repo.Password.ToString(), repo.RepoID);

                    if (session.RepositoryInfo.ThinClientUri == null
                        || String.IsNullOrEmpty(session.RepositoryInfo.ThinClientUri.ToString()))
                    {
                        Logger.Error("CmisUtils GetBrowsableURL | Repository does not implement ThinClientUri: " + repo.Address.AbsoluteUri);
                        return repo.Address.AbsoluteUri + repo.RemotePath;
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
                    return repo.Address.AbsoluteUri + repo.RemotePath;
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
            session.Clear(); // Clear all caches.
            session.Binding.GetRepositoryService().GetRepositoryInfos(null);
            string token = session.Binding.GetRepositoryService().GetRepositoryInfo(session.RepositoryInfo.Id, null).LatestChangeLogToken;
            Logger.DebugFormat("Server:{0} token: {1}" ,session.RepositoryInfo.ProductName, token);
            return token ?? string.Empty;
        }




        /// <summary>
        /// Should the local file/folder be made read-only?
        /// Check the permissions of the current user to the remote file/folder.
        /// </summary>
        public static void MakeReadOnlyIfCannotModifyRemote(IFileableCmisObject remoteObject, string localPath)
        {
            bool readOnly = !remoteObject.AllowableActions.Actions.Contains(PermissionMappingKeys.CanAddToFolderObject);
            if (readOnly)
            {
                new DirectoryInfo(localPath).Attributes = FileAttributes.ReadOnly;
            }
        }

        public static bool IsDocumentum(ISession session)
        {
            return session.RepositoryInfo.ProductName.Contains("Documentum");
        }


        public static bool DocumentExists(ISession session, string path)
        {
            return ItemExistsWithType(session, path, DotCMIS.Enums.BaseTypeId.CmisDocument);
        }


        public static bool FolderExists(ISession session, string path)
        {
            return ItemExistsWithType(session, path, DotCMIS.Enums.BaseTypeId.CmisFolder);
        }


        public static bool ItemExistsWithType(ISession session, string path, DotCMIS.Enums.BaseTypeId type)
        {
            try
            {
                // Try querying for this object. The object not existing is a common case, so do not log potential error.
                ICmisObject cmisObject = session.GetObjectByPath(path, false);

                if (cmisObject == null)
                {
                    return false;
                }

                return cmisObject.ObjectType.BaseTypeId.Equals(type)
                    || cmisObject.ObjectType.GetBaseType().BaseTypeId.Equals(type);
            }
            catch (Exception e)
            {
                if (e is ArgumentNullException || e is CmisObjectNotFoundException)
                {
                    // In DotCMIS, this exception actually means that the document does not exist.
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
