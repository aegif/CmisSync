using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using DotCMIS.Client;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Exceptions;
using DotCMIS.Enums;
using System.ComponentModel;
using System.Collections;
using DotCMIS.Data.Impl;

using System.Net;

namespace SparkleLib.Cmis
{
    public partial class SparkleRepoCmis : SparkleRepoBase
    {
        /**
         * Synchronization with a particular CMIS folder.
         */
        public partial class CmisDirectory
        {
            /**
             * Synchronize by checking all folders/files one-by-one.
             * This strategy is used if the CMIS server does not support the ChangeLog feature.
             * 
             * for all remote folders:
             *     if exists locally:
             *       recurse
             *     else
             *       if in database:
             *         delete recursively from server // if BIDIRECTIONAL
             *       else
             *         download recursively
             * for all remote files:
             *     if exists locally:
             *       if remote is more recent than local:
             *         download
             *       else
             *         upload                         // if BIDIRECTIONAL
             *     else:
             *       if in database:
             *         delete from server             // if BIDIRECTIONAL
             *       else
             *         download
             * for all local files:
             *   if not present remotely:
             *     if in database:
             *       delete
             *     else:
             *       upload                           // if BIDIRECTIONAL
             *   else:
             *     if has changed locally:
             *       upload                           // if BIDIRECTIONAL
             * for all local folders:
             *   if not present remotely:
             *     if in database:
             *       delete recursively from local
             *     else:
             *       upload recursively               // if BIDIRECTIONAL
             */
            private void CrawlSync(IFolder remoteFolder, string localFolder)
            {
                // Lists of files/folders, to delete those that have been removed on the server.
                IList remoteFiles = new ArrayList();
                IList remoteSubfolders = new ArrayList();

                // Crawl remote children.
                // SparkleLogger.LogInfo("Sync", String.Format("Crawl remote folder {0}", this.remoteFolderPath));
                crawlRemote(remoteFolder, localFolder, remoteFiles, remoteSubfolders);

                // Crawl local files.
                // SparkleLogger.LogInfo("Sync", String.Format("Crawl local files in the local folder {0}", localFolder));
                crawlLocalFiles(localFolder, remoteFolder, remoteFiles);

                // Crawl local folders.
                // SparkleLogger.LogInfo("Sync", String.Format("Crawl local folder {0}", localFolder));
                crawlLocalFolders(localFolder, remoteFolder, remoteSubfolders);
            }

            /**
             * Crawl remote content, syncing down if needed.
             * Meanwhile, cache remoteFiles and remoteFolders, they are output parameters that are used in crawlLocalFiles/crawlLocalFolders
             */
            private void crawlRemote(IFolder remoteFolder, string localFolder, IList remoteFiles, IList remoteFolders)
            {
                foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
                {
                    #region Cmis Folder
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        // It is a CMIS folder.
                        IFolder remoteSubFolder = (IFolder)cmisObject;
                        if (CheckRules(remoteSubFolder.Name, RulesType.Folder))
                        {
                            remoteFolders.Add(remoteSubFolder.Name);
                            string localSubFolder = localFolder + Path.DirectorySeparatorChar + remoteSubFolder.Name;

                            // Check whether local folder exists.
                            if (Directory.Exists(localSubFolder))
                            {
                                // Recurse into folder.
                                CrawlSync(remoteSubFolder, localSubFolder);
                            }
                            else
                            {
                                // If there was previously a file with this name, delete it.
                                // TODO warn if local changes in the file.
                                if (File.Exists(localSubFolder))
                                {
                                    File.Delete(localSubFolder);
                                }

                                if (database.ContainsFolder(localSubFolder))
                                {
                                    // If there was previously a folder with this name, it means that
                                    // the user has deleted it voluntarily, so delete it from server too.

                                    // Delete the folder from the remote server.
                                    remoteSubFolder.DeleteTree(true, null, true);

                                    // Delete the folder from database.
                                    database.RemoveFolder(localSubFolder);
                                }
                                else
                                {
                                    // The folder has been recently created on server, so download it.

                                    // Create local folder.
                                    Directory.CreateDirectory(localSubFolder);

                                    // Create database entry for this folder.
                                    database.AddFolder(localSubFolder, remoteFolder.LastModificationDate);

                                    // Recursive copy of the whole folder.
                                    RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                                }
                            }
                        }
                    }
                    #endregion

                    #region Cmis Document
                    else
                    {
                        // It is a CMIS document.
                        IDocument remoteDocument = (IDocument)cmisObject;

                        if (CheckRules(remoteDocument.Name, RulesType.File))
                        {
                            // We use the filename of the document's content stream.
                            // This can be different from the name of the document.
                            // For instance in FileNet it is not usual to have a document where
                            // document.Name is "foo" and document.ContentStreamFileName is "foo.jpg".
                            string remoteDocumentFileName = remoteDocument.ContentStreamFileName;

                            // Check if file extension is allowed

                            remoteFiles.Add(remoteDocumentFileName);
                            // If this file does not have a filename, ignore it.
                            // It sometimes happen on IBM P8 CMIS server, not sure why.
                            if (remoteDocumentFileName == null)
                            {
                                SparkleLogger.LogInfo("Sync", "Skipping download of '" + remoteDocument.Name + "' with null content stream in " + localFolder);
                                continue;
                            }

                            string filePath = localFolder + Path.DirectorySeparatorChar + remoteDocumentFileName;

                            if (File.Exists(filePath))
                            {
                                // Check modification date stored in database and download if remote modification date if different.
                                DateTime? serverSideModificationDate = remoteDocument.LastModificationDate;
                                DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(filePath);

                                if (lastDatabaseUpdate == null)
                                {
                                    SparkleLogger.LogInfo("Sync", "Downloading file absent from database: " + remoteDocumentFileName);
                                    DownloadFile(remoteDocument, localFolder);
                                }
                                else
                                {
                                    // If the file has been modified since last time we downloaded it, then download again.
                                    if (serverSideModificationDate > lastDatabaseUpdate)
                                    {
                                        if (database.LocalFileHasChanged(filePath))
                                        {
                                            SparkleLogger.LogInfo("Sync", "Conflict with file: " + remoteDocumentFileName + ", backing up locally modified version and downloading server version");
                                            // Rename locally modified file.
                                            String ext = Path.GetExtension(filePath);
                                            String filename = Path.GetFileNameWithoutExtension(filePath);
                                            String path = Path.GetDirectoryName(filePath);

                                            String NewFileName = SuffixIfExists(filename + "_" + repoinfo.User + "-version");
                                            String newFilePath = Path.Combine(path,NewFileName);
                                            File.Move(filePath, newFilePath);

                                            // Download server version
                                            DownloadFile(remoteDocument, localFolder);

                                            // TODO move to OS-dependant layer
                                            //System.Windows.Forms.MessageBox.Show("Someone modified a file at the same time as you: " + filePath
                                            //    + "\n\nYour version has been saved with a '_your-version' suffix, please merge your important changes from it and then delete it.");
                                            // TODO show CMIS property lastModifiedBy
                                        }
                                        else
                                        {
                                            SparkleLogger.LogInfo("Sync", "Downloading modified file: " + remoteDocumentFileName);
                                            DownloadFile(remoteDocument, localFolder);
                                        }
                                    }

                                    // Change modification date in database
                                    database.SetFileServerSideModificationDate(filePath, serverSideModificationDate);
                                }
                            }
                            else
                            {
                                if (database.ContainsFile(filePath))
                                {
                                    // File has been recently removed locally, so remove it from server too.
                                    remoteDocument.DeleteAllVersions();

                                    // Remove it from database.
                                    database.RemoveFile(filePath);
                                }
                                else
                                {
                                    // New remote file, download it.
                                    SparkleLogger.LogInfo("Sync", "Downloading new file: " + remoteDocumentFileName);
                                    DownloadFile(remoteDocument, localFolder);
                                }
                            }
                        }
                    }
                    #endregion
                }
            }

            /**
             * Crawl local files in a given directory (not recursive).
             */
            private void crawlLocalFiles(string localFolder, IFolder remoteFolder, IList remoteFiles)
            {
                foreach (string filePath in Directory.GetFiles(localFolder))
                {
                    string fileName = Path.GetFileName(filePath);

                    if (CheckRules(fileName, RulesType.File))
                    {
                        if (!remoteFiles.Contains(fileName))
                        {
                            // This local file is not on the CMIS server now, so
                            // check whether it used to exist on server or not.
                            if (database.ContainsFile(filePath))
                            {
                                // If file has changed locally, move to 'your_version' and warn about conflict
                                // TODO

                                // File has been deleted on server, so delete it locally.
                                SparkleLogger.LogInfo("Sync", "Removing remotely deleted file: " + filePath);
                                File.Delete(filePath);

                                // Delete file from database.
                                database.RemoveFile(filePath);
                            }
                            else
                            {
                                if (BIDIRECTIONAL)
                                {
                                    // New file, sync up.
                                    SparkleLogger.LogInfo("Sync", "Uploading file absent on repository: " + filePath);
                                    UploadFile(filePath, remoteFolder);
                                }
                            }
                        }
                        else
                        {
                            // The file exists both on server and locally.
                            if (database.LocalFileHasChanged(filePath))
                            {
                                if (BIDIRECTIONAL)
                                {
                                    // Upload new version of file content.
                                    SparkleLogger.LogInfo("Sync", "Uploading file update on repository: " + filePath);
                                    UpdateFile(filePath, remoteFolder);
                                }
                            }
                        }
                    }
                }
            }

            /**
             * Crawl local folders in a given directory (not recursive).
             */
            private void crawlLocalFolders(string localFolder, IFolder remoteFolder, IList remoteFolders)
            {
                foreach (string localSubFolder in Directory.GetDirectories(localFolder))
                {
                    if (CheckRules(localSubFolder, RulesType.Folder))
                    {
                        string folderName = Path.GetFileName(localSubFolder);
                        if (!remoteFolders.Contains(folderName))
                        {
                            // This local folder is not on the CMIS server now, so
                            // check whether it used to exist on server or not.
                            if (database.ContainsFolder(localSubFolder))
                            {
                                RemoveFolderLocally(localSubFolder);
                            }
                            else
                            {
                                if (BIDIRECTIONAL)
                                {
                                    // New local folder, upload recursively.
                                    UploadFolderRecursively(remoteFolder, localSubFolder);
                                }
                            }
                        }
                    }
                }
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
                if (syncing)
                {
                    SparkleLogger.LogInfo("Sync", String.Format("[{0}] - sync is already running in background.", repoinfo.TargetDirectory));
                    return;
                }
                syncing = true;

                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += new DoWorkEventHandler(
                    delegate(Object o, DoWorkEventArgs args)
                    {
                        SparkleLogger.LogInfo("Sync", String.Format("[{0}] - Launching sync in background, so that the UI stays available.", repoinfo.TargetDirectory));
#if !DEBUG
                        try
                        {
#endif
                            Sync();
#if !DEBUG
                        }
                        catch (CmisBaseException e)
                        {
                            SparkleLogger.LogInfo("Sync", "CMIS exception while syncing:" + e.Message);
                            SparkleLogger.LogInfo("Sync", e.StackTrace);
                            SparkleLogger.LogInfo("Sync", e.ErrorContent);
                        }
#endif
                    }
                );
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                    delegate(object o, RunWorkerCompletedEventArgs args)
                    {
                        syncing = false;
                    }
                );
                bw.RunWorkerAsync();
            }
        }
    }

}