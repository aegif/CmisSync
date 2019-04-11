#pragma warning disable 0414, 0219
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;

using CmisSync.Lib.Config;
using CmisSync.Lib.Cmis;

using log4net;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Enums;

namespace CmisSync.Lib.Sync.SyncMachine.Crawler
{

    [Serializable]
    public class ChangeLogProcessorBreakException : Exception
    {
        public ChangeLogProcessorBreakException () { }
        public ChangeLogProcessorBreakException (string msg) : base (msg) { }
        public ChangeLogProcessorBreakException (string msg, Exception innerException) : base (msg, innerException) {}
    }

    // TODO:
    // Change Log should consider 
    // change event does not happend in current remote path
    // eg : alfresco has only one changelog list.
    public class ChangeLogProcessor
    {
        private static readonly ILog Logger = LogManager.GetLogger (typeof (ChangeLogProcessor));

        // private BlockingCollection<SyncTriplet.SyncTriplet> semiSyncTriplets = null;

        private BlockingCollection<SyncTriplet.SyncTriplet> fullSyncTriplets = null;

        private Dictionary<string, List<ChangeType?>> changeBuffer = null;

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

            changeBuffer =  new Dictionary<string, List<ChangeType?>> ();

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
                 * Changelogtoken's first item is the change caused by lastest
                 * recorded change-log-token in our database. Since getContentChanges
                 * will get changes from CurrentChangeToken, it must be duplicated.
                 * Therefore one should remove it.
                 * 
                 * Due to latest report in master branch: single rename will not duplicate.
                 */
                var changeEvents = changes.ChangeEventList./*Where (p => p != changes.ChangeEventList.FirstOrDefault ()).*/ToList ();

                /*
                 *  To avoid sequential update on a single object.
                 *  TODO: Actually it is not necesary in current version because the changelog processor will exist
                 *  when UPDATE is detected and call the full crawler
                 */
                foreach (IChangeEvent changeEvent in changeEvents) {
                    if (changeBuffer.ContainsKey (changeEvent.ObjectId))
                        changeBuffer [changeEvent.ObjectId].Add (changeEvent.ChangeType);
                    else
                        changeBuffer [changeEvent.ObjectId] = new List<ChangeType?> { changeEvent.ChangeType };
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
                ChangeType? action = changeBuffer [objId].Last ();
                string remoteId = objId.Split (CmisUtils.CMIS_FILE_SEPARATOR).Last ();
                try {
                    Console.WriteLine ("  Getting remote object, last type: {0}, id = {1}", action, objId);

                    ICmisObject obj = session.GetObject ( remoteId, false);

                    // if this line is called, there must be a remote object, therefore the changetype must be created, updated (or security)
                    // thus it should be buffered for parallel process
                    if (action == ChangeType.Updated) {
                        // do full sync
                    }

                    if (obj is IFolder) {
                        IFolder remoteFolder = (IFolder)obj;
                        Console.WriteLine ("  -- {0} is Folder, id = {1}", ((IFolder)obj).Path, ((IFolder)obj).Id);
                    } else if (obj is IDocument) {
                        Console.WriteLine ("  -- {0} is Document, id = {1}", ((IDocument)obj).Name, ((IDocument)obj).Id);
                    }

                } catch (Exception e) {

                    // If it is deletion, execute sync
                    // due to all non-deletion not-found remote object will
                    // not trigger sync logic, it is safe to directly apply
                    // deletion syncs
                    if (action == ChangeType.Deleted) {
                        var dbpath = cmisSyncFolder.Database.GetPathById (remoteId);
                        string localpath = dbpath == null ? null : dbpath [0];

                        if (localpath != null) {
                            Console.WriteLine ("  --  {1} event: {0}", action, localpath);
                        } else {
                            Console.WriteLine ("  -- {0} not found in DB, ignore", objId);
                        }
                    } else {
                        // ignore not-found , 
                    }
                }
            }
        }
    }
}
#pragma warning restore 0414, 0219
