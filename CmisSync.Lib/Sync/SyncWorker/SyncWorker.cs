using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;

using log4net;
using System.IO;
using CmisSync.Auth;
using CmisSync.Lib.ActivityListener;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync;
using CmisSync.Lib.Sync.SyncMachine;
using CmisSync.Lib.Sync.SyncMachine.Exceptions;
using CmisSync.Lib.Cmis;

using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync.SyncWorker
{
    public class SyncWorker : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger (typeof (SyncWorker));

        private SyncMachine.SyncMachine syncMachine;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder;

        private ISession session;

        private IFolder remoteRootFolder;

        private bool isFirstSyncing = false;

        public SyncWorker (CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            Logger.Debug ("SyncWorker created.");
        }

        public void Initialize ()
        {
            Connect ();

            remoteRootFolder = null;
            try {
                remoteRootFolder = (IFolder)this.session.GetObjectByPath (cmisSyncFolder.RemotePath, true);
            } catch (PermissionDeniedException e) {
                session = null;
                Connect ();
                remoteRootFolder = (IFolder)this.session.GetObjectByPath (cmisSyncFolder.RemotePath, true);
            }

            cmisSyncFolder.RemoteRootFolder = remoteRootFolder;

            if (remoteRootFolder == null) {
                //todo
                return;
            }

            syncMachine = new SyncMachine.SyncMachine (cmisSyncFolder, session);

        }

        public void Disconnect () {
            session.Clear ();
        }


        public void DoSync() {

            //syncMachine.DoWatcherTest (); return;

            // syncMachine.DoChangeLogTest (); return;

            isFirstSyncing = true;

            if (isFirstSyncing)
                syncMachine.DoCrawlSync ();
            else {
                if (!syncMachine.DoChangeLogSync ()) {
                    Console.WriteLine ("Change Log Processor return broken: {0}, do full craw sync");
                    syncMachine.DoCrawlSync ();
                }
            }
        }

        private void Connect ()
        {
            CmisProfileRefactor cmisProfile = cmisSyncFolder.CmisProfile;
            CmisProperties cmisProperties = cmisProfile.CmisProperties;

            // Create session.
            this.session = Auth.Authentication.GetCmisSession (cmisProfile.RemoteUri.ToString (), cmisProfile.User, cmisProfile.Password.ToString (), cmisProfile.RepoID);

            Logger.Debug ("Created CMIS session: " + session.ToString ());

            // Detect repository capabilities.
            cmisProperties.ChangeLogCapability = session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;

            cmisProperties.IsGetDescendantsSupported = session.RepositoryInfo.Capabilities.IsGetDescendantsSupported == true;
            cmisProperties.IsGetFolderTreeSupported = session.RepositoryInfo.Capabilities.IsGetFolderTreeSupported == true;

            //repoInfo.CmisProfile.contentStreamFileNameOrderable = session.RepositoryInfo.Capabilities. TODO

            Config.CmisSyncConfig.SyncConfig.Folder folder = ConfigManager.CurrentConfig.GetFolder (this.cmisSyncFolder.Name);

            if (folder != null) {

                Config.CmisSyncConfig.Feature features = folder.SupportedFeatures;
                if (features != null) {

                    if (cmisProperties.IsGetDescendantsSupported && features.GetDescendantsSupport == false)
                        cmisProperties.IsGetDescendantsSupported = false;

                    if (cmisProperties.IsGetFolderTreeSupported && features.GetFolderTreeSupport == false)
                        cmisProperties.IsGetFolderTreeSupported = false;

                    if (cmisProperties.ChangeLogCapability && features.GetContentChangesSupport == false)
                        cmisProperties.ChangeLogCapability = false;

                    if (cmisProperties.ChangeLogCapability && session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                        || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.Properties)
                        cmisProperties.IsPropertyChangesSupported = true;
                }
            }

            Logger.Debug ("ChangeLog capability: " + cmisProperties.ChangeLogCapability.ToString ());
            Logger.Debug ("Get folder tree support: " + cmisProperties.IsGetFolderTreeSupported.ToString ());
            Logger.Debug ("Get descendants support: " + cmisProperties.IsGetDescendantsSupported.ToString ());
            /*if (repoInfo.ChunkSize > 0) {
                Logger.Debug ("Chunked Up/Download enabled: chunk size = " + repoInfo.ChunkSize.ToString () + " byte");
                } else {
                Logger.Debug ("Chunked Up/Download disabled");
                }*/

            HashSet<string> filters = new HashSet<string> ();
            filters.Add ("cmis:objectId");
            filters.Add ("cmis:name");
            if (!CmisUtils.IsDocumentum (session)) {
                filters.Add ("cmis:contentStreamFileName");
                filters.Add ("cmis:contentStreamLength");
            }
            filters.Add ("cmis:lastModificationDate");
            filters.Add ("cmis:lastModifiedBy");
            filters.Add ("cmis:path");
            filters.Add ("cmis:changeToken"); // Needed to send update commands, see https://github.com/aegif/CmisSync/issues/516
            session.DefaultContext = session.CreateOperationContext (filters, false, true, false, IncludeRelationshipsFlag.None, null, true, null, true, 100);
        }

        ~SyncWorker ()
        {
            Dispose (false);
        }

        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        private Object disposeLock = new object ();
        private bool disposed = false;
        protected virtual void Dispose (bool disposing)
        {
            lock (disposeLock) {
                if (!this.disposed) {
                    if (disposing) {
                        this.cmisSyncFolder.Dispose ();
                        this.syncMachine.Dispose ();
                   }
                    this.disposed = true;
                }
            }
        }
    }
}
