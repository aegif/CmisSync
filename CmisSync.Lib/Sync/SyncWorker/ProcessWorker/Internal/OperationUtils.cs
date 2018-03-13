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
using CmisSync.Lib.Utilities.UserNotificationListener;
using CmisSync.Lib.Sync.SynchronizeItem;
using CmisSync.Lib.Utilities.FileUtilities;

namespace CmisSync.Lib.Sync.SyncWorker.ProcessWorker.Internal
{
    public static class OperationUtils
    {
        private static readonly ILog Logger = LogManager.GetLogger (typeof (OperationUtils));

        public static string GetLocalFullPath(SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder) {
            return triplet.LocalExist? 
                          triplet.LocalStorage.FullPath:
                          Utils.PathCombine (
                          cmisSyncFolder.LocalPath,
                              String.Join (Path.DirectorySeparatorChar.ToString (),
                                           triplet.RemoteStorage.RelativePath.Split (CmisUtils.CMIS_FILE_SEPARATOR)));

        }

        public static string GetLocalRelativePath(string localFullPath, CmisSyncFolder.CmisSyncFolder cmisSyncFolder) {
            return localFullPath.Substring (cmisSyncFolder.LocalPath.Length).TrimStart (Path.DirectorySeparatorChar);
        }

        public static string GetRemoteFullPath (SyncTriplet.SyncTriplet triplet, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            return triplet.RemoteExist ? 
                          triplet.RemoteStorage.FullPath :
                          CmisFileUtil.PathCombine(
                              cmisSyncFolder.RemotePath,
                              String.Join (CmisUtils.CMIS_FILE_SEPARATOR.ToString(), 
                                           triplet.LocalStorage.RelativePath.Split (Path.DirectorySeparatorChar)));

        }

        public static string GetRemoteRelativePath (string remoteFullPath, CmisSyncFolder.CmisSyncFolder cmisSyncFolder)
        {
            return remoteFullPath.Substring (cmisSyncFolder.RemotePath.Length).TrimStart (CmisUtils.CMIS_FILE_SEPARATOR);
        }


        public static byte [] DownloadStream (DotCMIS.Data.IContentStream contentStream, string filePath)
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

        public static Dictionary<string, string []> FetchMetadata (IDocument document, ISession session)
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

        public static bool SetLastModifiedDate (IDocument remoteDocument, string filepath, Dictionary<string, string []> metadata)
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
    }
}
