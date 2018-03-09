using System;
using CmisSync.Lib.Utilities.FileUtilities;

namespace CmisSync.Lib.Cmis
{
    public class CmisProperties
    {
        /// <summary>
        /// At which degree the repository supports Change Logs.
        /// See http://docs.oasis-open.org/cmis/CMIS/v1.0/os/cmis-spec-v1.0.html#_Toc243905424
        /// The possible values are actually none, objectidsonly, properties, all
        /// But for now we only distinguish between none (false) and the rest (true)
        /// </summary>
        public bool ChangeLogCapability { get; set; }

        /// <summary>
        /// If the repository is able send a folder tree in one request, this is true,
        /// Otherwise the default behaviour is false
        /// </summary>
        public bool IsGetFolderTreeSupported { get; set; }

        /// <summary>
        /// If the repository allows to request all Descendants of a folder or file,
        /// this is set to true, otherwise the default behaviour is false
        /// </summary>
        public bool IsGetDescendantsSupported { get; set; }

        /// <summary>
        /// Is true, if the repository is able to return property changes.
        /// </summary>
        public bool IsPropertyChangesSupported { get; set; }

        /// <summary>
        /// Whether the CMIS server supports ordering by contentStreamFileName or not.
        /// </summary>
        public bool ContentStreamFileNameOrderable { get; set; }

        /// <summary>
        /// Whether to set local file names based on cmis:contentStreamName (true) or cmis:name (false)
        /// true is typically a good choice on Documentum
        /// false is typically a good choice on Alfresco
        /// </summary>
        public bool UseCmisStreamName { get; set; }

        public bool IgnoreIfSameLowercaseNames { get; set; }

        public CmisProperties ()
        {
            ContentStreamFileNameOrderable = false; // FIXME get that info from repository

            UseCmisStreamName = true;

            IgnoreIfSameLowercaseNames = !CmisFileUtil.IsFileSystemCaseSensitive ();

            ChangeLogCapability = false;

            IsGetFolderTreeSupported = false;

            IsGetDescendantsSupported = false;

            IsPropertyChangesSupported = false;
        }
    }
}
