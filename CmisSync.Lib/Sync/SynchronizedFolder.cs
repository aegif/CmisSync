using CmisSync.Lib.Cmis;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;
using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;

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
            // Log
            private static readonly ILog Logger = LogManager.GetLogger(typeof(SynchronizedFolder));

            /// <summary>
            /// Interval for which sync will wait while paused before retrying sync.
            /// </summary>
            private static readonly int SYNC_SUSPEND_SLEEP_INTERVAL = 5 * 1000; //five seconds

            /// <summary>
            /// An object for locking the sync method (one thread at a time can run sync).
            /// </summary>
            private Object syncLock = new Object();

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
            /// Example: "/User Homes/nicolas.raoul/demos"
            /// </summary>
            private string remoteFolderPath;


            /// <summary>
            /// Syncing lock.
            /// true if syncing (or pause) is being performed right now.
            /// TODO use is_syncing variable in parent
            /// </summary>
            private bool syncing;


            /// <summary>
            /// Whether sync is actually being in pause right now.
            /// This is different from CmisRepo.Status, which means "paused, or will be paused as soon as possible"
            /// </summary>
            private bool suspended = false;


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
            /// Configuration of the CmisSync synchronized folder, as defined in the XML configuration file.
            /// </summary>
            private RepoInfo repoinfo;


            /// <summary>
            /// Link to parent object.
            /// </summary>
            private RepoBase repo;

            /// <summary>
            /// Set for first sync.
            /// </summary>
            private bool firstSync = false;




            /// <summary>
            ///  Constructor for Repo (at every launch of CmisSync)
            /// </summary>
            public SynchronizedFolder(RepoInfo repoInfo, RepoBase repoCmis)
            {
                if (null == repoInfo || null == repoCmis)
                {
                    throw new ArgumentNullException("repoInfo");
                }

                this.repo = repoCmis;
                this.repoinfo = repoInfo;

                // Database is the user's AppData/Roaming
                database = new Database(repoinfo.CmisDatabase);

                // Get path on remote repository.
                remoteFolderPath = repoInfo.RemotePath;

                cmisParameters = new Dictionary<string, string>();
                cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
                cmisParameters[SessionParameter.AtomPubUrl] = repoInfo.Address.ToString();
                cmisParameters[SessionParameter.User] = repoInfo.User;
                cmisParameters[SessionParameter.Password] = repoInfo.Password.ToString();
                cmisParameters[SessionParameter.RepositoryId] = repoInfo.RepoID;
                cmisParameters[SessionParameter.ConnectTimeout] = "-1";

                foreach (string ignoredFolder in repoInfo.getIgnoredPaths())
                {
                    Logger.Info("The folder \"" + ignoredFolder + "\" will be ignored");
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
                // Create session factory.
                SessionFactory factory = SessionFactory.NewInstance();
                session = factory.CreateSession(cmisParameters);
                // Detect whether the repository has the ChangeLog capability.
                ChangeLogCapability = session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                        || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
                Logger.Info("ChangeLog capability: " + ChangeLogCapability.ToString());
                Logger.Info("Created CMIS session: " + session.ToString());
            }


            /// <summary>
            /// Whether this folder's synchronization is running right now.
            /// </summary>
            public bool isSyncing()
            {
                return this.syncing;
            }


            /// <summary>
            /// Whether this folder's synchronization is suspended right now.
            /// </summary>
            public bool isSuspended()
            {
                return this.suspended;
            }


            /// <summary>
            /// Synchronize between CMIS folder and local folder.
            /// </summary>
            public void Sync()
            {
                Sync(true);
            }


            /// <summary>
            /// Synchronize between CMIS folder and local folder.
            /// </summary>
            public void Sync(bool syncFull)
            {
                lock (syncLock)
                {
                    this.syncing = true;
                    repo.OnSyncStart(syncFull);

                    // If not connected, connect.
                    if (session == null)
                    {
                        Connect();
                        firstSync = true;
                    }

                    IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath);
                    string localFolder = repoinfo.TargetDirectory;

                    if (firstSync)
                    {
                        CrawlSync(remoteFolder, localFolder);
                        firstSync = false;
                    }
                    else
                    {
                    // ChangeLog is not ready yet.
                    //    if (ChangeLogCapability)
                    //    {
                    //        // ChangeLog sync...
                    //        ChangeLogSync(remoteFolder);
                    //        WatcherSync(remoteFolderPath, localFolder);
                    //    }
                    //    else
                    //    {
                            // No ChangeLog capability, so we have to crawl remote and local folders.
                            WatcherSync(remoteFolderPath, localFolder);
                        
                            if (syncFull)
                            {
                                CrawlSync(remoteFolder, localFolder);
                            }
                     //   }
                    }
                }
            }

            /// <summary>
            /// Synchronize has completed.
            /// </summary>
            public void SyncComplete(bool syncFull)
            {
                lock (syncLock)
                {
                    repo.OnSyncComplete(syncFull);
                    this.syncing = false;
                }
            }

            /// <summary>
            /// Sync in the background.
            /// </summary>
            public void SyncInBackground(bool syncFull)
            {
                if (this.syncing)
                {
                    Logger.Debug("Sync already running in background: " + repoinfo.TargetDirectory);
                    return;
                }

                using (BackgroundWorker bw = new BackgroundWorker())
                {
                    bw.DoWork += new DoWorkEventHandler(
                        delegate(Object o, DoWorkEventArgs args)
                        {
                            try
                            {
                                Sync(syncFull);
                            }
                            catch (CmisPermissionDeniedException e)
                            {
                                repo.OnSyncError(new PermissionDeniedException("Authentication failed.", e));
                            }
                            catch (Exception e)
                            {
                                repo.OnSyncError(new BaseException(e));
                            }
                        }
                    );
                    bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                        delegate(object o, RunWorkerCompletedEventArgs args)
                        {
                            SyncComplete(syncFull);
                        }
                    );
                    bw.RunWorkerAsync();
                }
            }

            /// <summary>
            /// Handle CMIS Exception.
            /// </summary>
            private void ProcessRecoverableException(string logMessage, Exception exception)
            {
                bool recoverable;
                // Exceptions: http://docs.oasis-open.org/cmis/CMIS/v1.0/cs01/cmis-spec-v1.0.html#_Toc243905433
                if (exception is CmisInvalidArgumentException)
                {
                    // One or more of the input parameters to the service method is missing or invalid (any method)
                    recoverable = true;
                }
                else if (exception is CmisObjectNotFoundException)
                {
                    // The service call has specified an object that does not exist in the Repository (any method)
                    recoverable = true;
                }
                else if (exception is CmisNotSupportedException)
                {
                    // The service method invoked requires an optional capability not supported by the repository (any method)
                    recoverable = false;
                }
                else if (exception is CmisPermissionDeniedException)
                {
                    // The caller of the service method does not have sufficient permissions to perform the operation (any method)
                    recoverable = false;
                }
                else if (exception is CmisRuntimeException)
                {
                    // Any other cause not expressible by another CMIS exception (any method)
                    recoverable = false;
                }
                else if (exception is CmisConstraintException)
                {
                    // The operation violates a Repository- or Object-level constraint defined in the CMIS domain model (write methods)
                    recoverable = true;
                }
                else if (exception is CmisContentAlreadyExistsException)
                {
                    // The operation attempts to set the content stream for a Document that already has a content stream without explicitly specifying the �overwriteFlag� parameter (setContentStream method)
                    recoverable = true;
                }
                else if (exception is CmisFilterNotValidException)
                {
                    // The property filter or rendition filter input to the operation is not valid (read methods)
                    recoverable = true;
                }
                else if (exception is CmisNameConstraintViolationException)
                {
                    // The repository is not able to store the object that the user is creating/updating due to a name constraint violation (write methods)
                    recoverable = true;
                }
                else if (exception is CmisStorageException)
                {
                    // The repository is not able to store the object that the user is creating/updating due to an internal storage problem (write methods)
                    recoverable = true;
                }
                else if (exception is CmisStreamNotSupportedException)
                {
                    // The operation is attempting to get or set a contentStream for a Document whose Object-type specifies that a content stream is not allowed for Document�s of that type (write methods)
                    recoverable = true;
                }
                else if (exception is CmisUpdateConflictException)
                {
                    // The operation is attempting to update an object that is no longer current (as determined by the repository) (write methods)
                    recoverable = true;
                }
                else if (exception is CmisVersioningException)
                {
                    // The operation is attempting to perform an action on a non-current version of a Document that cannot be performed on a non-current version.
                    recoverable = true;
                }
                else if (exception is CmisConnectionException)
                {
                    // Client unable to connect to server
                    recoverable = false;
                }
                else if (exception is IOException)
                {
                    // IO Exception
                    recoverable = true;
                }
                else if (exception is UnauthorizedAccessException)
                {
                    // Unable to access file/directory
                    recoverable = true;
                }
                else if (exception is ArgumentException)
                {
                    // File contains characters not valid for .NET, for instance carriage return.
                    recoverable = true;
                }
                else
                {
                    // All other errors...
                    recoverable = false;
                }

                Logger.Error(logMessage, exception);

                if ( ! recoverable)
                {
                    throw exception;
                }
            }

            /// <summary>
            /// Download all content from a CMIS folder.
            /// </summary>
            private void RecursiveFolderCopy(IFolder remoteFolder, string localFolder)
            {
                SleepWhileSuspended();

                IItemEnumerable<ICmisObject> children;
                try
                {
                    children = remoteFolder.GetChildren();
                }
                catch (CmisBaseException e)
                {
                    ProcessRecoverableException("Could not get children objects: " + remoteFolder.Path, e);
                    return;
                }

                // List all children.
                foreach (ICmisObject cmisObject in children)
                {
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        IFolder remoteSubFolder = (IFolder)cmisObject;
                        string localSubFolder = Path.Combine(localFolder, cmisObject.Name);
                        if (Utils.WorthSyncing(localFolder, remoteSubFolder.Name, repoinfo))
                        {
                            try
                            {
                                // Create local folder.
                                Directory.CreateDirectory(localSubFolder);
                            }
                            catch (Exception e)
                            {
                                ProcessRecoverableException("Could not create directory: " + localSubFolder, e);
                                continue;
                            }

                            // Create database entry for this folder
                            // TODO Add metadata
                            database.AddFolder(localSubFolder, remoteFolder.LastModificationDate);
                            Logger.Info("Added folder to database: " + localSubFolder);

                            // Recurse into folder.
                            RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                        }
                    }
                    else if (cmisObject is DotCMIS.Client.Impl.Document)
                    {
                        if (Utils.WorthSyncing(localFolder, cmisObject.Name, repoinfo))
                        {
                            // It is a file, just download it.
                            DownloadFile((IDocument)cmisObject, localFolder);
                        }
                    }
                    else
                    {
                        Logger.Warn("Unknown object type: " + cmisObject.ObjectType.DisplayName);
                    }
                }
            }


            /// <summary>
            /// Download a single file from the CMIS server.
            /// </summary>
            private bool DownloadFile(IDocument remoteDocument, string localFolder)
            {
                SleepWhileSuspended();

                string fileName = remoteDocument.ContentStreamFileName;
                Logger.Info("Downloading: " + fileName);

                // Skip if invalid file name. See https://github.com/nicolas-raoul/CmisSync/issues/196
                if (Utils.IsInvalidFileName(fileName))
                {
                    Logger.Info("Skipping download of file with illegal filename: " + fileName);
                    return true;
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
                    byte[] filehash = { };
                    try
                    {
                        contentStream = remoteDocument.GetContentStream();

                        // If this file does not have a content stream, ignore it.
                        // Even 0 bytes files have a contentStream.
                        // null contentStream sometimes happen on IBM P8 CMIS server, not sure why.
                        if (contentStream == null)
                        {
                            Logger.Warn("Skipping download of file with null content stream: " + fileName);
                            return true;
                        }
                        // Skip downloading the content, just go on with an empty file
                        if (remoteDocument.ContentStreamLength == 0)
                        {
                            Logger.Info("Skipping download of file with content length zero: " + fileName);
                            using (FileStream s = File.Create(tmpfilepath))
                            {
                                s.Close();
                            }
                        }
                        else
                        {
                            filehash = DownloadStream(contentStream, tmpfilepath);
                            contentStream.Stream.Close();
                        }
                        success = true;
                    }
                    catch (CmisBaseException e)
                    {
                        ProcessRecoverableException("Download failed: " + fileName, e);
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
                        catch (CmisBaseException e)
                        {
                            ProcessRecoverableException("Could not fetch metadata: " + fileName, e);
                            // Remove temporary local document to avoid it being considered a new document.
                            File.Delete(tmpfilepath);
                            return false;
                        }

                        // Remove the ".sync" suffix.
                        File.Move(tmpfilepath, filepath);

                        // Create database entry for this file.
                        database.AddFile(filepath, remoteDocument.LastModificationDate, metadata, filehash);
                        Logger.Info("Added file to database: " + filepath);
                    }
                    return success;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not download file: " + Path.Combine(localFolder, fileName), e);
                    return false;
                }
            }


            /// <summary>
            /// Download a file, without retrying.
            /// </summary>
            private byte[] DownloadStream(DotCMIS.Data.IContentStream contentStream, string filePath)
            {
                byte[] hash = { };
                using (Stream file = File.OpenWrite(filePath))
                using (SHA1 hashAlg = new SHA1Managed())
                using (CryptoStream hashstream = new CryptoStream(file, hashAlg, CryptoStreamMode.Write))
                {
                    byte[] buffer = new byte[8 * 1024];
                    int len;
                    while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        hashstream.Write(buffer, 0, len);
                    }
                    hashstream.FlushFinalBlock();
                    hash = hashAlg.Hash;
                }
                contentStream.Stream.Close();
                return hash;
            }


            /// <summary>
            /// Upload a single file to the CMIS server.
            /// </summary>
            private bool UploadFile(string filePath, IFolder remoteFolder)
            {
                SleepWhileSuspended();

                Logger.Info("Uploading: " + filePath);

                try
                {
                    IDocument remoteDocument = null;
                    byte[] filehash = { };

                    // Prepare properties
                    string fileName = Path.GetFileName(filePath);
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add(PropertyIds.Name, fileName);
                    properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

                    // Prepare content stream
                    using (Stream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (SHA1 hashAlg = new SHA1Managed())
                    using (CryptoStream hashstream = new CryptoStream(file, hashAlg, CryptoStreamMode.Read))
                    {
                        ContentStream contentStream = new ContentStream();
                        contentStream.FileName = fileName;
                        contentStream.MimeType = MimeType.GetMIMEType(fileName);
                        contentStream.Length = file.Length;
                        contentStream.Stream = hashstream;

                        remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                        filehash = hashAlg.Hash;
                    }

                    // Metadata.
                    Logger.Info("Uploaded: " + filePath);

                    // Get metadata. Some metadata has probably been automatically added by the server.
                    Dictionary<string, string[]> metadata = metadata = FetchMetadata(remoteDocument);

                    // Create database entry for this file.
                    database.AddFile(filePath, remoteDocument.LastModificationDate, metadata, filehash);
                    Logger.Info("Added file to database: " + filePath);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not upload file: " + filePath, e);
                    return false;
                }
            }

            /// <summary>
            /// Upload folder recursively.
            /// After execution, the hierarchy on server will be: .../remoteBaseFolder/localFolder/...
            /// </summary>
            private void UploadFolderRecursively(IFolder remoteBaseFolder, string localFolder)
            {
                SleepWhileSuspended();

                IFolder folder;
                try
                {
                    // Create remote folder.
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add(PropertyIds.Name, Path.GetFileName(localFolder));
                    properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");
                    folder = remoteBaseFolder.CreateFolder(properties);

                    // Create database entry for this folder
                    // TODO Add metadata
                    database.AddFolder(localFolder, folder.LastModificationDate);
                    Logger.Info("Added folder to database: " + localFolder);
                }
                catch (CmisBaseException e)
                {
                    ProcessRecoverableException("Could not create remote directory: " + remoteBaseFolder.Path + "/" + Path.GetFileName(localFolder), e);
                    return;
                }

                try
                {
                    // Upload each file in this folder.
                    foreach (string file in Directory.GetFiles(localFolder))
                    {
                        if (Utils.WorthSyncing(localFolder, Path.GetFileName(file), repoinfo))
                        {
                            UploadFile(file, folder);
                        }
                    }

                    // Recurse for each subfolder in this folder.
                    foreach (string subfolder in Directory.GetDirectories(localFolder))
                    {
                        if (Utils.WorthSyncing(localFolder, Path.GetFileName(subfolder), repoinfo))
                        {
                            UploadFolderRecursively(folder, subfolder);
                        }
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not uploading folder: " + localFolder, e);
                }
            }


            /// <summary>
            /// Upload new version of file.
            /// </summary>
            private bool UpdateFile(string filePath, IDocument remoteFile)
            {
                SleepWhileSuspended();
                try
                {
                    Logger.Info("Updating: " + filePath);
                    using (Stream localfile = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        // Ignore files with null or empty content stream.
                        if ((localfile == null) && (localfile.Length == 0))
                        {
                            Logger.Info("Skipping update of file with null or empty content stream: " + filePath);
                            return true;
                        }

                        // Check if the file is Check out or not
                        if (!(bool)remoteFile.IsVersionSeriesCheckedOut)
                        {

                            // Prepare content stream
                            ContentStream remoteStream = new ContentStream();
                            remoteStream.FileName = remoteFile.ContentStreamFileName;
                            remoteStream.Length = localfile.Length;
                            remoteStream.MimeType = remoteFile.GetContentStream().MimeType;
                            remoteStream.Stream = localfile;
                            remoteStream.Stream.Flush();
                            Logger.Debug("before SetContentStream");

                            // CMIS do not have a Method to upload block by block. So upload file must be full.
                            // We must waiting for support of CMIS 1.1 https://issues.apache.org/jira/browse/CMIS-628
                            // http://docs.oasis-open.org/cmis/CMIS/v1.1/cs01/CMIS-v1.1-cs01.html#x1-29700019
                            // DotCMIS.Client.IObjectId objID = remoteFile.SetContentStream(remoteStream, true, true);
                            remoteFile.SetContentStream(remoteStream, true, true);

                            Logger.Debug("after SetContentStream");

                            // Update timestamp in database.
                            database.SetFileServerSideModificationDate(filePath, ((DateTime)remoteFile.LastModificationDate).ToUniversalTime());

                            // Update checksum
                            database.RecalculateChecksum(filePath);

                            // TODO Update metadata?
                            Logger.Info("Updated: " + filePath);
                            return true;
                        }
                        else
                        {
                            throw new IOException("File is Check Out on the server");
                        }
                    }
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not update file: " + filePath, e);
                    return false;
                }
            }

            /// <summary>
            /// Upload new version of file content.
            /// </summary>
            private bool UpdateFile(string filePath, IFolder remoteFolder)
            {
                SleepWhileSuspended();
                try
                {
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

                    // If not found, it means the document has been deleted.
                    if (!found)
                    {
                        Logger.Info(filePath + " not found on server, must be uploaded instead of updated");
                        return UploadFile(filePath, remoteFolder);
                    }

                    // Update the document itself.
                    return UpdateFile(filePath, document);
                }
                catch (CmisBaseException e)
                {
                    ProcessRecoverableException("Could not update file: " + filePath, e);
                    return false;
                }
            }


            /// <summary>
            /// Remove folder from local filesystem and database.
            /// </summary>
            private bool RemoveFolderLocally(string folderPath)
            {
                SleepWhileSuspended();
                // Folder has been deleted on server, delete it locally too.
                try
                {
                    Logger.Info("Removing remotely deleted folder: " + folderPath);
                    Directory.Delete(folderPath, true);
                }
                catch (Exception e)
                {
                    ProcessRecoverableException("Could not delete tree:" + folderPath, e);
                    return false;
                }

                // Delete folder from database.
                if (!Directory.Exists(folderPath))
                {
                    database.RemoveFolder(folderPath);
                }

                return true;
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

            /// <summary>
            /// Rename a file remotely.
            /// </summary>
            private bool RenameFile(string directory, string newFilename, IDocument remoteFile)
            {
                SleepWhileSuspended();

                string oldPathname = Path.Combine(directory, remoteFile.Name);
                string newPathname = Path.Combine(directory, newFilename);
                try
                {

                    Logger.InfoFormat("Renaming: {0} -> {1}", oldPathname, newPathname);

                    IDictionary<string, object> properties = new Dictionary<string, object>();
                    properties[PropertyIds.Name] = newFilename;

                    IDocument updatedDocument = (IDocument)remoteFile.UpdateProperties(properties);

                    // Update the path in the database...
                    database.MoveFile(oldPathname, newPathname);

                    // Update timestamp in database.
                    database.SetFileServerSideModificationDate(newPathname, ((DateTime)updatedDocument.LastModificationDate).ToUniversalTime());

                    Logger.InfoFormat("Renamed file: {0} -> {1}", oldPathname, newPathname);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException(String.Format("Could not rename file: {0} -> {1}", oldPathname, newPathname), e);
                    return false;
                }
            }

            /// <summary>
            /// Rename a folder remotely.
            /// </summary>
            private bool RenameFolder(string directory, string newFilename, IFolder remoteFolder)
            {
                SleepWhileSuspended();

                string oldPathname = Path.Combine(directory, remoteFolder.Name);
                string newPathname = Path.Combine(directory, newFilename);
                try
                {

                    Logger.InfoFormat("Renaming: {0} -> {1}", oldPathname, newPathname);

                    IDictionary<string, object> properties = new Dictionary<string, object>();
                    properties[PropertyIds.Name] = newFilename;

                    IFolder updatedFolder = (IFolder)remoteFolder.UpdateProperties(properties);

                    // Update the path in the database...
                    database.MoveFolder(oldPathname, newPathname);

                    Logger.InfoFormat("Renamed folder: {0} -> {1}", oldPathname, newPathname);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException(String.Format("Could not rename folder: {0} -> {1}", oldPathname, newPathname), e);
                    return false;
                }
            }

            /// <summary>
            /// Move a file remotely.
            /// </summary>
            private bool MoveFile(string oldDirectory, string newDirectory, IFolder oldRemoteFolder, IFolder newRemoteFolder, IDocument remoteFile)
            {
                SleepWhileSuspended();

                string oldPathname = Path.Combine(oldDirectory, remoteFile.Name);
                string newPathname = Path.Combine(newDirectory, remoteFile.Name);
                try
                {

                    Logger.InfoFormat("Moving: {0} -> {1}", oldPathname, newPathname);


                    IDocument updatedDocument = (IDocument)remoteFile.Move(oldRemoteFolder, newRemoteFolder);

                    // Update the path in the database...
                    database.MoveFile(oldPathname, newPathname);

                    // Update timestamp in database.
                    database.SetFileServerSideModificationDate(newPathname, ((DateTime)updatedDocument.LastModificationDate).ToUniversalTime());

                    Logger.InfoFormat("Moved file: {0} -> {1}", oldPathname, newPathname);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException(String.Format("Could not move file: {0} -> {1}", oldPathname, newPathname), e);
                    return false;
                }
            }

            /// <summary>
            /// Move a folder remotely.
            /// </summary>
            private bool MoveFolder(string oldDirectory, string newDirectory, IFolder oldRemoteFolder, IFolder newRemoteFolder, IFolder remoteFolder)
            {
                SleepWhileSuspended();

                string oldPathname = Path.Combine(oldDirectory, remoteFolder.Name);
                string newPathname = Path.Combine(newDirectory, remoteFolder.Name);
                try
                {

                    Logger.InfoFormat("Moving: {0} -> {1}", oldPathname, newPathname);


                    IFolder updatedFolder = (IFolder)remoteFolder.Move(oldRemoteFolder, newRemoteFolder);

                    // Update the path in the database...
                    database.MoveFolder(oldPathname, newPathname);

                    Logger.InfoFormat("Moved folder: {0} -> {1}", oldPathname, newPathname);
                    return true;
                }
                catch (Exception e)
                {
                    ProcessRecoverableException(String.Format("Could not move folder: {0} -> {1}", oldPathname, newPathname), e);
                    return false;
                }
            }
            
            /// <summary>
            /// Sleep while suspended.
            /// </summary>
            private void SleepWhileSuspended()
            {
                if (repo.Status == SyncStatus.Suspend)
                {
                    repo.OnSyncSuspend();
                    while (repo.Status == SyncStatus.Suspend)
                    {
                        suspended = true;
                        Logger.Debug(String.Format("Sync of {0} is suspended, next retry in {1}ms", repoinfo.Name, SYNC_SUSPEND_SLEEP_INTERVAL));
                        System.Threading.Thread.Sleep(SYNC_SUSPEND_SLEEP_INTERVAL);
                    }
                    suspended = false;
                    repo.OnSyncResume();
                }
            }
        }
    }
}
