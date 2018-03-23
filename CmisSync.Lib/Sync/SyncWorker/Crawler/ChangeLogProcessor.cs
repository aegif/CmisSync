#pragma warning disable 0414, 0219
using System;
using System.Linq;
using System.Collections.Concurrent;

using CmisSync.Lib.Config;
using CmisSync.Lib.Cmis;

using log4net;
using DotCMIS;
using DotCMIS.Client;

namespace CmisSync.Lib.Sync.SyncWorker.Crawler
{
    public class ChangeLogProcessor
    {
        private static readonly ILog Logger = LogManager.GetLogger (typeof (ChangeLogProcessor));

        private BlockingCollection<SyncTriplet.SyncTriplet> semiSyncTriplets = null;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private ISession session;

        public ChangeLogProcessor (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session, BlockingCollection<SyncTriplet.SyncTriplet> semiSyncTriplets)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.session = session;
            this.semiSyncTriplets = semiSyncTriplets;
        }

        public void Start ()
        {

            Console.WriteLine (" Start Changelog process :");

            Config.CmisSyncConfig.Feature features = null;
            if (ConfigManager.CurrentConfig.GetFolder (cmisSyncFolder.Name) != null)
                features = ConfigManager.CurrentConfig.GetFolder (cmisSyncFolder.Name).SupportedFeatures;

            int maxNumItems = (features != null && features.MaxNumberOfContentChanges != null) ?  // TODO if there are more items, either loop or force CrawlSync
                (int)features.MaxNumberOfContentChanges : 50;


            string lastTokenOnClient = cmisSyncFolder.Database.GetChangeLogToken ();
            string lastTokenOnServer = CmisUtils.GetChangeLogToken (session);

            if (lastTokenOnClient == lastTokenOnServer) {
                Console.WriteLine ("  Synchronized ");
                // TODO: for debug
                //return;
            }
            if (lastTokenOnClient == null) {
                Console.WriteLine ("  Should do full sync! Local token is null");
                return;
            }

            // ChangeLog tokens are different, so checking changes is needed.
            var currentChangeToken = lastTokenOnClient;
            IChangeEvents changes;
            do {
                // Check which documents/folders have changed.
                changes = session.GetContentChanges (currentChangeToken, cmisSyncFolder.CmisProfile.CmisProperties.IsPropertyChangesSupported, 50);//maxNumItems);

                /*
                 * Changelogtoken's first item is the change caused by lastest
                 * recorded change-log-token in our database. Since getContentChanges
                 * will get changes from CurrentChangeToken, it must be duplicated.
                 * Therefore one should remove it.
                 */
                var changeEvents = changes.ChangeEventList.Where (p => p != changes.ChangeEventList.FirstOrDefault ()).ToList ();

                foreach (IChangeEvent e in changeEvents) {
                    try {
                        ICmisObject obj = session.GetObject (e.ObjectId, false);
                        Console.WriteLine ("  -- {0} event: {1}", obj.Name, e.ChangeType); 
                        if (obj is IFolder) {
                            Console.WriteLine ("  -- {0} is Folder, id = {1}", ((IFolder)obj).Path, ((IFolder)obj).Id);
                        } else if (obj is IDocument) {
                            Console.WriteLine (" -- {0} is Document, id = {1}", ((IDocument)obj).Name, ((IDocument)obj).Id);
                        }
                    } catch(Exception ex) {
                        Console.WriteLine ("  -- Cmis obj is not found, id = {0}", e.ObjectId);

                        var dbpath = cmisSyncFolder.Database.GetPathById (e.ObjectId.Split (CmisUtils.CMIS_FILE_SEPARATOR).Last ());
                        string localpath = dbpath == null ? null : dbpath [0];

                        Console.WriteLine ("  --  {1} event: {0}", e.ChangeType, localpath == null ? e.ObjectId : localpath);
                    }
                }


                currentChangeToken = changes.LatestChangeLogToken;

                //database.SetChangeLogToken (currentChangeToken);
            }
            // Repeat if there were two many changes to fit in a single response.
            while (changes.HasMoreItems ?? false);

            //database.SetChangeLogToken (lastTokenOnServer);

        }
    }
}
#pragma warning restore 0414, 0219