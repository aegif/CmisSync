using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS.Client;
using log4net;
using DotCMIS.Client.Impl;
using System.IO;

namespace CmisSync.Lib.Cmis
{
    /// <summary>
    /// TODO merge with Config.Feature ?
    /// </summary>
    public class CmisProfile
    {
        /// <summary>
        /// Log.
        /// </summary>
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(CmisProfile));

        /// <summary>
        /// Whether the CMIS server supports ordering by contentStreamFileName or not.
        /// </summary>
        public bool contentStreamFileNameOrderable { get; set; }

        /// <summary>
        /// Whether to set local file names based on cmis:contentStreamName (true) or cmis:name (false)
        /// true is typically a good choice on Documentum
        /// false is typically a good choice on Alfresco
        /// </summary>
        public bool UseCmisStreamName { get; set; }

        public bool IgnoreIfSameLowercaseNames { get; set; }

        public CmisProfile()
        {
            contentStreamFileNameOrderable = false; // FIXME get that info from repository

            UseCmisStreamName = true;

            IgnoreIfSameLowercaseNames = !IsFileSystemCaseSensitive();
        }

        /// <summary>
        /// Return the local filename to use for a CMIS document.
        /// This name must be unique in the folder, in practice it could be cmis:name or cmis:contentStreamFilename or cmis:objectId or a custom property.
        /// This name is then passed to the IPathRepresentationConverter which takes care of the peculiarities of the local filesystem.
        /// </summary>
        public string localFilename(IDocument document)
        {
            // Can be useful for tests, making local filenames radically different, thus making bugs easier to catch:
            // return document.Id;

            if (UseCmisStreamName)
            {
                if (document.ContentStreamFileName != null)
                {
                    return document.ContentStreamFileName;
                }
                else
                {
                    // This should probably never happen theoretically, but anyway that happens sometimes in Alfresco.
                    Logger.Warn("cmis:contentStreamFileName not set for \"" + document.Paths[0] + "\", using cmis:name \"" + document.Name + "\" as a file name instead");
                    return document.Name;
                }
            }
            else
            {
                return document.Name;
            }
        }


        /// <summary>
        /// Name used as part of the remote path in the local CmisSync database.
        /// This must be unique on the remote folder.
        /// </summary>
        public string remoteFilename(IDocument document)
        {
            return (string)document.GetPropertyValue("cmis:name");
        }


        /// <summary>
        /// Prepare the given OperationContext for use with this CMIS profile.
        /// </summary>
        /// <param name="operationContext"></param>
        public void ConfigureOperationContext(IOperationContext operationContext)
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


        /// <summary>
        /// Ignore folders and documents with a name that contains a slash.
        /// While it is very rare, Documentum is known to allow that and mistakenly present theses as CMIS object, violating the CMIS specification.
        /// </summary>
        public bool RemoteObjectWorthSyncing(ICmisObject cmisObject)
        {
            if (cmisObject.Name.Contains('/'))
            {
                Logger.Warn("Ignoring remote object " + cmisObject.Name + " as it contains a slash. The CMIS specification forbids slashes in path elements (paragraph 2.1.5.3), please report the bug to your server vendor");
                return false;
            }
            else
            {
                return true;
            }
        }


        /// <summary>
        /// Whether the operating system is case-sensitive.
        /// For instance on Linux you can have two files/folders called "test" and "TEST", but on Windows that does not work.
        /// CMIS allows for case-sensitive names.
        /// This method does not extend to mounted filesystems, which might have different properties.
        /// </summary>
        /// <returns>true if case sensitive</returns>
        private static bool IsFileSystemCaseSensitive()
        {
            // Actually try.
            string file = Path.GetTempPath() + "test";
            File.CreateText(file).Close();
            bool result = File.Exists("TEST");
            File.Delete(file);

            return result;
        }
    }
}
