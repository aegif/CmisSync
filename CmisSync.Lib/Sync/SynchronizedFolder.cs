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
using log4net;

namespace CmisSync.Lib.Sync
{
    public partial class CmisRepo : RepoBase
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisRepo));


        /// <summary>
        /// Synchronization with a particular CMIS folder.
        /// </summary>
        public partial class SynchronizedFolder : IDisposable
        {
            /// <summary>
            /// Whether sync is bidirectional or only from server to client.
            /// TODO make it a CMIS folder - specific setting
            /// </summary>
            private bool BIDIRECTIONAL = true;


            /// <summary>
            /// At which degree the repository supports Change Logs.
            /// See http://docs.oasis-open.org/cmis/CMIS/v1.0/os/cmis-spec-v1.0.html#_Toc243905424
            /// The possible values are actually none, objectidsonly, properties, all
            /// But for now we only distinguish between none (false) and the rest (true)
            /// </summary>
            private bool ChangeLogCapability;


            /// <summary>
            /// Session to the CMIS repository.
            /// </summary>
            private ISession session;


            /// <summary>
            /// Path of the root in the remote repository.
            // Example: "/User Homes/nicolas.raoul/demos"
            /// </summary>
            private string remoteFolderPath;


            /// <summary>
            /// Syncing lock.
            /// true if syncing is being performed right now.
            /// TODO use is_syncing variable in parent
            /// </summary>
            private bool syncing;


            /// <summary>
            /// Parameters to use for all CMIS requests.
            /// </summary>
            private Dictionary<string, string> cmisParameters;


            /// <summary>
            /// Track whether <c>Dispose</c> has been called.
            /// </summary>
            private bool disposed = false;


            /// <summary>
            /// Database to cache remote information from the CMIS server.
            /// </summary>
            private Database database;


            /// <summary>
            /// Listener we inform about activity (used by spinner).
            /// </summary>
            private IActivityListener activityListener;


            /// <summary>
            /// Configuration of the CmisSync synchronized folder, as defined in the XML configuration file.
            /// </summary>
            private RepoInfo repoinfo;


            /// <summary>
            /// Link to parent object.
            /// </summary>
            private RepoBase repo;


            /// <summary>
            ///  Constructor for Repo (at every launch of CmisSync)
            /// </summary>
            public SynchronizedFolder(RepoInfo repoInfo,
                IActivityListener listener, RepoBase repoCmis)
            {
                if (null == repoInfo || null == repoCmis)
                {
                    throw new ArgumentNullException("repoInfo");
                }

                this.repo = repoCmis;
                this.activityListener = listener;
                this.repoinfo = repoInfo;

                // Database is the user's AppData/Roaming
                database = new Database(repoinfo.CmisDatabase);

                // Get path on remote repository.
                remoteFolderPath = repoInfo.RemotePath;

                cmisParameters = new Dictionary<string, string>();
                cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
                cmisParameters[SessionParameter.AtomPubUrl] = repoInfo.Address.ToString();
                cmisParameters[SessionParameter.User] = repoInfo.User;
                cmisParameters[SessionParameter.Password] = Crypto.Deobfuscate(repoInfo.Password); // Deobfuscate password.
                cmisParameters[SessionParameter.RepositoryId] = repoInfo.RepoID;
                cmisParameters[SessionParameter.ConnectTimeout] = "-1";

				foreach (string ignoredFolder in repoInfo.getIgnoredPaths())
				{
					Logger.Info("The folder \""+ignoredFolder+"\" will be ignored");
				}
            }


            /// <summary>
            /// Destructor.
            /// </summary>
            ~SynchronizedFolder()
            {
                Dispose(false);
            }


            /// <summary>
            /// Implement IDisposable interface. 
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }


            /// <summary>
            /// Dispose pattern implementation.
            /// </summary>
            protected virtual void Dispose(bool disposing)
            {
                if (!this.disposed)
                {
                    if (disposing)
                    {
                        this.database.Dispose();
                    }
                    this.disposed = true;
                }
            }


            /// <summary>
            /// Connect to the CMIS repository.
            /// </summary>
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
                    Logger.Info("ChangeLog capability: " + ChangeLogCapability.ToString());
                    Logger.Info("Created CMIS session: " + session.ToString());
                }
                catch (CmisRuntimeException e)
                {
                    Logger.Error("Connection to repository failed: ", e);
                }
            }


            /// <summary>
            /// Synchronize between CMIS folder and local folder.
            /// </summary>
            public void Sync()
            {
                // If not connected, connect.
                if (session == null)
                {
                    Connect();
                }
                if (session == null)
                {
                    Logger.Error("Could not connect to: " + cmisParameters[SessionParameter.AtomPubUrl]);
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


            /// <summary>
            /// Sync in the background.
            /// </summary>
            public void SyncInBackground()
            {
                if (this.syncing)
                {
                    //Logger.Debug("Sync already running in background: " + repoinfo.TargetDirectory);
                    return;
                }
                this.syncing = true;

                using (BackgroundWorker bw = new BackgroundWorker())
                {
                    bw.DoWork += new DoWorkEventHandler(
                        delegate(Object o, DoWorkEventArgs args)
                        {
                            Logger.Info("Launching sync: " + repoinfo.TargetDirectory);
#if !DEBUG
                        try
                        {
#endif
                            Sync();
#if !DEBUG
                        }
                        catch (CmisBaseException e)
                        {
                            Logger.Error("CMIS exception while syncing:", e);
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
            }


            /// <summary>
            /// Download all content from a CMIS folder.
            /// </summary>
            private void RecursiveFolderCopy(IFolder remoteFolder, string localFolder)
            {
                activityListener.ActivityStarted();
                // List all children.
                foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
                {
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        IFolder remoteSubFolder = (IFolder)cmisObject;
                        string localSubFolder = localFolder + Path.DirectorySeparatorChar.ToString() + cmisObject.Name;
                        if (Utils.WorthSyncing(localSubFolder) && !repoinfo.isPathIgnored(remoteSubFolder.Path))
                        {
                            // Create local folder.
                            Directory.CreateDirectory(localSubFolder);

                            // Create database entry for this folder
                            // TODO Add metadata
                            database.AddFolder(localSubFolder, remoteFolder.LastModificationDate);

                            // Recurse into folder.
                            RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                        }
                    }
                    else
                    {
                        if (Utils.WorthSyncing(cmisObject.Name))
                            // It is a file, just download it.
                            DownloadFile((IDocument)cmisObject, localFolder);
                    }
                }
                activityListener.ActivityStopped();
            }


            /// <summary>
            /// Download a single file from the CMIS server.
            /// </summary>
            private void DownloadFile(IDocument remoteDocument, string localFolder)
            {
                activityListener.ActivityStarted();

                string fileName = remoteDocument.ContentStreamFileName;
                Logger.Info("Downloading: " + fileName);

                // TODO: Make this configurable.
                if (remoteDocument.ContentStreamLength == 0)
                {
                    Logger.Info("Skipping download of file with content length zero: " + fileName);
                    activityListener.ActivityStopped();
                    return;
                }

                // Skip if invalid file name. See https://github.com/nicolas-raoul/CmisSync/issues/196
                if (Utils.IsInvalidFileName(fileName))
                {
                    Logger.Info("Skipping download of file with illegal filename: " + fileName);
                    activityListener.ActivityStopped();
                    return;
                }

                try
                {
                    DotCMIS.Data.IContentStream contentStream = null;
                    string filepath = Path.Combine(localFolder, fileName);
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
                            Logger.Warn("Skipping download of file with null content stream: " + fileName);
                            activityListener.ActivityStopped();
                            return;
                        }

                        DownloadStream(contentStream, tmpfilepath);

                        contentStream.Stream.Close();
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Download failed: " + fileName + " " + ex);
                        if (contentStream != null) contentStream.Stream.Close();
                        success = false;
                        File.Delete(tmpfilepath);
                    }

                    if (success)
                    {
                        Logger.Info("Downloaded: " + fileName);
                        // TODO Control file integrity by using hash compare?

                        // Get metadata.
                        Dictionary<string, string[]> metadata = null;
                        try
                        {
                            metadata = FetchMetadata(remoteDocument);
                        }
                        catch (Exception e)
                        {
                            Logger.Info("Exception while fetching metadata: " + fileName + " " + Utils.ToLogString(e));
                            // Remove temporary local document to avoid it being considered a new document.
                            File.Delete(tmpfilepath);
                            activityListener.ActivityStopped();
                            return;
                        }

                        // Remove the ".sync" suffix.
                        File.Move(tmpfilepath, filepath);

                        // Create database entry for this file.
                        database.AddFile(filepath, remoteDocument.LastModificationDate, metadata);

                        Logger.Info("Added to database: " + fileName);
                    }
                }
                catch (IOException e)
                {
                    Logger.Warn("Exception while file operation: " + Utils.ToLogString(e));
                }

                activityListener.ActivityStopped();
            }


            /// <summary>
            /// Download a file, without retrying.
            /// </summary>
            private void DownloadStream(DotCMIS.Data.IContentStream contentStream, string filePath)
            {
                using (Stream file = File.OpenWrite(filePath))
                {
                    byte[] buffer = new byte[8 * 1024];
                    int len;
                    while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        file.Write(buffer, 0, len);
                    }
                }
                contentStream.Stream.Close();
            }


            /// <summary>
            /// Upload a single file to the CMIS server.
            /// </summary>
            private void UploadFile(string filePath, IFolder remoteFolder)
            {
                activityListener.ActivityStarted();
                IDocument remoteDocument = null;
                Boolean success = false;
                try
                {
                    Logger.Info("Uploading: " + filePath);

                    // Prepare properties
                    string fileName = Path.GetFileName(filePath);
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add(PropertyIds.Name, fileName);
                    properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

                    // Prepare content stream
                    using (Stream file = File.OpenRead(filePath))
                    {
                        ContentStream contentStream = new ContentStream();
                        contentStream.FileName = fileName;
                        contentStream.MimeType = MimeType.GetMIMEType(fileName);
                        contentStream.Length = file.Length;
                        contentStream.Stream = file;

                        // Upload
                        try
                        {
                            remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            Logger.Fatal("Upload failed: " + filePath + " " + ex);
                            if (contentStream != null) {
                                contentStream.Stream.Close();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is FileNotFoundException ||
                        e is IOException)
                    {
                        Logger.Warn("File deleted while trying to upload it, reverting.");
                        // File has been deleted while we were trying to upload/checksum/add.
                        // This can typically happen in Windows Explore when creating a new text file and giving it a name.
                        // In this case, revert the upload.
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
                    
                // Metadata.
                if (success)
                {
                    Logger.Info("Uploaded: " + filePath);

                    // Get metadata. Some metadata has probably been automatically added by the server.
                    Dictionary<string, string[]> metadata = FetchMetadata(remoteDocument);

                    // Create database entry for this file.
                    database.AddFile(filePath, remoteDocument.LastModificationDate, metadata);
                }

                activityListener.ActivityStopped();
            }


            /// <summary>
            /// Upload folder recursively.
            /// After execution, the hierarchy on server will be: .../remoteBaseFolder/localFolder/...
            /// </summary>
            private void UploadFolderRecursively(IFolder remoteBaseFolder, string localFolder)
            {
                // Create remote folder.
                Dictionary<string, object> properties = new Dictionary<string, object>();
                properties.Add(PropertyIds.Name, Path.GetFileName(localFolder));
                properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");
                IFolder folder = remoteBaseFolder.CreateFolder(properties);

                // Create database entry for this folder
                // TODO Add metadata
                database.AddFolder(localFolder, folder.LastModificationDate);

                try
                {
                    // Upload each file in this folder.
                    foreach (string file in Directory.GetFiles(localFolder))
                    {
                        if (Utils.WorthSyncing(file))
                            UploadFile(file, folder);
                    }

                    // Recurse for each subfolder in this folder.
                    foreach (string subfolder in Directory.GetDirectories(localFolder))
                    {
                        string path = subfolder.Substring(repoinfo.TargetDirectory.Length);
                        path = path.Replace("\\\\","/");
                        if (Utils.WorthSyncing(subfolder) && !repoinfo.isPathIgnored(path))
                            UploadFolderRecursively(folder, subfolder);
                    }
                }
                catch (Exception e)
                {
                    if (e is System.IO.DirectoryNotFoundException ||
                        e is IOException)
                    {
                        Logger.Warn("Folder deleted while trying to upload it, reverting.");
                        // Folder has been deleted while we were trying to upload/checksum/add.
                        // In this case, revert the upload.
                        folder.DeleteTree(true, null, true);
                    }
                    else
                    {
                        throw;
                    }
                }

            }


            /// <summary>
            /// Upload new version of file.
            /// </summary>
            private void UpdateFile(string filePath, IDocument remoteFile)
            {
                try
                {
                    Logger.Info("## Updating " + filePath);
                    using (Stream localfile = File.OpenRead(filePath))
                    {
                        // Ignore files with null or empty content stream.
                        if ((localfile == null) && (localfile.Length == 0))
                        {
                            Logger.Info("Skipping update of file with null or empty content stream: " + filePath);
                            return;
                        }

                        // Prepare content stream
                        ContentStream remoteStream = new ContentStream();
                        remoteStream.FileName = remoteFile.ContentStreamFileName;
                        remoteStream.Length = localfile.Length;
                        remoteStream.MimeType = MimeType.GetMIMEType(Path.GetFileName(filePath));
                        remoteStream.Stream = localfile;
                        remoteStream.Stream.Flush();
                        Logger.Debug("before SetContentStream");

                        // CMIS do not have a Method to upload block by block. So upload file must be full.
                        // We must waiting for support of CMIS 1.1 https://issues.apache.org/jira/browse/CMIS-628
                        // http://docs.oasis-open.org/cmis/CMIS/v1.1/cs01/CMIS-v1.1-cs01.html#x1-29700019
                        // DotCMIS.Client.IObjectId objID = remoteFile.SetContentStream(remoteStream, true, true);
                        remoteFile.SetContentStream(remoteStream, true, true);
                        Logger.Debug("after SetContentStream");
                        Logger.Info("## Updated " + filePath);
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(String.Format("Exception while update file {0}: {1}", filePath, e));
                }
            }

            /// <summary>
            /// Upload new version of file content.
            /// </summary>
            private void UpdateFile(string filePath, IFolder remoteFolder)
            {
                Logger.Info("# Updating " + filePath);

                // Tell the tray icon to start spinning.
                activityListener.ActivityStarted();
                
                // Find the document within the folder.
                string fileName = Path.GetFileName(filePath);
                IDocument document = null;
                bool found = false;
                foreach (ICmisObject obj in remoteFolder.GetChildren())
                {
                    if (null != (document = obj as IDocument))
                    {
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
                    Logger.Info(filePath + " not found on server, must be uploaded instead of updated");
                    return;
                }

                // Update the document itself.
                UpdateFile(filePath, document);

                // Update timestamp in database.
                database.SetFileServerSideModificationDate(filePath, ((DateTime)document.LastModificationDate).ToUniversalTime());

                // Update checksum
                database.RecalculateChecksum(filePath);

                // TODO Update metadata?

                // Tell the tray icon to stop spinning.
                activityListener.ActivityStopped();

                this.syncing = false;
                Logger.Info("# Updated " + filePath);
            }


            /// <summary>
            /// Remove folder from local filesystem and database.
            /// </summary>
            private void RemoveFolderLocally(string folderPath)
            {
                // Folder has been deleted on server, delete it locally too.
                try
                {
                    Logger.Info("Removing remotely deleted folder: " + folderPath);
                    Directory.Delete(folderPath, true);
                }
                catch (IOException e)
                {
                    Logger.Warn(String.Format("Exception while delete tree {0}: {1}", folderPath, Utils.ToLogString(e)));
                }

                // Delete folder from database.
                if (!Directory.Exists(folderPath))
                {
                    database.RemoveFolder(folderPath);
                }
            }

            /// <summary>
            /// Retrieve the CMIS metadata of a document.
            /// </summary>
            /// <returns>a dictionary in which each key is a type id and each value is a couple indicating the mode ("readonly" or "ReadWrite") and the value itself.</returns>
            private Dictionary<string, string[]> FetchMetadata(IDocument document)
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
        }
    }
}
