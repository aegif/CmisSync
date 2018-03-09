using System;
using System.Linq;
using System.Text;
using DotCMIS.Client;
using log4net;
using DotCMIS.Client.Impl;
using System.IO;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync;

namespace CmisSync.Lib.Utilities.FileUtilities
{
    public class CmisFileUtil
    {

        protected static readonly ILog Logger = LogManager.GetLogger (typeof (CmisFileUtil));

        /// <summary>
        /// Return the local filename to use for a CMIS document.
        /// This name must be unique in the folder, in practice it could be cmis:name or cmis:contentStreamFilename or cmis:objectId or a custom property.
        /// This name is then passed to the IPathRepresentationConverter which takes care of the peculiarities of the local filesystem.
        /// </summary>
        public static string GetLocalFileName (IDocument document, CmisProfileRefactor cmisProfile)
        {
            // Can be useful for tests, making local filenames radically different, thus making bugs easier to catch:
            // return document.Id;

            if (cmisProfile.CmisProperties.UseCmisStreamName) {
                if (document.ContentStreamFileName != null) {
                    return document.ContentStreamFileName;
                } else {
                    // This should probably never happen theoretically, but anyway that happens sometimes in Alfresco.
                    Logger.Warn ("cmis:contentStreamFileName not set for \"" + document.Paths [0] + "\", using cmis:name \"" + document.Name + "\" as a file name instead");
                    return document.Name;
                }
            } else {
                return document.Name;
            }
        }


        /// <summary>
        /// Name used as part of the remote path in the local CmisSync database.
        /// This must be unique on the remote folder.
        /// </summary>
        public static string GetRemoteFileName (IDocument document)
        {
            return (string)document.GetPropertyValue ("cmis:name");
        }

        /// <summary>
        /// Ignore folders and documents with a name that contains a slash.
        /// While it is very rare, Documentum is known to allow that and mistakenly present theses as CMIS object, violating the CMIS specification.
        /// </summary>
        public static bool RemoteObjectWorthSyncing (ICmisObject cmisObject)
        {
            if (cmisObject.Name.Contains ('/')) {
                Logger.Warn ("Ignoring remote object " + cmisObject.Name + " as it contains a slash. The CMIS specification forbids slashes in path elements (paragraph 2.1.5.3), please report the bug to your server vendor");
                return false;
            } else {
                return true;
            }
        }


        /// <summary>
        /// Equivalent of .NET Path.Combine, but for CMIS paths.
        /// CMIS paths always use forward slashes.
        /// </summary>
        public static string PathCombine (string cmisPath1, string cmisPath2)
        {
            if (String.IsNullOrEmpty (cmisPath1))
                return cmisPath2;

            if (String.IsNullOrEmpty (cmisPath2))
                return cmisPath1;

            // If the first path ends with a separator, just concatenate.
            if (cmisPath1.EndsWith (CmisUtils.CMIS_FILE_SEPARATOR.ToString ()))
                return cmisPath1 + cmisPath2;

            return cmisPath1 + CmisUtils.CMIS_FILE_SEPARATOR + cmisPath2;
        }


        /// <summary>
        /// Get the last part of a CMIS path
        /// Example: "/the/path/< 9000/theleaf" returns "theleaf"
        /// Why not use Path.GetFileName ? Because it chokes on characters that are not authorized on the local filesystem.
        /// </summary>
        /// <returns></returns>
        public static string GetLeafname (string cmisPath)
        {
            return cmisPath.Split ('/').Last ();
        }

        public static string GetUpperFolderOfCmisPath (string cmisPath)
        {
            return cmisPath.Substring (0, cmisPath.LastIndexOf (CmisUtils.CMIS_FILE_SEPARATOR));
        }

        /// <summary>
        /// Whether the operating system is case-sensitive.
        /// For instance on Linux you can have two files/folders called "test" and "TEST", but on Windows that does not work.
        /// CMIS allows for case-sensitive names.
        /// This method does not extend to mounted filesystems, which might have different properties.
        /// </summary>
        /// <returns>true if case sensitive</returns>
        public static bool IsFileSystemCaseSensitive ()
        {
            // Actually try.
            string file = Path.GetTempPath () + "test";
            File.CreateText (file).Close ();
            bool result = File.Exists ("TEST");
            File.Delete (file);

            return result;
        }

        /// <summary>
        /// Prepare the given OperationContext for use with this CMIS profile.
        /// </summary>
        /// <param name="operationContext"></param>
        public static void ConfigureOperationContext (IOperationContext operationContext)
        {
            /*
            Disabled because repository may generate error if the type is not Orderable for cmis:contentStreamFileName
            Documentum generates such an error: https://github.com/aegif/CmisSync/issues/724
            Alfresco also is not Orderable even though it does not generate an error:
            http://stackoverflow.com/questions/39290294/check-whether-cmiscontentstreamfilename-is-orderable
            
            if (IgnoreIfSameLowercaseNames)
            {
                // Depending on the CMIS profile, order by stream name or document name.
                if (UseCmisStreamName && contentStreamFileNameOrderable)
                {
                    operationContext.OrderBy = "cmis:contentStreamFileName";
                }
                else
                {
                    operationContext.OrderBy = "cmis:name";
                }
            }
            else
            {
                // Do not specify an order criteria, as we don't need it,
                // and it might have a performance impact on the CMIS server.
            }
            */
        }


    }
}
