using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;

using log4net;
using CmisSync.Auth;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync.SyncMachine.Internal;
using CmisSync.Lib.Sync.SyncTriplet;
using CmisSync.Lib.Utilities.FileUtilities;
using CmisSync.Lib.Cmis;

using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace CmisSync.Lib.Sync.SyncMachine.Crawler
{

    /*
     * Memo一下：
     *   现在的做法是，把remote的部分塞到remoteBuffer这个map里，assembler每次
     * 得到一个local生成的triplet后去map里找有没有已经get的remotestorage，若有
     * 就直接拿来用，否则自己去get对应的remote部分然后把生成的triplet扔给processor。
     * 同时把get到的remote部分标记在remotebuffer里。
     *   因此assembler对buffer的写入优先级大于remotebuffer。
     * 
     * 不过这个流程是建立在 local->remote 的优先级大于 remote->local 的基础上的，
     * 如果是单项同步或者 remote->local 优先级更大，则需要要把RemoteCrawlWorker的
     * 流程修改到和LocalCrawlWorker一样，同时需要1个dictionary来标记，以避免local
     * 和remote同时创建同一个 LS-DB-RS triplet。这样需要2把锁外加consumer的empty 
     * blocking + 新到元素唤醒 or busy waiting + increasement sleep，c#自己的
     * concurrent模块不提供。 
     * 
     * 另外，实际在mac中的测试表明由于local crawl十分迅速，绝大部分时候单独的remote 
     * crawl几乎没有任何效果, （10000个文件的话还是有效果的，但如果双方都是正序loop的话
     * 几乎一定是没用）。用锁同时把remote buffer和remoteUsedIndicator都lock了
     */

    /*
     * TODO: multi filing
     */
    public class RemoteCrawlWorker : IDisposable
    {

        private static readonly ILog Logger = LogManager.GetLogger (typeof (RemoteCrawlWorker));

        private OrderedDictionary orderedRemoteBuffer;

        private object lockObj;

        private ISession session;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder = null;

        private CmisProperties cmisProperties = null;

        private ItemsDependencies itemsDeps = null;

        // locker is the common lock shared with Assembler
        public RemoteCrawlWorker (
            CmisSyncFolder.CmisSyncFolder cmisSyncFolder,
            ISession session,
            OrderedDictionary ordBuffer,
            object locker,
            ItemsDependencies idps
        )
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.cmisProperties = cmisSyncFolder.CmisProfile.CmisProperties;
            this.session = session;
            this.orderedRemoteBuffer = ordBuffer;
            this.lockObj = locker;
            this.itemsDeps = idps;
        }

        public void Start() {

            IOperationContext operationContext = session.CreateOperationContext ();
            operationContext.FilterString = "";

            operationContext.IncludeAllowableActions = true;
            operationContext.IncludePolicies = true;
            operationContext.IncludeRelationships = IncludeRelationshipsFlag.Both;
            operationContext.IncludeAcls = true;

            // ConfigureOperationContext is commented out in CmisSync
            // cmisSyncFolder.CmisProfile.CmisProperties.ConfigureOperationContext (operationContext);
            operationContext.MaxItemsPerPage = Int32.MaxValue;

            CrawlRemoteFolder (this.cmisSyncFolder.RemoteRootFolder, operationContext);

        }

        // The order of appending triplet is important when consider deletion conflict
        // therefore orderedRemoteBuffer is an OrderedDictionary
        public void CrawlRemoteFolder(IFolder remoteFolder, IOperationContext context) {

            string folderName = remoteFolder.Path.Equals(cmisSyncFolder.RemotePath) ? 
                                            CmisUtils.CMIS_FILE_SEPARATOR.ToString() : SyncTripletFactory.CreateFromRemoteFolder (remoteFolder, cmisSyncFolder).Name;


            IItemEnumerable<ICmisObject> children = remoteFolder.GetChildren (context); 
            foreach (ICmisObject cmisObject in children) {

                try {
                    // process sub folders
                    if (cmisObject is DotCMIS.Client.Impl.Folder) {
                        IFolder subFolder = (IFolder)cmisObject;
                        SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFPFromRemoteFolder (subFolder, this.cmisSyncFolder);

                        lock (lockObj) {
                            if (this.cmisProperties.IgnoreIfSameLowercaseNames && orderedRemoteBuffer.Contains(triplet.Name.ToLowerInvariant ())) {
                                Logger.Warn ("Ignoring " + triplet.RemoteStorage.RelativePath + "because other file or folder has the same name when ignoring lowercase/uppercase");
                                continue;
                            }
                            if (!CmisFileUtil.RemoteObjectWorthSyncing (subFolder)) {
                                continue;
                            }

                            if (!triplet.DBExist) {
                                // Console.WriteLine (" % get remote sub - folder: {0}", triplet.RemoteStorage.RelativePath);
                                orderedRemoteBuffer.Add (cmisProperties.IgnoreIfSameLowercaseNames ? triplet.Name.ToLowerInvariant () : triplet.Name, triplet);
                            }
                        }

                        CrawlRemoteFolder (subFolder, context);

                        // the same with local crawler, if triplet is not freshly created, always add folder triplet after its contents
                        if (triplet.DBExist) {
                            lock (lockObj) {
                                orderedRemoteBuffer.Add (cmisProperties.IgnoreIfSameLowercaseNames ? triplet.Name.ToLowerInvariant () : triplet.Name, triplet);
                            }

                            // if triplet is not DBExist, it will result in 2 possible operstions:
                            //  - create locally, while Directory.CreateDirectory is thread safe, we can igore dependence resolving.
                            //  - conflict, download to local, there must be a local directory with the same name, no necessary for dependence resolving.
                            itemsDeps.AddItemDependence (folderName, triplet.Name);
                        }

                    } else {
                        if (cmisObject is DotCMIS.Client.Impl.Document) {
                            IDocument document = (IDocument)cmisObject;

                            SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFPFromRemoteDocument (remoteFolder, document, this.cmisSyncFolder);

                            lock (lockObj) {
                                if (this.cmisProperties.IgnoreIfSameLowercaseNames && orderedRemoteBuffer.Contains (triplet.Name.ToLowerInvariant ())) {
                                    Logger.Warn ("Ignoring " + triplet.RemoteStorage.RelativePath + "because other file or folder has the same name when ignoring lowercase/uppercase");
                                    continue;
                                }
                                if (!CmisFileUtil.RemoteObjectWorthSyncing (document)) {
                                    continue;
                                }

                                // Console.WriteLine (" % get remote file: {0}", triplet.RemoteStorage.RelativePath);
                                orderedRemoteBuffer.Add (cmisProperties.IgnoreIfSameLowercaseNames ? triplet.Name.ToLowerInvariant () : triplet.Name, triplet);
                            }

                            if (triplet.DBExist) {
                                // if triplet is not DBExist, it will result in 2 possible operstions:
                                //  - download to local, while Directory.CreateDirectory is thread safe, we can igore dependence resolving.
                                //  - conflict, download to local, there must be a local directory with the same name, no necessary for dependence resolving.
                                itemsDeps.AddItemDependence (folderName, triplet.Name);
                            }
                        }

                        else if (isLink(cmisObject)) {
                            Logger.Debug("Ignoring file '" + remoteFolder + "/" + cmisObject.Name + "' of type '" +
                                cmisObject.ObjectType.Description + "'. Links are not currently handled.");
                        } else {
                            Logger.Warn("Unknown object type: '" + cmisObject.ObjectType.Description + "' (" + cmisObject.ObjectType.DisplayName
                                + ") for object " + remoteFolder + "/" + cmisObject.Name);
                        }
                    }
                } catch (CmisBaseException e) {
                    
                }
            }
        } 

        private bool isLink (ICmisObject cmisObject)
        {
            IObjectType parent = cmisObject.ObjectType.GetParentType ();
            while (parent != null) {
                if (parent.Id.Equals ("I:cm:link")) {
                    return true;
                }
                parent = parent.GetParentType ();
            }
            return false;
        }

        ~RemoteCrawlWorker ()
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
                    this.disposed = true;
                }
            }
        }
    }
}
