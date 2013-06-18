using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using DotCMIS.Client;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Exceptions;
using DotCMIS.Enums;
using System.ComponentModel;
using System.Collections;
using DotCMIS.Data.Impl;

using System.Net;
using CmisSync.Lib.Cmis;
using DotCMIS.Data;

namespace CmisSync.Lib.Sync
{
    public partial class CmisRepo : RepoBase
    {
        public enum RulesType { Folder, File };

        /**
         * Synchronization with a particular CMIS folder.
         */
        public partial class SynchronizedFolder
        {
            /**
             * Whether sync is bidirectional or only from server to client.
             * TODO make it a CMIS folder - specific setting
             */
            private bool BIDIRECTIONAL = true;

            /**
             * At which degree the repository supports Change Logs.
             * See http://docs.oasis-open.org/cmis/CMIS/v1.0/os/cmis-spec-v1.0.html#_Toc243905424
             * The possible values are actually none, objectidsonly, properties, all
             * But for now we only distinguish between none (false) and the rest (true)
             */
            private bool ChangeLogCapability;

            /**
             * Session to the CMIS repository.
             */
            private ISession session;

            /**
             * Path of the root in the remote repository.
             * Example: "/User Homes/nicolas.raoul/demos"
             */
            private string remoteFolderPath;

            /**
             * Syncing lock.
             * true if syncing is being performed right now.
             * TODO use is_syncing variable in parent
             */
            private bool syncing = true;

            /**
             * Parameters to use for all CMIS requests.
             */
            private Dictionary<string, string> cmisParameters;

            /**
             * Database to cache remote information from the CMIS server.
             */
            private Database database;

            /**
             * Listener we inform about activity (used by spinner)
             */
            private ActivityListener activityListener;

            /**
             * Config 
             * */
            private RepoInfo repoinfo;

            /**
             * Link to parent object
             **/
            private RepoBase repo;

            /**
             * Constructor for Repo (at every launch of CmisSync)
             */
            public SynchronizedFolder(RepoInfo repoInfo,
                ActivityListener activityListener, CmisRepo repo)
            {
                this.repo = repo;
                this.activityListener = activityListener;
                this.repoinfo = repoInfo;

                // Database is the user's AppData/Roaming
                database = new Database(repoinfo.CmisDatabase);

                // Get path on remote repository.
                remoteFolderPath = repoInfo.RemotePath;

                cmisParameters = new Dictionary<string, string>();
                cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
                cmisParameters[SessionParameter.AtomPubUrl] = repoInfo.Address.ToString();
                cmisParameters[SessionParameter.User] = repoInfo.User;
                // Unprotect password
                cmisParameters[SessionParameter.Password] = Crypto.Deobfuscate(repoInfo.Password);
                cmisParameters[SessionParameter.RepositoryId] = repoInfo.RepoID;

                cmisParameters[SessionParameter.ConnectTimeout] = "-1";

                syncing = false;
            }


            /**
             * Connect to the CMIS repository.
             */
            public void Connect()
            {
                try
                {
                    // Create session factory.
                    SessionFactory factory = SessionFactory.NewInstance();
                    session = factory.CreateSession(cmisParameters);
                    // Detect whether the repository has the ChangeLog capability.
                    ChangeLogCapability = session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                            || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
                    Logger.Info("SynchronizedFolder | ChangeLog capability: " + ChangeLogCapability);
                    Logger.Info("SynchronizedFolder | Created CMIS session: " + session.ToString());
                }
                catch (CmisRuntimeException e)
                {
                    Logger.Error("SynchronizedFolder | Connection to repository failed: ", e);
                }
            }


            /**
             * Synchronize between CMIS folder and local folder.
             */
            public void Sync()
            {
                // If not connected, connect.
                if (session == null)
                {
                    Connect();
                }
                if (session == null)
                {
                    Logger.Error("SynchronizedFolder | Could not connect to: " + cmisParameters[SessionParameter.AtomPubUrl]);
                    return; // Will try again at next sync. 
                }

                IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath);

                //            if (ChangeLogCapability)              Disabled ChangeLog algorithm until this issue is solved: https://jira.nuxeo.com/browse/NXP-10844
                //            {
                //                ChangeLogSync(remoteFolder);
                //            }
                //            else
                //            {
                // No ChangeLog capability, so we have to crawl remote and local folders.
                // CrawlSync(remoteFolder, localRootFolder);
                CrawlSync(remoteFolder, repoinfo.TargetDirectory);
                //            }
            }

            /**
             * Sync in the background.
             */
            public void SyncInBackground()
            {
                if (this.syncing)
                {
                    Logger.Info(String.Format("SynchronizedFolder | [{0}] - sync is already running in background.", repoinfo.TargetDirectory));
                    return;
                }
                this.syncing = true;

                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += new DoWorkEventHandler(
                    delegate(Object o, DoWorkEventArgs args)
                    {
                        Logger.Info(String.Format("SynchronizedFolder | [{0}] - Launching sync in background, so that the UI stays available.", repoinfo.TargetDirectory));
#if !DEBUG
                        try
                        {
#endif
                            Sync();
#if !DEBUG
                        }
                        catch (CmisBaseException e)
                        {
                            Logger.Fatal("SynchronizedFolder | CMIS exception while syncing:", e);
                        }
#endif
                    }
                );
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                    delegate(object o, RunWorkerCompletedEventArgs args)
                    {
                        this.syncing = false;
                    }
                );
                bw.RunWorkerAsync();
            }


            /**
             * Download all content from a CMIS folder.
             */
            private void RecursiveFolderCopy(IFolder remoteFolder, string localFolder)
            {
                activityListener.ActivityStarted();
                // List all children.
                foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
                {
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        IFolder remoteSubFolder = (IFolder)cmisObject;
                        string localSubFolder = localFolder + Path.DirectorySeparatorChar + cmisObject.Name;
                        if (WorthSyncing(localSubFolder))
                        {

                            // Create local folder.
                            Directory.CreateDirectory(localSubFolder);

                            // Create database entry for this folder
                            // TODO - Yannick - Add metadata
                            database.AddFolder(localSubFolder, remoteFolder.LastModificationDate);

                            // Recurse into folder.
                            RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                        }
                    }
                    else
                    {
                        if (WorthSyncing(cmisObject.Name))
                            // It is a file, just download it.
                            DownloadFile((IDocument)cmisObject, localFolder);
                    }
                }
                activityListener.ActivityStopped();
            }


            /**
             * Download a single file from the CMIS server.
             */
            private void DownloadFile(IDocument remoteDocument, string localFolder)
            {
                activityListener.ActivityStarted();
                Logger.Info(String.Format("SynchronizedFolder | Download of file {0}", remoteDocument.ContentStreamFileName));

                if (remoteDocument.ContentStreamLength == 0)
                {
                    Logger.Info("SynchronizedFolder | Skipping download of file with content length zero: " + remoteDocument.ContentStreamFileName);
                    activityListener.ActivityStopped();
                    return;
                }

                DotCMIS.Data.IContentStream contentStream = null;
                string filepath = Path.Combine(localFolder, remoteDocument.ContentStreamFileName);
                string tmpfilepath = filepath + ".sync";

                // If there was previously a directory with this name, delete it.
                // TODO warn if local changes inside the folder.
                if (Directory.Exists(filepath))
                {
                    Directory.Delete(filepath);
                }

                // If file exists, delete it.
                File.Delete(filepath);
                File.Delete(tmpfilepath);

                // Download file.
                Boolean success = false;
                try
                {
                    contentStream = remoteDocument.GetContentStream();

                    // If this file does not have a content stream, ignore it.
                    // Even 0 bytes files have a contentStream.
                    // null contentStream sometimes happen on IBM P8 CMIS server, not sure why.
                    if (contentStream == null)
                    {
                        Logger.Warn("SynchronizedFolder | Skipping download of file with null content stream: " + remoteDocument.ContentStreamFileName);
                        throw new IOException();
                    }

                    DownloadStream(contentStream, tmpfilepath);

                    contentStream.Stream.Close();
                    success = true;
                }
                catch (Exception ex)
                {
                    Logger.Fatal(String.Format("SynchronizedFolder | Download of file {0} failed: {1}", remoteDocument.ContentStreamFileName, ex));
                    success = false;
                    File.Delete(tmpfilepath);
                    if (contentStream != null) contentStream.Stream.Close();
                }

                if (success)
                {
                    // TODO Control file integrity by using hash compare?

                    // Rename file
                    File.Move(tmpfilepath, filepath);

                    // Get metadata.
                    Dictionary<string, string[]> metadata = FetchMetadata(remoteDocument);

                    // Create database entry for this file.
                    database.AddFile(filepath, remoteDocument.LastModificationDate, metadata);
                }
                activityListener.ActivityStopped();
            }

            /**
             * Download a file, without retrying
             */
            private void DownloadStream(DotCMIS.Data.IContentStream contentStream, string filePath)
            {
                Stream file = File.OpenWrite(filePath);
                byte[] buffer = new byte[8 * 1024];
                int len;
                while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    file.Write(buffer, 0, len);
                }
                file.Close();
                contentStream.Stream.Close();
            }


            /**
             * Upload a single file to the CMIS server.
             */
            private void UploadFile(string filePath, IFolder remoteFolder)
            {
                activityListener.ActivityStarted();
                IDocument remoteDocument = null;
                try
                {
                    Logger.Info(String.Format("SynchronizedFolder | Uploading: {0}", filePath));

                    // Prepare properties
                    string fileName = Path.GetFileName(filePath);
                    string tmpfileName = fileName + ".sync";
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add(PropertyIds.Name, tmpfileName);
                    properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

                    Boolean success = false;

                    // Prepare content stream
                    Stream file = File.OpenRead(filePath);
                    ContentStream contentStream = new ContentStream();
                    contentStream.FileName = tmpfileName;
                    contentStream.MimeType = MimeType.GetMIMEType(fileName);
                    contentStream.Length = file.Length;
                    contentStream.Stream = file;
                    contentStream.Stream.Flush();

                    // Upload
                    try
                    {
                        try
                        {
                            string remotepath = remoteFolder.Path + '/' + tmpfileName;
                            ICmisObject obj = session.GetObjectByPath(remotepath);
                            if (obj != null)
                            {
                                Logger.Info("SynchronizedFolder | Temp file exist on remote server, so delete it");
                                remoteDocument = (IDocument)obj;
                                remoteDocument.DeleteAllVersions();
                            }
                        }
                        catch (DotCMIS.Exceptions.CmisObjectNotFoundException)
                        {
                            // Create an empty file on remote server and get ContentStream
                            Logger.Info(String.Format("SynchronizedFolder | File do not exist on remote server, so create an Empty file on the CMIS Server for {0} and launch a simple update", filePath));
                        }
                        remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Fatal(String.Format("SynchronizedFolder | Upload failed {0} : {1}", filePath, ex));
                        success = false;
                        if (contentStream != null) contentStream.Stream.Close();
                    }

                    if (success)
                    {
                        Logger.Info(String.Format("SynchronizedFolder | Upload finished: {0}", filePath));
                        if (contentStream != null) { contentStream.Stream.Close(); contentStream.Stream.Dispose(); }
                        properties[PropertyIds.Name] = fileName;
                        file.Close();

                        // Object update change ID
                        DotCMIS.Client.IObjectId objID = remoteDocument.UpdateProperties(properties, true);
                        remoteDocument = (IDocument)session.GetObject(objID);

                        // Get metadata.
                        Dictionary<string, string[]> metadata = FetchMetadata(remoteDocument);

                        // Create database entry for this file.
                        database.AddFile(filePath, remoteDocument.LastModificationDate, metadata);

                        // No close method, No dispose method
                        remoteDocument = null;
                    }
                    contentStream.Stream.Close();
                }
                catch (Exception e)
                {
                    if (e is FileNotFoundException ||
                        e is IOException)
                    {
                        Logger.Warn("SynchronizedFolder | File deleted while trying to upload it, reverting.");
                        // File has been deleted while we were trying to upload/checksum/add.
                        // This can typically happen in Windows when creating a new text file and giving it a name.
                        // Revert the upload.
                        if (remoteDocument != null)
                        {
                            remoteDocument.DeleteAllVersions();
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
                activityListener.ActivityStopped();
            }

            /**
             * Upload folder recursively.
             * After execution, the hierarchy on server will be: .../remoteBaseFolder/localFolder/...
             */
            private void UploadFolderRecursively(IFolder remoteBaseFolder, string localFolder)
            {
                // Create remote folder.
                Dictionary<string, object> properties = new Dictionary<string, object>();
                properties.Add(PropertyIds.Name, Path.GetFileName(localFolder));
                properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");
                IFolder folder = remoteBaseFolder.CreateFolder(properties);

                // Create database entry for this folder
                // TODO - Yannick - Add metadata
                database.AddFolder(localFolder, folder.LastModificationDate);

                // Upload each file in this folder.
                foreach (string file in Directory.GetFiles(localFolder))
                {
                    if (WorthSyncing(file))
                        UploadFile(file, folder);
                }

                // Recurse for each subfolder in this folder.
                foreach (string subfolder in Directory.GetDirectories(localFolder))
                {
                    if (WorthSyncing(subfolder))
                        UploadFolderRecursively(folder, subfolder);
                }
            }

            private void UpdateFile(string filePath, IDocument remoteFile)
            {
                Logger.Info("SynchronizedFolder | ## Updating " + filePath);
                Stream localfile = File.OpenRead(filePath);
                if ((localfile == null) && (localfile.Length == 0))
                {
                    Logger.Info("SynchronizedFolder | Skipping update of file with null or empty content stream: " + filePath);
                    return;
                }

                // Prepare content stream
                ContentStream remoteStream = new ContentStream();
                remoteStream.FileName = remoteFile.ContentStreamFileName;
                remoteStream.Length = localfile.Length;
                remoteStream.MimeType = MimeType.GetMIMEType(Path.GetFileName(filePath));
                remoteStream.Stream = localfile;
                remoteStream.Stream.Flush();
                Logger.Info("SynchronizedFolder | before SetContentStream");

                // CMIS do not have a Method to upload block by block. So upload file must be full.
                // We must waiting for support of CMIS 1.1 https://issues.apache.org/jira/browse/CMIS-628
                // http://docs.oasis-open.org/cmis/CMIS/v1.1/cs01/CMIS-v1.1-cs01.html#x1-29700019
                // DotCMIS.Client.IObjectId objID = remoteFile.SetContentStream(remoteStream, true, true);
                remoteFile.SetContentStream(remoteStream, true, true);
                Logger.Info("SynchronizedFolder | after SetContentStream");
                localfile.Close();
                localfile.Dispose();
                remoteStream.Stream.Close();
                Logger.Info("SynchronizedFolder | ## Updated " + filePath);

            }

            /**
             * Upload new version of file content.
             */
            private void UpdateFile(string filePath, IFolder remoteFolder)
            {
                Logger.Info("SynchronizedFolder | # Updating " + filePath);
                activityListener.ActivityStarted();
                string fileName = Path.GetFileName(filePath);

                IDocument document = null;
                bool found = false;
                foreach (ICmisObject obj in remoteFolder.GetChildren())
                {
                    if (obj is IDocument)
                    {
                        document = (IDocument)obj;
                        if (document.Name == fileName)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                // If not found, it means the document has been deleted, will be processed at the next sync cycle.
                if (!found)
                {
                    Logger.Info("SynchronizedFolder |" + filePath + " not found on server, must be uploaded instead of updated");
                    return;
                }

                UpdateFile(filePath, document);

                // Update timestamp in database.
                database.SetFileServerSideModificationDate(filePath, document.LastModificationDate);
                // Update checksum
                database.RecalculateChecksum(filePath);

                // TODO - Yannick - Update metadata ?

                activityListener.ActivityStopped();

                this.syncing = false;
                Logger.Info("SynchronizedFolder | # Updated " + filePath);
            }

            /**
             * Remove folder from local filesystem and database.
             */
            private void RemoveFolderLocally(string folderPath)
            {
                // Folder has been deleted on server, delete it locally too.
                Logger.Info("SynchronizedFolder | Removing remotely deleted folder: " + folderPath);
                Directory.Delete(folderPath, true);

                // Delete folder from database.
                database.RemoveFolder(folderPath);
            }

            public Dictionary<string, string[]> FetchMetadata(IDocument document)
            {
                Dictionary<string, string[]> metadata = new Dictionary<string, string[]>();

                IObjectType typeDef = session.GetTypeDefinition(document.ObjectType.Id/*"cmis:document" not Name FullName*/); // TODO cache
                IList<IPropertyDefinition> propertyDefs = typeDef.PropertyDefinitions;

                // Get metadata.
                foreach (IProperty property in document.Properties)
                {
                    // Mode
                    string mode = "readonly";
                    foreach (IPropertyDefinition propertyDef in propertyDefs)
                    {
                        if (propertyDef.Id.Equals("cmis:name"))
                        {
                            Updatability updatability = propertyDef.Updatability;
                            mode = updatability.ToString();
                        }
                    }

                    // Value
                    if (property.IsMultiValued)
                    {
                        metadata.Add(property.Id, new string[] { property.DisplayName, mode, property.ValuesAsString });
                    }
                    else
                    {
                        metadata.Add(property.Id, new string[] { property.DisplayName, mode, property.ValueAsString });
                    }
                }

                return metadata;
            }

            /**
             * Find an available name (potentially suffixed) for this file.
             * For instance:
             * - if /dir/file does not exist, return the same path
             * - if /dir/file exists, return /dir/file (1)
             * - if /dir/file (1) also exists, return /dir/file (2)
             * - etc
             */
            public static string SuffixIfExists(String path)
            {
                if (!File.Exists(path))
                {
                    return path;
                }
                else
                {
                    int index = 1;
                    do
                    {
                        string ret = path + " (" + index + ")";
                        if (!File.Exists(ret))
                        {
                            return ret;
                        }
                        index++;
                    }
                    while (true);
                }
            }



            /**
             * Check whether the file is worth syncing or not.
             * Files that are not worth syncing include temp files, locks, etc.
             * */
            public Boolean WorthSyncing(string filename)
            {
                filename = filename.ToLower();

                string[] filenames = new string[] {
                    "~",             // gedit and emacs
                    "thumbs.db", "desktop.ini", // Windows
                    "CVS", ".svn", ".git", ".hg", ".bzr", // Version control local settings
                    ".DS_Store", ".Icon\r\r", "._", ".Spotlight-V100", ".Trashes", // Mac OS X
                    "$~"
                };

                foreach (string ext in filenames)
                {
                    if (filename.ToLower() == ext.ToLower())
                    {
                        Logger.Info("SynchronizedFolder | Unworth syncing:" + filename);
                        return false;
                    }
                }

                string[] extensions = new string[] {
                    ".autosave", // Various autosaving apps
                    ".~lock", // LibreOffice
                    ".part", ".crdownload", // Firefox and Chromium temporary download files
                    ".sw[a-z]", ".un~", ".swp", ".swo", // vi(m)
                    ".directory", // KDE
                    ".ds_store", ".Icon\r\r", "._", ".spotlight-V100", ".trashes", // Mac OS X
                    ".(autosaved).graffle", // Omnigraffle
                    ".tmp", ".TMP", // MS Office
                    ".~ppt", ".~pptx",
                    ".~xls", ".~xlsx",
                    ".~doc", ".~docx",
                    ".cvsignore", ".~cvsignore",// CVS
                    ".gitignore", // GIT
                    ".sync", // CmisSync File Downloading/Uploading
                    ".cmissync" // CmisSync Database 
                };
                // TODO: Consider these ones as well:
                //string[] ExcludeRules = new string[] {
                //    "*.autosave", // Various autosaving apps
                //    "*~", // gedit and emacs
                //    ".~lock.*", // LibreOffice
                //    "*.part", "*.crdownload", // Firefox and Chromium temporary download files
                //    ".*.sw[a-z]", "*.un~", "*.swp", "*.swo", // vi(m)
                //    ".directory", // KDE
                //    ".DS_Store", "Icon\r", "._*", ".Spotlight-V100", ".Trashes", // Mac OS X
                //    "*(Autosaved).graffle", // Omnigraffle
                //    "Thumbs.db", "Desktop.ini", // Windows
                //    "~*.tmp", "~*.TMP", "*~*.tmp", "*~*.TMP", // MS Office
                //    "~*.ppt", "~*.PPT", "~*.pptx", "~*.PPTX",
                //    "~*.xls", "~*.XLS", "~*.xlsx", "~*.XLSX",
                //    "~*.doc", "~*.DOC", "~*.docx", "~*.DOCX",
                //    "*/CVS/*", ".cvsignore", "*/.cvsignore", // CVS
                //    "/.svn/*", "*/.svn/*", // Subversion
                //    "/.hg/*", "*/.hg/*", "*/.hgignore", // Mercurial
                //    "/.bzr/*", "*/.bzr/*", "*/.bzrignore" // Bazaar
                //};

                string filext = Path.GetExtension(filename);
                foreach (string ext in extensions)
                {
                    if (filext.Equals(ext))
                    {
                        Logger.Info("SynchronizedFolder | Unworth syncing:" + filename);
                        return false;
                    }
                }
                Logger.Info("SynchronizedFolder | Worth syncing:" + filename);
                return true;
            }
        }
    }
}
