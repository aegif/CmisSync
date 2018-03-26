using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;

using log4net;
using CmisSync.Auth;
using CmisSync.Lib.ActivityListener;
using CmisSync.Lib.Config;
using CmisSync.Lib.Sync;
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

        private ConcurrentDictionary<string, SyncTriplet.SyncTriplet> remoteBuffer = null;

        private ISession session;

        private CmisSyncFolder.CmisSyncFolder cmisSyncFolder = null;

        private CmisProperties cmisProperties = null;

        public RemoteCrawlWorker (CmisSyncFolder.CmisSyncFolder cmisSyncFolder, ISession session,
                                  ConcurrentDictionary<string, SyncTriplet.SyncTriplet> remoteBuffer)
        {
            this.cmisSyncFolder = cmisSyncFolder;
            this.cmisProperties = cmisSyncFolder.CmisProfile.CmisProperties;
            this.session = session;
            this.remoteBuffer = remoteBuffer;
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

        public void CrawlRemoteFolder(IFolder remoteFolder, IOperationContext context) {

            IItemEnumerable<ICmisObject> children = remoteFolder.GetChildren (context); 
            foreach (ICmisObject cmisObject in children) {
                try {
                    if (cmisObject is DotCMIS.Client.Impl.Folder) {
                        IFolder subFolder = (IFolder)cmisObject;
                        SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromRemoteFolder (subFolder, this.cmisSyncFolder);

                        if (this.cmisProperties.IgnoreIfSameLowercaseNames && remoteBuffer.ContainsKey (triplet.Name.ToLowerInvariant ())) {
                            Logger.Warn ("Ignoring " + triplet.RemoteStorage.RelativePath + "because other file or folder has the same name when ignoring lowercase/uppercase");
                            continue;
                        }
                        if (!CmisFileUtil.RemoteObjectWorthSyncing (subFolder)) {
                            continue;
                        }

                        // Console.WriteLine (" % get remote sub - folder: {0}", triplet.RemoteStorage.RelativePath);

                        if (!remoteBuffer.TryAdd (cmisProperties.IgnoreIfSameLowercaseNames ? triplet.Name.ToLowerInvariant () : triplet.Name, triplet)) {
                            Logger.Error ("Adding " + triplet.Name + " to remote buffer failed!");
                        }

                        CrawlRemoteFolder (subFolder, context);

                    } else {
                        if (cmisObject is DotCMIS.Client.Impl.Document) {
                            IDocument document = (IDocument)cmisObject;

                            SyncTriplet.SyncTriplet triplet = SyncTripletFactory.CreateSFGFromRemoteDocument (remoteFolder, document, this.cmisSyncFolder);

                            if (this.cmisProperties.IgnoreIfSameLowercaseNames && remoteBuffer.ContainsKey (triplet.Name.ToLowerInvariant ())) {
                                Logger.Warn ("Ignoring " + triplet.RemoteStorage.RelativePath + "because other file or folder has the same name when ignoring lowercase/uppercase");
                                continue;
                            }
                            if (!CmisFileUtil.RemoteObjectWorthSyncing (document)) {
                                continue;
                            }

                            // Console.WriteLine (" % get remote file: {0}", triplet.RemoteStorage.RelativePath);

                            if (!remoteBuffer.TryAdd (cmisProperties.IgnoreIfSameLowercaseNames ? triplet.Name.ToLowerInvariant () : triplet.Name, triplet)) {
                                Logger.Error ("Adding " + triplet.Name + " to remote buffer failed!");
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
