#pragma warning disable 0414, 0219
using System;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;

using CmisSync.Lib.Config;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Sync.SyncMachine.Crawler;
using CmisSync.Lib.Sync.SyncMachine.Exceptions;
using CmisSync.Lib.Utilities.FileUtilities;

using log4net;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync.SyncMachine.Crawler
{

    // TODO:
    // Change Log should consider 
    // change event does not happend in current remote path
    // eg : alfresco has only one changelog list.
    public class ChangeLogProcessor
    {
        private static readonly ILog Logger = LogManager.GetLogger (typeof (ChangeLogProcessor));

        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null;

        private Dictionary<string, List<IChangeEvent>> changeBuffer = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private ISession session;

        public ChangeLogProcessor (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session, BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;
            this.fullSyncTriplets = fullSyncTriplets;
        }

        public void Start ()
        {

            Console.WriteLine (" Start Changelog process :");

            //changeBuffer =  new Dictionary<string, List<ChangeType?>> ();
            changeBuffer =  new Dictionary<string, List<IChangeEvent>> ();

            Config.CmisSyncConfig.Feature features = null;
            if (ConfigManager.CurrentConfig.GetFolder (cmisSyncFolder.Name) != null)
                features = ConfigManager.CurrentConfig.GetFolder (cmisSyncFolder.Name).SupportedFeatures;

            int maxNumItems = (features != null && features.MaxNumberOfContentChanges != null) ?  // TODO if there are more items, either loop or force CrawlSync
                (int)features.MaxNumberOfContentChanges : 50;


            string lastTokenOnClient = cmisSyncFolder.Database.GetChangeLogToken ();
            string lastTokenOnServer = CmisUtils.GetChangeLogToken (session);

            if (lastTokenOnClient == lastTokenOnServer) {
                Console.WriteLine ("  Synchronized ");
                return;
            }
            if (lastTokenOnClient == null) {
                Console.WriteLine ("  Should do full sync! Local token is null");
                return;
            }

            // ChangeLog tokens are different, so checking changes is needed.
            var currentChangeToken = lastTokenOnClient;
            IChangeEvents changes;
            do {
                Console.WriteLine (" Get changes for current token: {0}", currentChangeToken);

                // Check which documents/folders have changed.
                changes = session.GetContentChanges (currentChangeToken, cmisSyncFolder.CmisProfile.CmisProperties.IsPropertyChangesSupported, maxNumItems);

                /*
                 * Due to latest report in the master branch: single rename will not duplicate.
                 */
                var changeEvents = changes.ChangeEventList./*Where (p => p != changes.ChangeEventList.FirstOrDefault ()).*/ToList ();

                /*
                 *  To avoid sequential update on a single object.
                 *  TODO: Actually it is not necesary in current version because the changelog processor will exist
                 *  when UPDATE is detected and call the full crawler
                 */
                foreach (IChangeEvent changeEvent in changeEvents) {
                    if (changeBuffer.ContainsKey (changeEvent.ObjectId)) {
                        //changeBuffer [changeEvent.ObjectId].Add (changeEvent.ChangeType);
                        try {
                            long deltaTime = ((DateTime)changeEvent.ChangeTime).ToFileTime () - ((DateTime)changeBuffer [changeEvent.ObjectId].Last ().ChangeTime).ToFileTime ();
                            // FileTime's unit is 100nano second.
                            // 5000000 = 0.5s = 500ms 
                            // If an Update is following a Create in 500ms, ignore it. Create the object;
                            if (deltaTime > 5000000) {
                                changeBuffer [changeEvent.ObjectId].Add (changeEvent);
                            }
                        } catch (Exception e) {
                            //ChangeTime is null
                            changeBuffer [changeEvent.ObjectId].Add (changeEvent);
                        }
                    } else {
                        //changeBuffer [changeEvent.ObjectId] = new List<ChangeType?> { changeEvent.ChangeType };
                        changeBuffer [changeEvent.ObjectId] = new List<IChangeEvent> { changeEvent };
                    }
                }

                currentChangeToken = changes.LatestChangeLogToken;

                if (changes.HasMoreItems == true && (currentChangeToken == null || currentChangeToken.Length == 0)) {
                    // then the repository is too old to support changelog
                    // do normal full sync
                    break;
                }

                //database.SetChangeLogToken (currentChangeToken);
            }
            // Repeat if there were two many changes to fit in a single response.
            while (changes.HasMoreItems ?? false);

            // processs change logs
            foreach (string objId in changeBuffer.Keys) {
                
                //ChangeType? action = changeBuffer [objId].Last ();
                ChangeType? action = changeBuffer [objId].Last ().ChangeType;

                /*for some old version of alfresco, ObjectId of changeEvent would be /RemotePath/ + Id, remove it*/
                string remoteId = objId.Split (CmisUtils.CMIS_FILE_SEPARATOR).Last ();
                try {
                    Console.WriteLine ("  Getting remote object, last type: {0}, id = {1}", action, objId);

                    ICmisObject obj = session.GetObject ( remoteId, false);

                    /*
                     * for some cmis eg: old alfresco, get changelog token for one repository will
                     * return all changelog tokens. Check if they are worth syncing here.
                     *   - SyncFileUtil: their path starts with CmisSyncFolder.RemotePath
                     *   - CmisFileUtil: their name contain no slashes
                     */
                    if (!SyncFileUtil.IsRemoteObjectInCmisSyncPath (obj, cmisSyncFolder) ||
                        !CmisFileUtil.RemoteObjectWorthSyncing (obj)) continue;

                    // if this line is called, there must be a remote object, therefore the changetype must be created, updated (or security)
                    // thus it should be buffered for parallel process
                    if (action == ChangeType.Updated) {
                        // do full sync
                        throw new ChangeLogProcessorBrokenException (String.Format(" UPDATE detected, id = {0}, name = {1}", obj.Id, obj.Name));
                    }

                    SyncTriplet.SyncTriplet triplet = null;
                    if (obj is IFolder) {

                        triplet = SyncTripletFactory.CreateFromRemoteFolder ((IFolder)obj, cmisSyncFolder);
                        Console.WriteLine ("  -- {0} is Folder, id = {1}, action = {2}", ((IFolder)obj).Path, ((IFolder)obj).Id, action);

                    } else if (obj is IDocument) {
                        triplet = SyncTripletFactory.CreateFromRemoteDocument ((IDocument)obj, cmisSyncFolder);
                        Console.WriteLine ("  -- {0} is Document, id = {1}", ((IDocument)obj).Name, ((IDocument)obj).Id);
                    }

                    if (!fullSyncTriplets.TryAdd (triplet)) {
                        Console.WriteLine ("Add folder triplet to full synctriplet queue failed: " + obj.Name);
                        Logger.Error ("Add folder triplet to full synctriplet queue failed: " + obj.Name);
                    }

                } catch (CmisObjectNotFoundException ex) { /* should be CmisObjectNotFoundExcepiton, not Exception, otherwise previous ChangeLogProcessorBroken will be caught */

                    // If it is deletion, execute sync
                    // due to all non-deletion not-found remote object will
                    // not trigger sync logic, it is safe to directly apply
                    // deletion syncs
                    if (action == ChangeType.Deleted) {
                        var dbpath = cmisSyncFolder.Database.GetPathById (remoteId);
                        string localpath = dbpath == null ? null : dbpath [0];

                        if (localpath != null) {
                            string localFullPath = Path.Combine (cmisSyncFolder.LocalPath, localpath);
                            Console.WriteLine ("  --  {1} event: {0}", action, localFullPath);
                            if (dbpath[2].Equals("Folder")) {
                                Console.WriteLine ("  -- Delete folder work: {0}", localFullPath);

                                /*
                                 * If changelog will give out all change event in the removed folder,
                                 * it is not necessary to traverse the local dictionary.
                                 * It is also not necessary to check duplication in delayedFolderDeletion 
                                 * concurrent queue in TripletProcessor.
                                if (!FolderDeleteEventHandler (localFullPath, cmisSyncFolder)) {
                                    throw new ChangeLogProcessorBrokenException ("Folder Deletion Failed.");
                                }*/

                                SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromLocalFolder (localFullPath, cmisSyncFolder);
                                if (!fullSyncTriplets.TryAdd (triplet)) {
                                    Console.WriteLine ("Add folder deletion triplet to full synctriplet queue failed! {0}", localFullPath);
                                }
                            } else {
                                Console.WriteLine ("  -- Delete file work: {0}", localFullPath);

                                SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromLocalDocument (localFullPath, cmisSyncFolder);
                                if (!fullSyncTriplets.TryAdd(triplet)) {
                                    Console.WriteLine ("Add file deletion triplet to full synctriplet queue failed! {0}", localFullPath);
                                }
                            }

                        } else {
                            Console.WriteLine ("  -- {0} not found in DB, ignore", objId);
                        }
                    } else {
                        // ignore not-found , 
                    }
                }
            }
        }

        /*
        private bool FolderDeleteEventHandler(string localFullPath, CmisSyncFolder.CmisSyncFolder syncFolder) {

            LocalCrawlWorker folderDeletionWorker = new LocalCrawlWorker (syncFolder, fullSyncTriplets);

            try {
                folderDeletionWorker.StartFrom (localFullPath);
            } catch (Exception ex) {
                Console.WriteLine ("Folder deletion failed: {0}: {1}", localFullPath, ex.Message);
                return false;
            }

            return true; 
        }
        */
    }
}
#pragma warning restore 0414, 0219