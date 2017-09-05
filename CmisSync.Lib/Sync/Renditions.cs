using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS.Client;
using System.IO;
using DotCMIS.Data;
using CmisSync.Lib.Cmis;

namespace CmisSync.Lib.Sync
{
    class Renditions
    {
        //CmisRepo cmisRepo;

        /// <summary>
        /// Log.
        /// </summary>
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Renditions));

        internal static void Update(ISession session, RepoInfo repoInfo, Database.Database database, DotCMIS.Client.IFolder remoteFolder, string remoteFolderPath, string localFolder)
        {
            // Crawl local files and create or update their renditions.
            CheckLocalFolder(session, repoInfo, database, localFolder);

            // Remove renditions whose document does not exist anymore.

        }

        private static void CheckLocalFolder(ISession session, RepoInfo repoInfo, Database.Database database, string localFolder)
        {
            //cmisRepo.SleepWhileSuspended();

            // Check files in this folder.

            string[] files;
            try
            {
                files = Directory.GetFiles(localFolder);
            }
            catch (Exception e)
            {
                Logger.Warn("Could not get the file list from folder: " + localFolder, e);
                return;
            }

            foreach (string file in files)
            {
                if (file.Contains(CmisRepo.SynchronizedFolder.RENDITION_IDENTIFIER))
                {
                    // File is a rendition, check whether it is still needed or not.

                    string document = file.Substring(0, file.IndexOf(CmisRepo.SynchronizedFolder.RENDITION_IDENTIFIER));
                    if (!files.Contains(document))
                    {
                        string renditionPath = Path.Combine(localFolder, file);
                        Logger.Info("The document " + document + " does not exist anymore, so delete its rendition " + renditionPath + " too.");
                        File.Delete(renditionPath);
                        continue; // The file has been deleted, no need to check it, directly skip to next file.
                    }
                }
                else
                {
                    // File is not a rendition. Check whether it has new renditions.
                    string localFullPath = Path.Combine(localFolder, file);
                    
                    // Get object id from local path.
                    IObjectId objectId = session.CreateObjectId(database.GetDocumentId(localFullPath));
                    ICmisObject cmisObject = session.GetObject(objectId);
                    IDocument remoteDocument = (IDocument)cmisObject;
                    SyncItem syncItem = SyncItemFactory.CreateFromLocalPath(localFullPath, false /* not a folder */, repoInfo, database);
                    Download(session, repoInfo, database, remoteDocument, syncItem); // Download the renditions.
                }
            }

            // Recurse into sub-folders.
            string[] folders;
            try
            {
                folders = Directory.GetDirectories(localFolder);
            }
            catch (Exception e)
            {
                Logger.Warn("Could not get the file list from folder: " + localFolder, e);
                return;
            }
            foreach(string folder in folders)
            {
                CheckLocalFolder(session, repoInfo, database, Path.Combine(localFolder, folder));
            }
        }



        internal static bool Download(ISession session, RepoInfo repoInfo, Database.Database database, IDocument remoteDocument, SyncItem syncItem)
        {
            IList<IRenditionData> renditions = session.GetRenditions(repoInfo.RepoID, remoteDocument.Id, "*", 10, 0, new DotCMIS.Data.Extensions.ExtensionsData());
            foreach (IRenditionData rendition in renditions)
            {
                Logger.Debug(rendition + " " + rendition.MimeType);
                string fileExtension = MimeType.GetExtension(rendition.MimeType);

                string title = rendition.Title;
                var sanitizedTitle = new string(title.Select(
                    character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character).ToArray());

                string path = syncItem.LocalPath + CmisRepo.SynchronizedFolder.RENDITION_IDENTIFIER + sanitizedTitle + "." + fileExtension;

                byte[] filehash = CmisSync.Lib.Sync.CmisRepo.SynchronizedFolder.DownloadStream(remoteDocument.GetContentStream(rendition.StreamId), path);
                if (null == filehash)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
