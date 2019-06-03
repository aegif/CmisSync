using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;

using log4net;
using System.IO;
using CmisSync.Auth;
using CmisSync.Lib.ActivityListener;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Utilities.FileUtilities;

using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;


namespace CmisSync.Lib.Sync.SyncMachine.ProcessWorker.Internal
{

    /*
     * TODO: process recoverable exceptions
     */
    /// <summary>
    /// Util class that provides synchronizing methods for upload, download, delete and conflict resolving.
    /// All methods return SyncResult enum.
    /// </summary>
    public static class WorkerOperations
    {

        private static readonly ILog Logger = LogManager.GetLogger (typeof (WorkerOperations));

        /// <summary>
        /// Creates the local folder.
        /// Downloading a remote directory results in creating the folder only.
        /// </summary>
        /// <param name="triplet">Triplet.</param>
        /// <param name="session">Session.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncResult CreateLocalFolder(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            string localFolder = OperationUtils.GetLocalFullPath (triplet, cmisSyncFolder);

            try {
                // Must be CmisFileUtil.PathCombine
                // IFolder remoteFolder = (IFolder)session.GetObjectByPath (CmisFileUtil.PathCombine (triplet.RemoteStorage.RootPath, triplet.RemoteStorage.RelativePath), false);
                IFolder remoteFolder = (IFolder)triplet.RemoteStorage.CmisObject;

                Directory.CreateDirectory (localFolder);

                if (remoteFolder.CreationDate != null) {
                    Directory.SetCreationTime (localFolder, (DateTime)remoteFolder.CreationDate);
                }
                if (remoteFolder.LastModificationDate != null) {
                    Directory.SetLastWriteTime (localFolder, (DateTime)remoteFolder.LastModificationDate);
                }

                // Create database entry for this folder
                cmisSyncFolder.Database.AddFolder (
                    triplet.RemoteStorage.RelativePath, OperationUtils.GetLocalRelativePath (localFolder, cmisSyncFolder),
                    remoteFolder.Id, remoteFolder.LastModificationDate);
                Logger.Info ("Added folder to database: " + localFolder);

            } catch (Exception e) {
                Console.WriteLine ("  %% download folder failed, {0} : {1}",  
                                   Utils.PathCombine (triplet.RemoteStorage.RootPath, triplet.RemoteStorage.RelativePath), e.Message);

                return SyncResult.FAILED;
            }

            return SyncResult.SUCCEED;
        }


        /*
         * Be aware, in this method, triplet.LocalStorage might be null 
         */
        public static SyncResult DownloadFile (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            string remoteRelativePath = triplet.RemoteStorage.RelativePath;

            Console.WriteLine("  %% Downloading: " + remoteRelativePath);

            // Skip if invalid file name. See https://github.com/aegif/CmisSync/issues/196
            if (SyncFileUtil.IsInvalidFileName (CmisFileUtil.GetLeafname(remoteRelativePath))) {
                Logger.Info ("Skipping download of file with illegal filename: " + remoteRelativePath);
                return SyncResult.SUCCEED;
            }

            Boolean success = false;
            try {

                // Use absolute path for download
                string filePath = OperationUtils.GetLocalFullPath (triplet, cmisSyncFolder);
                string tmpFilePath = filePath + ".sync";

                // Create if path not found.
                // Directory.CreateDirectory is state safe so just catch excepiton
                Directory.CreateDirectory (Path.GetDirectoryName (tmpFilePath));

                if (cmisSyncFolder.Database.GetOperationRetryCounter (filePath, Database.Database.OperationType.DOWNLOAD) > cmisSyncFolder.MaxDownloadRetries) {
                    Logger.Info (String.Format ("Skipping download of file {0} because of too many failed ({1}) downloads", 
                                                remoteRelativePath, 
                                                cmisSyncFolder.Database.GetOperationRetryCounter (filePath, Database.Database.OperationType.DOWNLOAD)));
                    return SyncResult.SUCCEED;
                }

                // if .sync file exists, remove it.
                if (File.Exists (tmpFilePath)) {
                    Utils.DeleteEvenIfReadOnly (tmpFilePath);
                }

                // Download file.
                Console.WriteLine (" %%% download remote :" + triplet.RemoteStorage.FullPath);
                byte [] filehash = { };
                DotCMIS.Data.IContentStream contentStream = null;
                //IDocument remoteDocument = (IDocument) session.GetObjectByPath (triplet.RemoteStorage.FullPath, false);
                IDocument remoteDocument = (IDocument)triplet.RemoteStorage.CmisObject;

                // If zero length, skip downloading the content, just go on with an empty file
                if (remoteDocument.ContentStreamLength == 0) {
                    Logger.Info ("Skipping download of file with content length zero: " + remoteRelativePath);
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
                            Logger.Warn ("Skipping download of file with null content stream: " + remoteRelativePath);
                            return SyncResult.SUCCEED;
                        } else {
                            filehash = OperationUtils.DownloadStream (contentStream, tmpFilePath);
                            contentStream.Stream.Close ();
                        }
                        success = true;
                    } catch (CmisBaseException e) {
                        Console.WriteLine ("  %% download error, " + e.Message);
                        if (contentStream != null) contentStream.Stream.Close ();
                        success = false;
                        File.Delete (tmpFilePath);
                    }
                }
                if (!success) {
                    return SyncResult.FAILED;
                }

                Logger.Info (String.Format ("Downloaded remote object({0}): {1}", remoteDocument.Id, remoteRelativePath));

                // Get metadata.
                Dictionary<string, string []> metadata = null;
                try {
                    metadata = OperationUtils.FetchMetadata (remoteDocument, session);
                } catch (CmisBaseException e) {
                    // Remove temporary local document to avoid it being considered a new document.
                    Console.WriteLine ("  %% download error, " + e.Message);
                    File.Delete (tmpFilePath);
                    return SyncResult.FAILED;
                }

                Logger.Debug (String.Format ("Renaming temporary local download file {0} to {1}", tmpFilePath, filePath));

                // Remove the ".sync" suffix.
                if (File.Exists(filePath)) {
                    Utils.DeleteEvenIfReadOnly (filePath);    
                }
                File.Move (tmpFilePath, filePath);
                success &= OperationUtils.SetLastModifiedDate (remoteDocument, filePath, metadata);

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
                cmisSyncFolder.Database.AddFile (
                    OperationUtils.GetLocalRelativePath(filePath, cmisSyncFolder), remoteRelativePath, CheckSumUtil.Checksum(filePath),
                    remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                Console.WriteLine ("  %% file {0} been downloaded to\n       {1}", remoteRelativePath, filePath);
                //Logger.Info ("Added file to database: " + filePath);

                if (!success) {
                    return SyncResult.FAILED;
                }
            } catch (Exception e) {
                Console.WriteLine ("  %% download error, " + e.Message);
                return SyncResult.FAILED;
            }
            return SyncResult.SUCCEED;
        }

        public static SyncResult CreateRemoteFolder (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            string remoteFullPath = OperationUtils.GetRemoteFullPath (triplet, cmisSyncFolder);
            string remoteRelativePath = OperationUtils.GetRemoteRelativePath (remoteFullPath, cmisSyncFolder);
            string remoteLeaf = CmisFileUtil.GetLeafname (remoteRelativePath);

            // Create remote folder.
            Dictionary<string, object> properties = new Dictionary<string, object> ();
            properties.Add (PropertyIds.Name, remoteLeaf);
            properties.Add (PropertyIds.ObjectTypeId, "cmis:folder");
            properties.Add (PropertyIds.CreationDate, Directory.GetCreationTime (triplet.LocalStorage.FullPath));
            properties.Add (PropertyIds.LastModificationDate, Directory.GetLastWriteTime (triplet.LocalStorage.FullPath));

            try {

                String remoteParentPath = CmisFileUtil.GetUpperFolderOfCmisPath (remoteFullPath);
                IFolder remoteParentFolder;

                try {
                    remoteParentFolder = (IFolder)session.GetObjectByPath (remoteParentPath, false);
                } catch (Exception e) {
                    Console.WriteLine ("  %% Can not get {1}'s parent folder {0}, abort creating", remoteParentPath, triplet.Name);
                    return SyncResult.FAILED;
                }

                IFolder folder = remoteParentFolder.CreateFolder (properties);
                Logger.Debug (String.Format ("Creating remote folder {0} for local folder {1}", remoteLeaf, triplet.LocalStorage.RelativePath));
                Logger.Debug (String.Format ("Created remote folder {0}({1}) for local folder {2}", remoteLeaf, folder.Id, triplet.LocalStorage.RelativePath));
                cmisSyncFolder.Database.AddFolder (remoteRelativePath, triplet.LocalStorage.RelativePath, folder.Id, folder.LastModificationDate);

            } catch (CmisNameConstraintViolationException) {
                Logger.Warn ("Remote file conflict with local folder " + remoteLeaf);
                return SyncResult.FAILED;
            } catch (Exception e) {
                Console.WriteLine (" %%%% create remote folder " + remoteLeaf + " failed. " + e.Message);
            }

            Console.WriteLine ("  %%%% create remote folder: " + remoteFullPath);
            return SyncResult.SUCCEED;
        }


        /*
         *  Beaware in this method, triplet.RemoteStorage may not exist.
         */
        public static SyncResult UploadFile (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            string remoteFullPath = OperationUtils.GetRemoteFullPath (triplet, cmisSyncFolder);
            string remoteRelativePath = OperationUtils.GetRemoteRelativePath (remoteFullPath, cmisSyncFolder);
            string remoteLeaf = CmisFileUtil.GetLeafname (remoteRelativePath);

            string localFullPath = triplet.LocalStorage.FullPath;

            string remoteFolderFullPath = CmisFileUtil.GetUpperFolderOfCmisPath (remoteFullPath);
            IFolder remoteFolder = null;

            // The remote folder does not exist. Return CONFLICT
            try {
                remoteFolder = (IFolder)session.GetObjectByPath (remoteFolderFullPath, false);
                if (null == remoteFolder) return SyncResult.CONFLICT;
            } catch (Exception e) {
                return SyncResult.CONFLICT;
            }


            try {
                IDocument remoteDocument = null;

                byte [] filehash = { };

                // Prepare properties
                Dictionary<string, object> properties = new Dictionary<string, object> ();
                properties.Add (PropertyIds.Name, remoteLeaf);
                properties.Add (PropertyIds.ObjectTypeId, "cmis:document");
                properties.Add (PropertyIds.CreationDate, File.GetCreationTime (localFullPath));
                properties.Add (PropertyIds.LastModificationDate, File.GetLastWriteTime (localFullPath));

                // Prepare content stream
                using (Stream file = File.Open (localFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (SHA1 hashAlg = new SHA1Managed ())
                using (CryptoStream hashstream = new CryptoStream (file, hashAlg, CryptoStreamMode.Read)) {
                    ContentStream contentStream = new ContentStream ();
                    contentStream.FileName = remoteLeaf;
                    contentStream.MimeType = MimeType.GetMIMEType (remoteLeaf);
                    contentStream.Length = file.Length;
                    contentStream.Stream = hashstream;

                    Logger.InfoFormat ("Uploading: {0} as {1}", triplet.LocalStorage.RelativePath, remoteRelativePath);

                    remoteDocument = remoteFolder.CreateDocument (properties, contentStream, null);
                    Logger.InfoFormat ("Uploaded: {0}", localFullPath);
                    filehash = hashAlg.Hash;
                }

                // Get metadata. Some metadata has probably been automatically added by the server.
                Dictionary<string, string []> metadata = OperationUtils.FetchMetadata (remoteDocument, session);

                // Create database entry for this file.
                cmisSyncFolder.Database.AddFile (
                    triplet.LocalStorage.RelativePath, remoteRelativePath, CheckSumUtil.Checksum(localFullPath),
                    remoteDocument.Id, remoteDocument.LastModificationDate, metadata, filehash);
                return SyncResult.SUCCEED;
            } catch (Exception e) {
                Console.WriteLine ("  %% Upload error, " + e.Message);
                return SyncResult.FAILED;
            }
        }

        public static SyncResult UpdateRemoteFile(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder) 
        {
            if (!triplet.RemoteExist) return SyncResult.FAILED;

            string localFullPath = triplet.LocalStorage.FullPath;

            string remoteFullPath = OperationUtils.GetRemoteFullPath (triplet, cmisSyncFolder);
            string remoteRelativePath = OperationUtils.GetRemoteRelativePath (remoteFullPath, cmisSyncFolder);
            string remoteLeaf = CmisFileUtil.GetLeafname (remoteRelativePath);

            try {

                Logger.Info ("Updating: " + localFullPath);

                using (Stream localfile = File.Open (localFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    // Ignore files with null or empty content stream.
                    if ((localfile == null) && (localfile.Length == 0)) {
                        Logger.Info ("Skipping update of file with null or empty content stream: " + localFullPath);
                        return SyncResult.SUCCEED;
                    }

                    //IDocument remoteFile = (IDocument)session.GetObjectByPath (remoteFullPath, false);
                    IDocument remoteFile = (IDocument)triplet.RemoteStorage.CmisObject;

                    // Check is write permission is allow
                    // Check if the file is Check out or not
                    //if (!(bool)remoteFile.IsVersionSeriesCheckedOut)
                    if ((remoteFile.IsVersionSeriesCheckedOut == null) || !(bool)remoteFile.IsVersionSeriesCheckedOut) {

                        // Prepare content stream
                        ContentStream remoteStream = new ContentStream ();
                        remoteStream.FileName = CmisFileUtil.GetLocalFileName (remoteFile, cmisSyncFolder.CmisProfile);
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

                        cmisSyncFolder.Database.SetFileServerSideModificationDate (triplet, serverSideModificationDate);

                        // Update checksum
                        cmisSyncFolder.Database.SetChecksum (triplet);

                        // TODO Update metadata?
                        Logger.Info ("Updated: " + triplet.LocalStorage.RelativePath);
                        return SyncResult.SUCCEED;
                    } else {
                        string message = String.Format ("File {0} is CheckOut on the server by another user: {1}", triplet.LocalStorage.RelativePath, remoteFile.CheckinComment);

                        // throw new IOException("File is Check Out on the server");
                        Logger.Info (message);
                        return SyncResult.FAILED;
                    }
                }
            } catch (Exception e) {
                return SyncResult.FAILED;
            }
        }

        public static SyncResult DeleteRemoteFile (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            string message0 = "CmisSync Warning: You have deleted file " + triplet.RemoteStorage.RelativePath +
                             "\nCmisSync will now delete it from the server. If you actually did not delete this file, please report a bug at CmisSync@aegif.jp";
            Logger.Info (message0);

            try {

                // IDocument remoteDocument = (IDocument)session.GetObjectByPath (triplet.RemoteStorage.FullPath, false);
                IDocument remoteDocument = (IDocument)triplet.RemoteStorage.CmisObject;

                if (remoteDocument.IsVersionSeriesCheckedOut != null
                    && (bool)remoteDocument.IsVersionSeriesCheckedOut
                    && remoteDocument.VersionSeriesCheckedOutBy != null
                    && !remoteDocument.VersionSeriesCheckedOutBy.Equals (cmisSyncFolder.CmisProfile.User)) {
                    string message = String.Format ("Restoring file \"{0}\" because it is checked out on the server by another user: {1}",
                                                    triplet.LocalStorage.RelativePath, remoteDocument.VersionSeriesCheckedOutBy);
                    Logger.Info (message);

                    // Restore the deleted file
                    // TODO:
                    // in this version, the order of scanning is:
                    // local exist first, then db exist but local delete, then remote exist
                    // therefore restore deleted file ( local not exist, db exist ) will not
                    // effect local scanning process.
                    // 
                    // require study on processing order
                    return DownloadFile (triplet, session, cmisSyncFolder);
                } else {
                    // File has been recently removed locally, so remove it from server too.
                    Logger.Info ("Removing locally deleted file on server: " + triplet.RemoteStorage.FullPath);
                    /*success &=*/
                    remoteDocument.DeleteAllVersions ();
                    // Remove it from database.
                    cmisSyncFolder.Database.RemoveFile (triplet);
                    return SyncResult.SUCCEED;
                }
            } catch (CmisObjectNotFoundException e) {
                // if the object is already removed, delete it from DB
                cmisSyncFolder.Database.RemoveFile (triplet);
                return SyncResult.SUCCEED;
            } catch (Exception e) {
                Console.WriteLine ("  %% delete remote file failed, " + e.Message);
                return SyncResult.FAILED;
            }
        }


        /// <summary>
        /// Delete the folder from the remote server.
        /// </summary>
        public static SyncResult DeleteRemoteFolder (SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            try {
                //IFolder folder = (IFolder)session.GetObjectByPath (triplet.RemoteStorage.FullPath, false);
                IFolder folder = (IFolder)triplet.RemoteStorage.CmisObject;

                // Companion with triplet.Delay property:
                // All files and subfolders should be removed before
                // it. 
                if (folder.GetChildren ().TotalNumItems > 0) {
                    Console.WriteLine ("   %% unable to delete non-empty folder: " + folder.Path + "\n" +
                                       "      check if there is modified file in it");

                    // Delete the folder from database.
                    // RemoveDbRecord (triplet, cmisSyncFolder);
                    // return SyncResult.SUCCEED;
                    return SyncResult.FAILED;
                }

                Logger.Debug ("Removing remote folder tree: " + triplet.RemoteStorage.RelativePath);

                IList<string> failedIDs = folder.DeleteTree (true, null, true);

                if (failedIDs != null && failedIDs.Count != 0) {
                    Logger.Error ("Failed to completely delete remote folder " + folder.Path);
                    // TODO Should we retry? Maybe at least once, as a manual recursion instead of a DeleteTree.
                }

                // Delete the folder from database.
                cmisSyncFolder.Database.RemoveFolder (triplet);
            } catch (CmisPermissionDeniedException e) {

                // TODO
                // We don't have the permission to delete this folder. Warn and recreate it.
                /*
                Utils.NotifyUser("You don't have the necessary permissions to delete folder " + folder.Path
                    + "\nIf you feel you should be able to delete it, please contact your server administrator");
                */
                cmisSyncFolder.Database.RemoveFolder (triplet);
            } catch (CmisObjectNotFoundException e) {
                // The remote object is not found, it has been removed. Delete it from DB
                cmisSyncFolder.Database.RemoveFolder (triplet);
            } catch (Exception e) {
                Console.WriteLine ("  %% delete remote folder failed, " + e.Message);
                return SyncResult.FAILED;
            }

            return SyncResult.SUCCEED;
        }


        public static SyncResult DeleteLocalFile(SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            string localFullPath = triplet.LocalStorage.FullPath;
            try {
                File.Delete (localFullPath); 
            } catch (Exception e) {
                return SyncResult.FAILED;
            }

            if (!File.Exists(localFullPath)) {
                cmisSyncFolder.Database.RemoveFile (triplet);
            }

            return SyncResult.SUCCEED;
        }

        /// <summary>
        /// Remove folder from local filesystem and database.
        /// </summary>
        public static SyncResult DeleteLocalFolder (SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            string localFullPath = triplet.LocalStorage.FullPath;
            // Folder has been deleted on server, delete it locally too.

            try {

                Logger.Info ("Removing remotely deleted folder: " + localFullPath);
                if (Directory.Exists (localFullPath)) {

                    bool isEmpty = true;
                    foreach (var f in Directory.GetFiles (localFullPath)) {
                        if (SyncFileUtil.WorthSyncing (f, cmisSyncFolder)) {
                            Console.WriteLine ("  %% Local folder: {0} is not empty. Containing file: {1}", triplet.Name, f);
                            isEmpty = false;
                            break;
                        }
                    }
                    if (isEmpty) {
                        foreach (var d in Directory.GetDirectories (localFullPath)) {
                            if (SyncFileUtil.WorthSyncing (d, cmisSyncFolder)) {
                                Console.WriteLine ("  %% Local folder: {0} is not empty. Containing folder: {1}", triplet.Name, d);
                                isEmpty = false;
                                break;
                            }
                        }
                    }

                    if (isEmpty) {
                        Directory.Delete (localFullPath, true);
                    } else {
                        Console.WriteLine ("  %% Local folder: {0} is not empty. ", triplet.Name);
                        SolveConflict (triplet, cmisSyncFolder);
                        RemoveDbRecord (triplet, cmisSyncFolder);
                        return SyncResult.CONFLICT;
                    }
                }

                RemoveDbRecord (triplet, cmisSyncFolder);

                return SyncResult.SUCCEED;

            } catch (Exception e) {
                Console.WriteLine ("  %% Delete local folder: {0} failed: {1}", triplet.Name, e.Message);
                return SyncResult.FAILED;
            }
        }

        /// <summary>
        /// Moves the remote file. When this method is called, the structure of the triplet is:
        ///   LS: local new item
        ///   DB: local old item, remote old item
        ///   RS: remote old item
        /// </summary>
        /// <returns>The remote file.</returns>
        /// <param name="triplet">Triplet.</param>
        /// <param name="session">Session.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncResult MoveRemoteFile(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            string oldLocalPath = triplet.DBStorage.DBLocalPath;
            string newLocalPath = triplet.LocalStorage.RelativePath;
            string oldLocalFolder = Path.GetDirectoryName (oldLocalPath);
            string newLocalFolder = Path.GetDirectoryName (newLocalPath);

            bool rename = oldLocalFolder == newLocalFolder;

            if (!triplet.RemoteExist) {
                Console.WriteLine ("  %% Move {0} -> {1} failed: remote item not found. ", oldLocalPath, newLocalPath);
                return SyncResult.FAILED;
            }

            try {
                IDocument updatedDocument;
                if (rename) {
                    // do rename
                    Logger.InfoFormat ("Renaming remote file: {0} -> {1}", oldLocalPath, newLocalPath);

                    IDictionary<string, object> properties = new Dictionary<string, object> ();
                    properties [PropertyIds.Name] = Path.GetFileName (newLocalPath);

                    // rename remote object and refresh triplet's info
                    updatedDocument = (IDocument)triplet.RemoteStorage.CmisObject.UpdateProperties (properties);


                } else {

                    // full path for html GET
                    string oldRemoteFolderPath = CmisFileUtil.PathCombine (
                        cmisSyncFolder.RemotePath,
                        CmisFileUtil.GetUpperFolderOfCmisPath (triplet.DBStorage.DBRemotePath));

                    string newRemoteFolderPath = CmisFileUtil.PathCombine (
                        cmisSyncFolder.RemotePath,
                        cmisSyncFolder.Database.LocalToRemote (newLocalFolder, true));

                    Logger.InfoFormat ("Moving remote file {2} from : {0} -> {1}", oldRemoteFolderPath, newRemoteFolderPath, triplet.Name);

                    IFolder oldRemoteFolder = (IFolder)session.GetObjectByPath (oldRemoteFolderPath, true);
                    IFolder newRemoteFolder = (IFolder)session.GetObjectByPath (newRemoteFolderPath, true);

                    // move remote object and refresh triplet's info
                    updatedDocument = (IDocument)((IDocument)triplet.RemoteStorage.CmisObject).Move (oldRemoteFolder, newRemoteFolder);

                }

                // Set latest info to triplet 
                SyncTripletFactory.AssembleRemoteIntoLocal (updatedDocument, triplet, cmisSyncFolder);

                // Update the path in the database...
                cmisSyncFolder.Database.MoveFile (triplet);

                // Update timestamp in database.
                cmisSyncFolder.Database.SetFileServerSideModificationDate (
                    triplet,
                    ((DateTime)updatedDocument.LastModificationDate).ToUniversalTime ());

                Logger.InfoFormat ("Move remote file: {0} -> {1} succeed.", oldLocalPath, newLocalPath);
            } catch (Exception e) {
                Console.WriteLine ("  %% Move remote file: {0} -> {1} failed.", oldLocalPath, newLocalPath);
                Console.WriteLine (e.Message);
                return SyncResult.FAILED;
            }

            return SyncResult.SUCCEED;
        }

        /// <summary>
        /// Moves the remote folder. When this method is called, the status of triplet is:
        ///   LS: local new item
        ///   DB: local old item, remote old item
        ///   RS: remote old item        
        /// 
        /// Remember that only triplets' name contains last '/' when it is a folder, LS and
        /// DB's relative path do not contains last DirectorySeperatarChar.
        /// </summary>
        /// <returns>The remote folder.</returns>
        /// <param name="triplet">Triplet.</param>
        /// <param name="session">Session.</param>
        /// <param name="cmisSyncFolder">Cmis sync folder.</param>
        public static SyncResult MoveRemoteFolder(SyncTriplet.SyncTriplet triplet, ISession session, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {

            string oldLocalPath = triplet.DBStorage.DBLocalPath;
            string newLocalPath = triplet.LocalStorage.RelativePath;

            string oldLocalUpperFolder = Path.GetDirectoryName (oldLocalPath);
            string newLocalUpperFolder = Path.GetDirectoryName (newLocalPath);

            bool rename = oldLocalUpperFolder == newLocalUpperFolder;

            if (!triplet.RemoteExist) {
                Console.WriteLine ("  %% Move {0} -> {1} failed: remote item not found. ", oldLocalPath, newLocalPath);
                return SyncResult.FAILED;
            }

            try {
                IFolder updatedFolder;
                if (rename) {
                    // do rename
                    Logger.InfoFormat ("Renaming remote folder: {0} -> {1}", oldLocalPath, newLocalPath);

                    IDictionary<string, object> properties = new Dictionary<string, object> ();

                    // Path.GetFileName will return the folder's name given path like 'd1/d2'. Becareful that there is no 
                    // Path.DirectorySeperatarChar at the end of path.
                    properties [PropertyIds.Name] = Path.GetFileName(newLocalPath);

                    // rename remote object and refresh triplet's info
                    updatedFolder = (IFolder)triplet.RemoteStorage.CmisObject.UpdateProperties (properties);


                } else {
                    // full path for html GET
                    string oldRemoteUpperFolderPath = CmisFileUtil.PathCombine (
                        cmisSyncFolder.RemotePath,
                        CmisFileUtil.GetUpperFolderOfCmisPath (triplet.DBStorage.DBRemotePath));
                    string newRemoteUpperFolderPath = CmisFileUtil.PathCombine (
                        cmisSyncFolder.RemotePath,
                        cmisSyncFolder.Database.LocalToRemote (newLocalUpperFolder, true));

                    IFolder oldRemoteUpperFolder = (IFolder)session.GetObjectByPath (oldRemoteUpperFolderPath, true);
                    IFolder newRemoteUpperFolder = (IFolder)session.GetObjectByPath (newRemoteUpperFolderPath, true);

                    // move remote object and refresh triplet's info
                    updatedFolder = (IFolder)((IFolder)triplet.RemoteStorage.CmisObject).Move (oldRemoteUpperFolder, newRemoteUpperFolder);
                }

                // Set latest info to triplet 
                SyncTripletFactory.AssembleRemoteIntoLocal (updatedFolder, triplet, cmisSyncFolder);

                // Update the path in the database...
                cmisSyncFolder.Database.MoveFolder (triplet);

                Logger.InfoFormat ("Move remote folder: {0} -> {1} succeed.", oldLocalPath, newLocalPath);
            } catch (Exception e) {
                Console.WriteLine ("  %% Move remote folder: {0} -> {1} failed.", oldLocalPath, newLocalPath);
                Console.WriteLine (e.Message);
                return SyncResult.FAILED;
            }

            return SyncResult.SUCCEED;
        }

        public static SyncResult SolveConflict(SyncTriplet.SyncTriplet triplet,  CmisSyncFolder.CmisSyncFolder cmisSyncFolder) {

            // case: LS=ne, RS=e, but LS!=DB, RS!=DB, conflict but download only
            if (!triplet.LocalExist) return SyncResult.SUCCEED;

            try {

                Console.WriteLine ("  %% Renaming: {0}", triplet.LocalStorage.RelativePath);
                string filePath = OperationUtils.GetLocalFullPath (triplet, cmisSyncFolder);
                // Rename local file with a conflict suffix.

                if (triplet.IsFolder) {
                    string conflictFoldername = Utils.CreateConflictFoldername (filePath, cmisSyncFolder.CmisProfile.User);
                    Directory.Move (filePath, conflictFoldername);
                } else {
                    string conflictFilename = Utils.CreateConflictFilename (filePath, cmisSyncFolder.CmisProfile.User);
                    File.Move (filePath, conflictFilename);
                }
            } catch (Exception e) {
                Console.WriteLine ("  %% rename file: " + triplet.Name + " failed. " + e.Message);
            }
   
            return SyncResult.SUCCEED;
        }

        public static SyncResult RemoveDbRecord(SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder) {
            if (triplet.IsFolder) {
                cmisSyncFolder.Database.RemoveFolder (triplet);
            } else {
                cmisSyncFolder.Database.RemoveFile (triplet);
            }
            return SyncResult.SUCCEED;
        }
    }
}
