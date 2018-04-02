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
using System.Threading;
using CmisSync.Lib.Database;
using CmisSync.Lib.ActivityListener;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync.SyncRepo;
using CmisSync.Lib.Utilities.PathConverter;
using CmisSync.Lib.UserNotificationListener;
using CmisSync.Lib.Sync.SynchronizeItem;



namespace CmisSync.Lib.Sync.CmisRepoFolder
{
    /// <summary>
    /// Synchronization with a particular CMIS folder.
    /// </summary>
    public partial class SynchronizedFolder : IDisposable
    {
        /// <summary>
        /// Download all content from a CMIS folder.
        /// </summary>
        /// <param name="remoteFolder">The new folder to download. Example: /sites/project/newfolder</param>
        /// <param name="remotePath">The new folder to download. Example: /sites/project/newfolder</param>
        /// <param name="localFolder">The new folder that will be filled by this operation. Warning: It must exist already! Example: C:\CmisSync\project\newfolder</param> TODO: Create the local folder in this method.
        /// <returns>success or not</returns>
        private bool RecursiveFolderCopy (IFolder remoteFolder, string remotePath, string localFolder)
        {
            SleepWhileSuspended ();

            IItemEnumerable<ICmisObject> children;
            try {
                children = remoteFolder.GetChildren ();
            } catch (CmisBaseException e) {
                ProcessRecoverableException ("Could not get children objects: " + remoteFolder.Path, e);
                return false;
            }

            // Remember seen names, and depending on the profile ignore the ones that have appeared already.
            // For instance, if the CMIS server has a file called Hello and a folder called HELLO, then on Windows the paths would conflict.
            // Store lowercase file and folder names inside.
            HashSet<string> names = new HashSet<string> ();

            // Download all children.
            bool success = true;
            foreach (ICmisObject cmisObject in children) {
                string remoteSubPath = remotePath + CmisUtils.CMIS_FILE_SEPARATOR + cmisObject.Name;
                if (cmisObject is DotCMIS.Client.Impl.Folder) {
                    // Case of a folder: Download recursively.

                    IFolder remoteSubFolder = (IFolder)cmisObject;
                    var localSubFolderItem = database.GetFolderSyncItemFromRemotePath (remoteSubFolder.Path);

                    if (null == localSubFolderItem) {
                        localSubFolderItem = SyncItemFactory.CreateFromRemoteFolder (remoteSubFolder.Path, repoInfo, database);
                    }

                    // Ignore edgy folders.
                    if (repoInfo.CmisProfile.IgnoreIfSameLowercaseNames && names.Contains (remoteSubFolder.Name.ToLowerInvariant ())) {
                        Logger.Warn ("Ignoring " + remoteSubFolder.Name + "because other file or folder has the same name when ignoring lowercase/uppercase");
                        continue;
                    }
                    if (!repoInfo.CmisProfile.RemoteObjectWorthSyncing (remoteSubFolder)) {
                        continue;
                    }

                    if (Utils.WorthSyncing (localFolder, PathRepresentationConverterUtil.RemoteToLocal (remoteSubFolder.Name), repoInfo)) {
                        success &= DownloadDirectory (remoteSubFolder, remoteSubPath, localSubFolderItem.LocalPath);
                        names.Add (remoteSubFolder.Name.ToLowerInvariant ());
                    }
                } else if (cmisObject is DotCMIS.Client.Impl.Document) {
                    // Case of a document: Download it.

                    Document document = (Document)cmisObject;
                    // Ignore edgy documents.

                    if (repoInfo.CmisProfile.IgnoreIfSameLowercaseNames && names.Contains (document.Name.ToLowerInvariant ())) {
                        Logger.Warn ("Ignoring " + document.Name + "because other file or folder has the same name when ignoring lowercase/uppercase");
                        continue;
                    }
                    if (!repoInfo.CmisProfile.RemoteObjectWorthSyncing (document)) {
                        continue;
                    }

                    if (Utils.WorthSyncing (localFolder, repoInfo.CmisProfile.localFilename (document), repoInfo)) {
                        success &= DownloadFile (document, remoteSubPath, localFolder);
                        names.Add (document.Name.ToLowerInvariant ());
                    }
                } else {
                    Logger.Debug ("Unknown object type: " + cmisObject.ObjectType.DisplayName
                        + " for object " + remoteFolder + "/" + cmisObject.Name);
                }
            }
            return success;
        }


        /// <summary>
        /// Download a single folder from the CMIS server, by simple recursive copy.
        /// </summary>
        /// <returns>true if successful</returns>
        private bool DownloadDirectory (IFolder remoteFolder, string remotePath, string localFolder)
        {
            SleepWhileSuspended ();
            try {
                // Create local folder.
                Directory.CreateDirectory (localFolder);

                if (remoteFolder.CreationDate != null) {
                    Directory.SetCreationTime (localFolder, (DateTime)remoteFolder.CreationDate);
                }
                if (remoteFolder.LastModificationDate != null) {
                    Directory.SetLastWriteTime (localFolder, (DateTime)remoteFolder.LastModificationDate);
                }
            } catch (Exception e) {
                ProcessRecoverableException ("Could not create directory: " + localFolder, e);
                return false;
            }

            // Create database entry for this folder
            var syncFolderItem = database.GetFolderSyncItemFromRemotePath (remoteFolder.Path);
            if (null == syncFolderItem) {
                syncFolderItem = SyncItemFactory.CreateFromRemoteFolder (remoteFolder.Path, repoInfo, database);
            }
            database.AddFolder (syncFolderItem, remoteFolder.Id, remoteFolder.LastModificationDate);
            Logger.Info ("Added folder to database: " + localFolder);

            // Recurse into folder.
            bool success = RecursiveFolderCopy (remoteFolder, remotePath, localFolder);

            return success;
        }


        /// <summary>
        /// Set the last modification date of a local file to whatever a remote document's last modfication date is.
        /// </summary>
        /// <returns>true if successful</returns>
        private bool SetLastModifiedDate (IDocument remoteDocument, string filepath, Dictionary<string, string []> metadata)
        {
            try {
                if (remoteDocument.LastModificationDate != null) {
                    File.SetLastWriteTimeUtc (filepath, (DateTime)remoteDocument.LastModificationDate);
                } else {
                    string [] cmisModDate;
                    if (metadata.TryGetValue ("cmis:lastModificationDate", out cmisModDate) && cmisModDate.Length == 3) // TODO explain 3 and 2 in following line
                    {
                        DateTime modDate = DateTime.Parse (cmisModDate [2]);
                        File.SetLastWriteTimeUtc (filepath, modDate);
                    }
                }
            } catch (Exception e) {
                Logger.Debug (String.Format ("Failed to set last modified date for the local file: {0}", filepath), e);
                return false;
            }
            return true;
        }


        private void SetLastModifiedDate (IFolder remoteFolder, string folderpath, Dictionary<string, string []> metadata)
        {
            try {
                if (remoteFolder.LastModificationDate != null) {
                    File.SetLastWriteTimeUtc (folderpath, (DateTime)remoteFolder.LastModificationDate);
                } else {
                    string [] cmisModDate;
                    if (metadata.TryGetValue ("cmis:lastModificationDate", out cmisModDate) && cmisModDate.Length == 3) {
                        DateTime modDate = DateTime.Parse (cmisModDate [2]);
                        File.SetLastWriteTimeUtc (folderpath, modDate);
                    }
                }
            } catch (Exception e) {
                Logger.Debug (String.Format ("Failed to set last modified date for the local folder: {0}", folderpath), e);
            }
        }


        /// <summary>
        /// Download a single file from the CMIS server.
        /// 
        /// Algorithm:
        /// 
        /// Skip if invalid filename
        /// If directory exists with same name, delete it
        /// If temporary file already exists but database has a different modification date than server, delete it
        /// Download data and metadata, return if that fails
        /// If a file with this name already exists locally
        ///   If conflict
        ///     Rename the existing file and put the server file instead
        ///     Notify the user
        ///   If file update
        ///     Replace the file
        /// Else (new file)
        ///   Save
        /// Set creation date and last modification date if available
        /// Make read-only if remote can not be modified
        /// Create CmisSync database entry for this file
        /// 
        /// </summary>
        /// <returns>success or not</returns>
        private bool DownloadFile (IDocument remoteDocument, string remotePath, string localFolder)
        {
            //Logger.Debug("DownloadFile " + remoteDocument + " remotePath:" + remotePath + " localFolder:" + localFolder);
            SleepWhileSuspended ();

            var syncItem = database.GetSyncItemFromRemotePath (remotePath);
            if (null == syncItem) {
                syncItem = SyncItemFactory.CreateFromRemoteDocument (remotePath, remoteDocument, repoInfo, database);
            }

            Logger.Info ("Downloading: " + syncItem.LocalLeafname);

            // Skip if invalid file name. See https://github.com/aegif/CmisSync/issues/196
            if (Utils.IsInvalidFileName (syncItem.LocalLeafname)) {
                Logger.Info ("Skipping download of file with illegal filename: " + syncItem.LocalLeafname);
                return true;
            }

            Boolean success = false;
            try {
                DotCMIS.Data.IContentStream contentStream = null;
                string filePath = syncItem.LocalPath;
                string tmpFilePath = filePath + ".sync";
                if (database.GetOperationRetryCounter (filePath, Database.Database.OperationType.DOWNLOAD) > repoInfo.MaxDownloadRetries) {
                    Logger.Info (String.Format ("Skipping download of file {0} because of too many failed ({1}) downloads", database.GetOperationRetryCounter (filePath, Database.Database.OperationType.DOWNLOAD)));
                    return true;
                }

                // If there was previously a directory with this name, delete it.
                // TODO warn if local changes inside the folder.
                if (Directory.Exists (filePath)) {
                    Utils.DeleteEvenIfReadOnly (filePath);
                }

                if (File.Exists (tmpFilePath)) {
                    DateTime? remoteDate = remoteDocument.LastModificationDate;
                    if (null == remoteDate) {
                        Utils.DeleteEvenIfReadOnly (tmpFilePath);
                    } else {
                        remoteDate = ((DateTime)remoteDate).ToUniversalTime ();
                        DateTime? serverDate = database.GetDownloadServerSideModificationDate (syncItem);
                        if (remoteDate != serverDate) {
                            Utils.DeleteEvenIfReadOnly (tmpFilePath);
                        }
                    }
                }

                // Download file.
                byte [] filehash = { };

                // If zero length, skip downloading the content, just go on with an empty file
                if (remoteDocument.ContentStreamLength == 0) {
                    Logger.Info ("Skipping download of file with content length zero: " + syncItem.RemoteLeafname);
                    using (FileStream s = File.Create (tmpFilePath)) {
                        s.Close ();
                        success = true;
                    }
                } else {
                    try {
                        contentStream = remoteDocument.GetContentStream ();

                        // If this file does not have a content stream, ignore it.
                        // Even 0 bytes files have a contentStream.
                        // null contentStream sometimes happen on IBM P8 CMIS server, not sure why.
                        if (contentStream == null) {
                            Logger.Warn ("Skipping download of file with null content stream: " + syncItem.RemoteLeafname);
                            return true;
                        } else {
                            filehash = DownloadStream (contentStream, tmpFilePath);
                            contentStream.Stream.Close ();
                        }
                        success = true;
                    } catch (CmisBaseException e) {
                        ProcessRecoverableException ("Download failed: " + syncItem.RemoteLeafname, e);
                        if (contentStream != null) contentStream.Stream.Close ();
                        success = false;
                        File.Delete (tmpFilePath);
                    }
                }
                if (!success) {
                    return false;
                }

                Logger.Info (String.Format ("Downloaded remote object({0}): {1}", remoteDocument.Id, syncItem.RemoteLeafname));

                // TODO Control file integrity by using hash compare?

                // Get metadata.
                Dictionary<string, string []> metadata = null;
                try {
                    metadata = FetchMetadata (remoteDocument);
                } catch (CmisBaseException e) {
                    ProcessRecoverableException ("Could not fetch metadata: " + syncItem.RemoteLeafname, e);
                    // Remove temporary local document to avoid it being considered a new document.
                    File.Delete (tmpFilePath);
                    return false;
                }

                // Either it is an update; or a file with the same name has been created at the same time locally, resulting in a conflict.
                if (File.Exists (filePath)) {
                    if (database.LocalFileHasChanged (filePath)) // Conflict. Server-side file and local file both modified.
                    {
                        Logger.Info (String.Format ("Conflict with file: {0}", syncItem.RemoteLeafname));

                        // Rename local file with a conflict suffix.
                        string conflictFilename = Utils.CreateConflictFilename (filePath, repoInfo.User);
                        Logger.Debug (String.Format ("Renaming conflicted local file {0} to {1}", filePath, conflictFilename));
                        File.Move (filePath, conflictFilename);

                        // Remove the ".sync" suffix.
                        Logger.Debug (String.Format ("Renaming temporary local download file {0} to {1}", tmpFilePath, filePath));
                        File.Move (tmpFilePath, filePath);
                        success &= SetLastModifiedDate (remoteDocument, filePath, metadata);

                        // Warn user about conflict.
                        string lastModifiedBy = CmisUtils.GetProperty (remoteDocument, "cmis:lastModifiedBy");
                        string message =
                            String.Format ("User {0} added a file named {1} at the same time as you.", lastModifiedBy, filePath)
                            + "\n\n"
                            + "Your version has been renamed '" + conflictFilename + "', please merge your important changes from it and then delete it.";
                        Logger.Info (message);
                        UserNotificationListenerUtil.NotifyUser (message);
                    } else // Server side file was modified, but local file was not modified. Just need to update the file.
                      {
                        Logger.Debug (String.Format ("Deleting old local file {0}", filePath));
                        Utils.DeleteEvenIfReadOnly (filePath);

                        Logger.Debug (String.Format ("Renaming temporary local download file {0} to {1}", tmpFilePath, filePath));
                        // Remove the ".sync" suffix.
                        File.Move (tmpFilePath, filePath);
                        success &= SetLastModifiedDate (remoteDocument, filePath, metadata);
                    }
                } else // New file
                  {
                    Logger.Debug (String.Format ("Renaming temporary local download file {0} to {1}", tmpFilePath, filePath));
                    // Remove the ".sync" suffix.
                    File.Move (tmpFilePath, filePath);
                    success &= SetLastModifiedDate (remoteDocument, filePath, metadata);
                }

                if (null != remoteDocument.CreationDate) {
                    File.SetCreationTime (filePath, (DateTime)remoteDocument.CreationDate);
                }
                if (null != remoteDocument.LastModificationDate) {
                    File.SetLastWriteTime (filePath, (DateTime)remoteDocument.LastModificationDate);
                }

                // Should the local file be made read-only?
                // Check ther permissions of the current user to the remote document.
                bool readOnly = !remoteDocument.AllowableActions.Actions.Contains (Actions.CanSetContentStream);
                if (readOnly) {
                    File.SetAttributes (filePath, FileAttributes.ReadOnly);
                }

                // Create database entry for this file.
                database.AddFile (syncItem, remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                Logger.Info ("Added file to database: " + filePath);

                if (!success) {
                    return false;
                }
            } catch (Exception e) {
                ProcessRecoverableException ("Could not download file: " + syncItem.LocalPath, e);
                return false;
            }
            return true;
        }


        private bool ResumeUploadFile (string filePath, IDocument remoteDocument)
        {
            Logger.Debug ("Resuming Upload: " + filePath + " to remote document: " + remoteDocument.Name);
            if (repoInfo.ChunkSize <= 0) {
                return UpdateFile (filePath, remoteDocument);
            }

            // disable the chunk upload
            return UpdateFile (filePath, remoteDocument);
        }


        /// <summary>
        /// Download a file, without retrying.
        /// </summary>
        private byte [] DownloadStream (DotCMIS.Data.IContentStream contentStream, string filePath)
        {
            byte [] hash = { };
            using (Stream file = File.Create (filePath))
            using (SHA1 hashAlg = new SHA1Managed ())
            using (CryptoStream hashstream = new CryptoStream (file, hashAlg, CryptoStreamMode.Write)) {
                byte [] buffer = new byte [8 * 1024];
                int len;
                while ((len = contentStream.Stream.Read (buffer, 0, buffer.Length)) > 0) {
                    hashstream.Write (buffer, 0, len);
                }
                hashstream.FlushFinalBlock ();
                hash = hashAlg.Hash;
            }
            contentStream.Stream.Close ();
            return hash;
        }


        /// <summary>
        /// Upload a single file to the CMIS server.
        /// </summary>
        private bool UploadFile (string filePath, IFolder remoteFolder) // TODO make SyncItem
        {
            SleepWhileSuspended ();

            var syncItem = database.GetSyncItemFromLocalPath (filePath);
            if (null == syncItem) {
                syncItem = SyncItemFactory.CreateFromLocalPath (filePath, false, repoInfo, database);
            }

            try {
                IDocument remoteDocument = null;
                byte [] filehash = { };

                // Prepare properties
                string remoteFileName = syncItem.RemoteLeafname;
                Dictionary<string, object> properties = new Dictionary<string, object> ();
                properties.Add (PropertyIds.Name, remoteFileName);
                properties.Add (PropertyIds.ObjectTypeId, "cmis:document");
                properties.Add (PropertyIds.CreationDate, File.GetCreationTime (syncItem.LocalPath));
                properties.Add (PropertyIds.LastModificationDate, File.GetLastWriteTime (syncItem.LocalPath));

                // Prepare content stream
                using (Stream file = File.Open (syncItem.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (SHA1 hashAlg = new SHA1Managed ())
                using (CryptoStream hashstream = new CryptoStream (file, hashAlg, CryptoStreamMode.Read)) {
                    ContentStream contentStream = new ContentStream ();
                    contentStream.FileName = remoteFileName;
                    contentStream.MimeType = MimeType.GetMIMEType (remoteFileName);
                    contentStream.Length = file.Length;
                    contentStream.Stream = hashstream;

                    Logger.InfoFormat ("Uploading: {0} as {1}/{2}", syncItem.LocalPath, remoteFolder.Path, remoteFileName);
                    remoteDocument = remoteFolder.CreateDocument (properties, contentStream, null);
                    Logger.InfoFormat ("Uploaded: {0}", syncItem.LocalPath);
                    filehash = hashAlg.Hash;
                }

                // Get metadata. Some metadata has probably been automatically added by the server.
                Dictionary<string, string []> metadata = FetchMetadata (remoteDocument);

                // Create database entry for this file.
                database.AddFile (syncItem, remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                Logger.Info ("Added file to database: " + syncItem.LocalPath);
                return true;
            } catch (Exception e) {
                ProcessRecoverableException ("Could not upload file: " + syncItem.LocalPath, e);
                return false;
            }
        }

        /// <summary>
        /// Upload folder recursively.
        /// After execution, the hierarchy on server will be: .../remoteBaseFolder/localFolder/...
        /// </summary>
        private bool UploadFolderRecursively (IFolder remoteBaseFolder, string localFolder) // TODO switch order of argument for consistency with methods above and below
        {
            bool success = true;
            SleepWhileSuspended ();

            IFolder folder = null;
            try {
                var syncItem = database.GetFolderSyncItemFromLocalPath (localFolder);
                if (null == syncItem) {
                    syncItem = SyncItemFactory.CreateFromLocalPath (localFolder, true, repoInfo, database);
                }
                // Create remote folder.
                Dictionary<string, object> properties = new Dictionary<string, object> ();
                properties.Add (PropertyIds.Name, syncItem.RemoteLeafname);
                properties.Add (PropertyIds.ObjectTypeId, "cmis:folder");
                properties.Add (PropertyIds.CreationDate, Directory.GetCreationTime (localFolder));
                properties.Add (PropertyIds.LastModificationDate, Directory.GetLastWriteTime (localFolder));
                try {
                    Logger.Debug (String.Format ("Creating remote folder {0} for local folder {1}", syncItem.RemoteLeafname, localFolder));
                    folder = remoteBaseFolder.CreateFolder (properties);
                    Logger.Debug (String.Format ("Created remote folder {0}({1}) for local folder {2}", syncItem.RemoteLeafname, folder.Id, localFolder));
                } catch (CmisNameConstraintViolationException) {
                    foreach (ICmisObject cmisObject in remoteBaseFolder.GetChildren ()) {
                        if (cmisObject.Name == syncItem.RemoteLeafname) {
                            folder = cmisObject as IFolder;
                        }
                    }
                    if (folder == null) {
                        Logger.Warn ("Remote file conflict with local folder " + syncItem.LocalLeafname);
                        // TODO Show error message
                        return false;
                    }
                    success = false;
                } catch (Exception e) {
                    ProcessRecoverableException (String.Format ("Exception when creating remote folder for local folder {0}: {1}", localFolder, e.Message), e);
                    return false;
                }

                // Create database entry for this folder
                // TODO Add metadata
                database.AddFolder (syncItem, folder.Id, folder.LastModificationDate);
                Logger.Info ("Added folder to database: " + localFolder);
            } catch (CmisBaseException e) {
                ProcessRecoverableException ("Could not create remote directory: " + remoteBaseFolder.Path + "/" + Path.GetFileName (localFolder), e);
                return false;
            }

            try {
                // Upload each file in this folder.
                foreach (string file in Directory.GetFiles (localFolder)) {
                    if (Utils.WorthSyncing (localFolder, Path.GetFileName (file), repoInfo)) {
                        success &= UploadFile (file, folder);
                    }
                }

                // Recurse for each subfolder in this folder.
                foreach (string subfolder in Directory.GetDirectories (localFolder)) {
                    if (Utils.WorthSyncing (localFolder, Path.GetFileName (subfolder), repoInfo)) {
                        success &= UploadFolderRecursively (folder, subfolder);
                    }
                }
            } catch (Exception e) {
                ProcessRecoverableException ("Could not upload folder: " + localFolder, e);
                return false;
            }
            return success;
        }


        /// <summary>
        /// Upload new version of file.
        /// </summary>
        private bool UpdateFile (string localFilePath, IDocument remoteFile)
        {
            SleepWhileSuspended ();
            try {
                var syncItem = database.GetSyncItemFromLocalPath (localFilePath);
                if (null == syncItem) {
                    syncItem = SyncItemFactory.CreateFromLocalPath (localFilePath, false, repoInfo, database);
                }

                Logger.Info ("Updating: " + syncItem.LocalPath);
                using (Stream localfile = File.Open (localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    // Ignore files with null or empty content stream.
                    if ((localfile == null) && (localfile.Length == 0)) {
                        Logger.Info ("Skipping update of file with null or empty content stream: " + localFilePath);
                        return true;
                    }

                    // Check is write permission is allow

                    // Check if the file is Check out or not
                    //if (!(bool)remoteFile.IsVersionSeriesCheckedOut)
                    if ((remoteFile.IsVersionSeriesCheckedOut == null) || !(bool)remoteFile.IsVersionSeriesCheckedOut) {

                        // Prepare content stream
                        ContentStream remoteStream = new ContentStream ();
                        remoteStream.FileName = repoInfo.CmisProfile.localFilename (remoteFile);
                        remoteStream.Length = localfile.Length;
                        remoteStream.MimeType = remoteFile.GetContentStream ().MimeType;
                        remoteStream.Stream = localfile;
                        remoteStream.Stream.Flush ();
                        Logger.Debug ("before SetContentStream");

                        // CMIS do not have a Method to upload block by block. So upload file must be full.
                        // We must waiting for support of CMIS 1.1 https://issues.apache.org/jira/browse/CMIS-628
                        // http://docs.oasis-open.org/cmis/CMIS/v1.1/cs01/CMIS-v1.1-cs01.html#x1-29700019
                        // DotCMIS.Client.IObjectId objID = remoteFile.SetContentStream(remoteStream, true, true);
                        remoteFile.SetContentStream (remoteStream, true, true);

                        Logger.Debug ("after SetContentStream");

                        // Get updated file.
                        var allFileVersions = remoteFile.GetAllVersions ();
                        // CMIS 1.1 specification for getAllVersions: "returns the list of all document objects in the speciﬁed version series, sorted by cmis:creationDate descending"
                        // So the latest version is at index 0
                        var updatedFile = allFileVersions [0];

                        // Update timestamp in database.
                        DateTime serverSideModificationDate = updatedFile.RefreshTimestamp;
                        database.SetFileServerSideModificationDate (syncItem, serverSideModificationDate);

                        // Update checksum
                        database.RecalculateChecksum (syncItem);

                        // TODO Update metadata?
                        Logger.Info ("Updated: " + syncItem.LocalPath);
                        return true;
                    } else {
                        string message = String.Format ("File {0} is CheckOut on the server by another user: {1}", syncItem.LocalPath, remoteFile.CheckinComment);

                        // throw new IOException("File is Check Out on the server");
                        Logger.Info (message);
                        UserNotificationListenerUtil.NotifyUser (message);
                        return false;
                    }
                }
            } catch (Exception e) {
                ProcessRecoverableException ("Could not update file: " + localFilePath, e);
                return false;
            }
        }

        /// <summary>
        /// Upload new version of file content.
        /// </summary>
        private bool UpdateFile (string filePath, IFolder remoteFolder)
        {
            SleepWhileSuspended ();
            try {
                // Find the document within the folder.
                var syncItem = database.GetSyncItemFromLocalPath (filePath);
                if (null == syncItem) {
                    syncItem = SyncItemFactory.CreateFromLocalPath (filePath, false, repoInfo, database);
                }
                string fileName = syncItem.RemoteLeafname;
                IDocument document = null;
                bool found = false;
                foreach (ICmisObject obj in remoteFolder.GetChildren ()) {
                    if (null != (document = obj as IDocument)) {
                        if (repoInfo.CmisProfile.remoteFilename (document).Equals (fileName)) {
                            found = true;
                            break;
                        }
                    }
                }

                // If not found, it means the document has been deleted.
                if (!found) {
                    Logger.Info (syncItem.LocalPath + " not found on server, must be uploaded instead of updated");
                    return UploadFile (syncItem.LocalPath, remoteFolder);
                }

                // Update the document itself.
                return UpdateFile (syncItem.LocalPath, document);
            } catch (CmisBaseException e) {
                ProcessRecoverableException ("Could not update file: " + filePath, e);
                return false;
            }
        }


        public bool DeleteRemoteDocument (IDocument remoteDocument, SyncItem syncItem)
        {
            bool success = true;

            string message0 = "CmisSync Warning: You have deleted file " + syncItem.LocalPath +
                             "\nCmisSync will now delete it from the server. If you actually did not delete this file, please report a bug at CmisSync@aegif.jp";
            Logger.Info (message0);
            //Utils.NotifyUser(message0);

            if (remoteDocument.IsVersionSeriesCheckedOut != null
                && (bool)remoteDocument.IsVersionSeriesCheckedOut
                && remoteDocument.VersionSeriesCheckedOutBy != null
                && !remoteDocument.VersionSeriesCheckedOutBy.Equals (repoInfo.User)) {
                string message = String.Format ("Restoring file \"{0}\" because it is checked out on the server by another user: {1}",
                    syncItem.LocalPath, remoteDocument.VersionSeriesCheckedOutBy);
                Logger.Info (message);
                UserNotificationListenerUtil.NotifyUser (message);

                // Restore the deleted file
                activityListener.ActivityStarted ();
                success &= DownloadFile (remoteDocument, syncItem.RemotePath, Path.GetDirectoryName (syncItem.LocalPath));
                activityListener.ActivityStopped ();
            } else {
                // File has been recently removed locally, so remove it from server too.

                activityListener.ActivityStarted ();
                Logger.Info ("Removing locally deleted file on server: " + syncItem.RemotePath);
                /*success &=*/
                remoteDocument.DeleteAllVersions ();
                // Remove it from database.
                database.RemoveFile (syncItem);
                activityListener.ActivityStopped ();
            }
            return success;
        }


        /// <summary>
        /// Delete the folder from the remote server.
        /// </summary>
        public void DeleteRemoteFolder (IFolder folder, SyncItem syncItem, string upperFolderPath)
        {
            try {
                Logger.Debug ("Removing remote folder tree: " + folder.Path);
                IList<string> failedIDs = folder.DeleteTree (true, null, true);
                if (failedIDs != null && failedIDs.Count != 0) {
                    Logger.Error ("Failed to completely delete remote folder " + folder.Path);
                    // TODO Should we retry? Maybe at least once, as a manual recursion instead of a DeleteTree.
                }

                // Delete the folder from database.
                database.RemoveFolder (syncItem);
            } catch (CmisPermissionDeniedException e) {

                // TODO: Add resource
                string message = String.Format ("フォルダ {0} に対して削除やリネームする権限がないため、サーバからこのフォルダを復元します（フォルダに含まれるファイル数が多い場合、復元に時間がかかります）。", syncItem.LocalPath);
                UserNotificationListenerUtil.NotifyUser (message);


                // We don't have the permission to delete this folder. Warn and recreate it.
                /*
                Utils.NotifyUser("You don't have the necessary permissions to delete folder " + folder.Path
                    + "\nIf you feel you should be able to delete it, please contact your server administrator");
                */
                database.RemoveFolder (syncItem);
                DownloadDirectory (folder, syncItem.RemotePath, syncItem.LocalPath);
            }
        }


        /// <summary>
        /// Remove folder from local filesystem and database.
        /// </summary>
        private bool RemoveFolderLocally (string folderPath)
        {
            SleepWhileSuspended ();
            // Folder has been deleted on server, delete it locally too.
            try {
                Logger.Info ("Removing remotely deleted folder: " + folderPath);
                Directory.Delete (folderPath, true);
            } catch (Exception e) {
                ProcessRecoverableException ("Could not delete tree:" + folderPath, e);
                return false;
            }

            // Delete folder from database.
            if (!Directory.Exists (folderPath)) {
                var syncFolderItem = database.GetFolderSyncItemFromLocalPath (folderPath);
                if (null == syncFolderItem) {
                    syncFolderItem = SyncItemFactory.CreateFromLocalPath (folderPath, true, repoInfo, database);
                }
                database.RemoveFolder (syncFolderItem);
            }

            return true;
        }

        /// <summary>
        /// Retrieve the CMIS metadata of a document.
        /// </summary>
        /// <returns>a dictionary in which each key is a type id and each value is a couple indicating the mode ("readonly" or "ReadWrite") and the value itself.</returns>
        private Dictionary<string, string []> FetchMetadata (IDocument document)
        {
            Dictionary<string, string []> metadata = new Dictionary<string, string []> ();

            IObjectType typeDef = session.GetTypeDefinition (document.ObjectType.Id/*"cmis:document" not Name FullName*/); // TODO cache
            IList<IPropertyDefinition> propertyDefs = typeDef.PropertyDefinitions;

            // Get metadata.
            foreach (IProperty property in document.Properties) {
                // Mode
                string mode = "readonly";
                foreach (IPropertyDefinition propertyDef in propertyDefs) {
                    if (propertyDef.Id.Equals ("cmis:name")) {
                        Updatability updatability = propertyDef.Updatability;
                        mode = updatability.ToString ();
                    }
                }

                // Value
                if (property.IsMultiValued) {
                    metadata.Add (property.Id, new string [] { property.DisplayName, mode, property.ValuesAsString });
                } else {
                    metadata.Add (property.Id, new string [] { property.DisplayName, mode, property.ValueAsString });
                }
            }

            return metadata;
        }

        /// <summary>
        /// Rename a file remotely.
        /// </summary>
        private bool RenameFile (string directory, string newFilename, IDocument remoteFile)
        {
            SleepWhileSuspended ();

            string oldPathname = Path.Combine (directory, remoteFile.Name);
            string newPathname = Path.Combine (directory, newFilename);
            try {

                Logger.InfoFormat ("Renaming: {0} -> {1}", oldPathname, newPathname);

                IDictionary<string, object> properties = new Dictionary<string, object> ();
                properties [PropertyIds.Name] = newFilename;

                IDocument updatedDocument = (IDocument)remoteFile.UpdateProperties (properties);

                // Update the path in the database...
                database.MoveFile (SyncItemFactory.CreateFromLocalPath (oldPathname, false, repoInfo, database),
                    SyncItemFactory.CreateFromLocalPath (newPathname, false, repoInfo, database));

                // Update timestamp in database.
                database.SetFileServerSideModificationDate (
                    SyncItemFactory.CreateFromLocalPath (newPathname, false, repoInfo, database),
                    ((DateTime)updatedDocument.LastModificationDate).ToUniversalTime ());

                Logger.InfoFormat ("Renamed file: {0} -> {1}", oldPathname, newPathname);
                return true;
            } catch (Exception e) {
                ProcessRecoverableException (String.Format ("Could not rename file: {0} -> {1}", oldPathname, newPathname), e);
                return false;
            }
        }

        /// <summary>
        /// Rename a folder remotely.
        /// </summary>
        private bool RenameFolder (string directory, string newFilename, IFolder remoteFolder)
        {
            SleepWhileSuspended ();

            string oldPathname = Path.Combine (directory, remoteFolder.Name);
            string newPathname = Path.Combine (directory, newFilename);
            try {

                Logger.InfoFormat ("Renaming: {0} -> {1}", oldPathname, newPathname);

                IDictionary<string, object> properties = new Dictionary<string, object> ();
                properties [PropertyIds.Name] = newFilename;

                IFolder updatedFolder = (IFolder)remoteFolder.UpdateProperties (properties);

                // Update the path in the database...
                database.MoveFolder (SyncItemFactory.CreateFromLocalPath (oldPathname, true, repoInfo, database),
                    SyncItemFactory.CreateFromLocalPath (newPathname, true, repoInfo, database));      // database query

                Logger.InfoFormat ("Renamed folder: {0} -> {1}", oldPathname, newPathname);
                return true;
            } catch (Exception e) {
                ProcessRecoverableException (String.Format ("Could not rename folder: {0} -> {1}", oldPathname, newPathname), e);
                return false;
            }
        }

        /// <summary>
        /// Move a file remotely.
        /// </summary>
        private bool MoveFile (string oldDirectory, string newDirectory, IFolder oldRemoteFolder, IFolder newRemoteFolder, IDocument remoteFile)
        {
            SleepWhileSuspended ();

            string oldPathname = Path.Combine (oldDirectory, remoteFile.Name);
            string newPathname = Path.Combine (newDirectory, remoteFile.Name);
            try {

                Logger.InfoFormat ("Moving: {0} -> {1}", oldPathname, newPathname);


                IDocument updatedDocument = (IDocument)remoteFile.Move (oldRemoteFolder, newRemoteFolder);

                // Update the path in the database...
                database.MoveFile (SyncItemFactory.CreateFromLocalPath (oldPathname, false, repoInfo, database),
                    SyncItemFactory.CreateFromLocalPath (newPathname, false, repoInfo, database));        // database query

                // Update timestamp in database.
                database.SetFileServerSideModificationDate (
                    SyncItemFactory.CreateFromLocalPath (newPathname, false, repoInfo, database),
                    ((DateTime)updatedDocument.LastModificationDate).ToUniversalTime ());    // database query

                Logger.InfoFormat ("Moved file: {0} -> {1}", oldPathname, newPathname);
                return true;
            } catch (Exception e) {
                ProcessRecoverableException (String.Format ("Could not move file: {0} -> {1}", oldPathname, newPathname), e);
                return false;
            }
        }

        /// <summary>
        /// Move a folder remotely.
        /// </summary>
        private bool MoveFolder (string oldDirectory, string newDirectory, IFolder oldRemoteFolder, IFolder newRemoteFolder, IFolder remoteFolder)
        {
            SleepWhileSuspended ();

            string oldPathname = Path.Combine (oldDirectory, remoteFolder.Name);
            string newPathname = Path.Combine (newDirectory, remoteFolder.Name);
            try {

                Logger.InfoFormat ("Moving: {0} -> {1}", oldPathname, newPathname);


                IFolder updatedFolder = (IFolder)remoteFolder.Move (oldRemoteFolder, newRemoteFolder);

                // Update the path in the database...
                database.MoveFolder (SyncItemFactory.CreateFromLocalPath (oldPathname, true, repoInfo, database),
                    SyncItemFactory.CreateFromLocalPath (newPathname, true, repoInfo, database));      // database query

                Logger.InfoFormat ("Moved folder: {0} -> {1}", oldPathname, newPathname);
                return true;
            } catch (Exception e) {
                ProcessRecoverableException (String.Format ("Could not move folder: {0} -> {1}", oldPathname, newPathname), e);
                return false;
            }
        }

    }
}
