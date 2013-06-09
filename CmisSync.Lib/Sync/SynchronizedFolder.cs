﻿using System;
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
             * Local folder where the changes are synchronized to.
             * Example: "C:\CmisSync"
             */
            // Store in repoInfo
            // private string localRootFolder;

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
                cmisParameters[SessionParameter.Password] = Crypto.Unprotect(repoInfo.Password);
                cmisParameters[SessionParameter.RepositoryId] = repoInfo.RepoID;

                cmisParameters[SessionParameter.ConnectTimeout] = "-1";

                syncing = false;

            }


            /**
             * Connect to the CMIS repository.
             */
            public void Connect()
            {
                do
                {
                    try
                    {
                        // Create session factory.
                        SessionFactory factory = SessionFactory.NewInstance();
                        session = factory.CreateSession(cmisParameters);
                        // Detect whether the repository has the ChangeLog capability.
                        ChangeLogCapability = session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                                || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
                        Logger.Info("Sync | ChangeLog capability: " + ChangeLogCapability);
                        Logger.Info("Sync | Created CMIS session: " + session.ToString());
                    }
                    catch (CmisRuntimeException e)
                    {
                        Logger.Fatal("Sync | Exception: ", e);
                    }
                    if (session == null)
                    {
                        Logger.Warn("Sync | Connection failed, waiting for 10 seconds: " + repoinfo.TargetDirectory + "(" + cmisParameters[SessionParameter.AtomPubUrl] + ")");
                        System.Threading.Thread.Sleep(10 * 1000);
                    }
                }
                while (session == null);
            }



            /**
             * Synchronize between CMIS folder and local folder.
             */
            public void Sync()
            {
                // If not connected, connect.
                if (session == null)
                    Connect();

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
                    Logger.Info(String.Format("Sync | [{0}] - sync is already running in background.", repoinfo.TargetDirectory));
                    return;
                }
                this.syncing = true;

                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += new DoWorkEventHandler(
                    delegate(Object o, DoWorkEventArgs args)
                    {
                        Logger.Info(String.Format("Sync | [{0}] - Launching sync in background, so that the UI stays available.", repoinfo.TargetDirectory));
#if !DEBUG
                        try
                        {
#endif
                            Sync();
#if !DEBUG
                        }
                        catch (CmisBaseException e)
                        {
                            Logger.Fatal("Sync | CMIS exception while syncing:", e);
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
                        if (CheckRules(localSubFolder, RulesType.Folder))
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
                        if (CheckRules(cmisObject.Name, RulesType.File))
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

                if (remoteDocument.ContentStreamLength == 0)
                {
                    Logger.Info("CmisDirectory | Skipping download of file with null content stream: " + remoteDocument.ContentStreamFileName);
                    activityListener.ActivityStopped();
                    return;
                }

                StreamWriter localfile = null;
                DotCMIS.Data.IContentStream contentStream = null;

                string filepath = Path.Combine(localFolder, remoteDocument.ContentStreamFileName);

                // If a file exist, file is deleted.
                if (File.Exists(filepath))
                    File.Delete(filepath);

                string tmpfilepath = filepath + ".sync";

                // Download file, starting at the last download point
                Boolean success = false;
                try
                {
                    // Get the last position in the localfile. By default position 0 (Nuxeo do not support partial getContentStream #107
                    Int64 Offset = 0;

                    // Nuxeo don't support partial getContentStream
                    if (session.RepositoryInfo.VendorName.ToLower().Contains("nuxeo"))
                    {
                        Logger.Warn("CmisDirectory | Nuxeo does not support partial download, so restart from zero.");
                        localfile = new StreamWriter(tmpfilepath);
                        contentStream = remoteDocument.GetContentStream();
                    }
                    else
                    {
                        // Create Stream with the local file in append mode, if file is empty it's like a full download (Offset 0)
                        localfile = new StreamWriter(tmpfilepath, true);
                        localfile.AutoFlush = true;
                        Offset = localfile.BaseStream.Position;
                        contentStream = remoteDocument.GetContentStream(remoteDocument.Id, Offset, remoteDocument.ContentStreamLength);
                    }

                    if (contentStream == null)
                    {
                        Logger.Warn("CmisDirectory | Skipping download of file with null content stream: " + remoteDocument.ContentStreamFileName);
                        throw new IOException();
                    }

                    Logger.Info(String.Format("CmisDirectory | Start download of file with offset {0}", Offset));

                    contentStream.Stream.Flush();
                    CopyStream(contentStream.Stream, localfile.BaseStream);
                    localfile.Flush();
                    localfile.Close();
                    contentStream.Stream.Close();
                    success = true;
                }
                catch (Exception ex)
                {
                    Logger.Fatal(String.Format("CmisDirectory | Download of file {0} abort: {1}", remoteDocument.ContentStreamFileName, ex));
                    success = false;
                    if (localfile != null)
                    {
                        localfile.Flush();
                        localfile.Close();
                        File.Delete(tmpfilepath);
                    }
                    if (contentStream != null) contentStream.Stream.Close();
                }

                try
                {
                    // Rename file
                    // TODO - Yannick - Control file integrity by using hash compare - Is it necessary ?
                    if (success)
                    {
                        File.Move(tmpfilepath, filepath);

                        // Get metadata.
                        Dictionary<string, string[]> metadata = FetchMetadata(remoteDocument);

                        // Create database entry for this file.
                        database.AddFile(filepath, remoteDocument.LastModificationDate, metadata);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Fatal("CmisDirectory | Unable to write metadata in the CmisDatabase: " + ex.ToString());
                }
                activityListener.ActivityStopped();
            }

            private void CopyStream(Stream src, Stream dst)
            {
                byte[] buffer = new byte[8 * 1024];
                while (true)
                {
                    int read = src.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return;
                    dst.Write(buffer, 0, read);
                }
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
                    Logger.Info(String.Format("Sync | Start upload of file {0}", filePath));

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
                                Logger.Info("Sync | Temp file exist on remote server, so delete it");
                                remoteDocument = (IDocument)obj;
                                remoteDocument.DeleteAllVersions();
                            }
                        }
                        catch (DotCMIS.Exceptions.CmisObjectNotFoundException)
                        {
                            // Create an empty file on remote server and get ContentStream
                            Logger.Info(String.Format("Sync | File do not exist on remote server, so create an Empty file on the CMIS Server for {0} and launch a simple update", filePath));
                        }
                        remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Fatal(String.Format("Sync | Upload of file {0} abort: {1}", filePath, ex));
                        success = false;
                        if (contentStream != null) contentStream.Stream.Close();
                    }

                    if (success)
                    {
                        Logger.Info(String.Format("Sync | Upload of file {0} finished", filePath));
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
                        Logger.Warn("Sync | File deleted while trying to upload it, reverting.");
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
                    if (CheckRules(file, RulesType.File))
                        UploadFile(file, folder);
                }

                // Recurse for each subfolder in this folder.
                foreach (string subfolder in Directory.GetDirectories(localFolder))
                {
                    if (CheckRules(subfolder, RulesType.Folder))
                        UploadFolderRecursively(folder, subfolder);
                }
            }

            private void UpdateFile(string filePath, IDocument remoteFile)
            {
                Logger.Info("CmisDirectory | ## Updating " + filePath);
                Stream localfile = File.OpenRead(filePath);
                if ((localfile == null) && (localfile.Length == 0))
                {
                    Logger.Info("Sync | Skipping update of file with null or empty content stream: " + filePath);
                    return;
                }

                // Prepare content stream
                ContentStream remoteStream = new ContentStream();
                remoteStream.FileName = remoteFile.ContentStreamFileName;
                remoteStream.Length = localfile.Length;
                remoteStream.MimeType = MimeType.GetMIMEType(Path.GetFileName(filePath));
                remoteStream.Stream = localfile;
                remoteStream.Stream.Flush();
                Logger.Info("CmisDirectory | before SetContentStream");

                // CMIS do not have a Method to upload block by block. So upload file must be full.
                // We must waiting for support of CMIS 1.1 https://issues.apache.org/jira/browse/CMIS-628
                // http://docs.oasis-open.org/cmis/CMIS/v1.1/cs01/CMIS-v1.1-cs01.html#x1-29700019
                // DotCMIS.Client.IObjectId objID = remoteFile.SetContentStream(remoteStream, true, true);
                remoteFile.SetContentStream(remoteStream, true, true);
                Logger.Info("CmisDirectory | after SetContentStream");
                localfile.Close();
                localfile.Dispose();
                remoteStream.Stream.Close();
                Logger.Info("CmisDirectory | ## Updated " + filePath);

            }

            /**
             * Upload new version of file content.
             */
            private void UpdateFile(string filePath, IFolder remoteFolder)
            {
                Logger.Info("CmisDirectory | # Updating " + filePath);
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
                    Logger.Info("CmisDirectory |" + filePath + " not found on server, must be uploaded instead of updated");
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
                Logger.Info("CmisDirectory | # Updated " + filePath);
            }

            /**
             * Remove folder from local filesystem and database.
             */
            private void RemoveFolderLocally(string folderPath)
            {
                // Folder has been deleted on server, delete it locally too.
                Logger.Info("Sync | Removing remotely deleted folder: " + folderPath);
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
                foreach(IProperty property in document.Properties)
                {
                    // Mode
                    string mode = "readonly";
                    foreach (IPropertyDefinition propertyDef in propertyDefs)
                    {
                        if(propertyDef.Id.Equals("cmis:name"))
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
             * Check if the filename provide is compliance
             * Return true if path is ok, or false is path contains one or more rule
             * */
            public Boolean CheckRules(string path, RulesType ruletype)
            {
                string[] contents = new string[] {
                "~",             // gedit and emacs
                "Thumbs.db", "Desktop.ini","desktop.ini","thumbs.db", // Windows
                "$~"
            };

                string[] extensions = new string[] {
            ".autosave", // Various autosaving apps
            ".~lock", // LibreOffice
            ".part", ".crdownload", // Firefox and Chromium temporary download files
            ".sw[a-z]", ".un~", ".swp", ".swo", // vi(m)
            ".directory", // KDE
            ".DS_Store", ".Icon\r\r", "._", ".Spotlight-V100", ".Trashes", // Mac OS X
            ".(Autosaved).graffle", // Omnigraffle
            ".tmp", ".TMP", // MS Office
            ".~ppt", ".~pptx",
            ".~xls", ".~xlsx",
            ".~doc", ".~docx",
            ".cvsignore", ".~cvsignore",// CVS
            ".gitignore", // GIT
            ".sync", // CmisSync File Downloading/Uploading
            ".cmissync" // CmisSync Database 
            };

                string[] directories = new string[] {
                "CVS",".svn",".git",".hg",".bzr",".DS_Store", ".Icon\r\r", "._", ".Spotlight-V100", ".Trashes" // Mac OS X
            };

                //Logger.LogInfo("SyncRules", "Check rules for " + path);
                Boolean found = false;
                foreach (string content in contents)
                {
                    if (path.Contains(content)) found = true;
                }

                if (ruletype == RulesType.Folder)
                {
                    foreach (string dir in directories)
                    {
                        if (path.ToLower().Contains(dir)) found = true;
                    }
                }
                else
                {
                    foreach (string ext in extensions)
                    {
                        string filext = Path.GetExtension(path);
                        if (filext.ToLower() == ext.ToLower()) found = true;
                    }
                }

                string not = string.Empty;
                if (found) not = " not";

                //Logger.LogInfo("SyncRules", String.Format("Path" + path + " is{0} ok", not));
                return !found;

            }
        }
    }

}
