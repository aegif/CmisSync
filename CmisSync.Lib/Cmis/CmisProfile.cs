using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS.Client;
using log4net;

namespace CmisSync.Lib.Cmis
{
    public class CmisProfile
    {
        /// <summary>
        /// Log.
        /// </summary>
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(CmisProfile));

        /// <summary>
        /// Whether to set local file names based on cmis:contentStreamName (true) or cmis:name (false)
        /// true is typically a good choice on Documentum
        /// false is typically a good choice on Alfresco
        /// </summary>
        public bool UseCmisStreamName { get; set; }

        public CmisProfile()
        {
            UseCmisStreamName = true;
        }

        /// <summary>
        /// Return the local filename to use for a CMIS document.
        /// This name must be unique in the folder, in practice it could be cmis:name or cmis:contentStreamFilename or cmis:objectId or a custom property.
        /// This name is then passed to the IPathRepresentationConverter which takes care of the peculiarities of the local filesystem.
        /// </summary>
        public string localFilename(IDocument document)
        {
            if (UseCmisStreamName)
            {
                if (document.ContentStreamFileName != null)
                {
                    return document.ContentStreamFileName;
                }
                else
                {
                    // This should probably never happen theoretically, but anyway that happens sometimes in Alfresco.
                    Logger.Error("ContentStreamFileName null for " + document.Name + "(" + document.Paths[0] + ")");
                    return document.Name;
                }
            }
            else
            {
                return document.Name;
            }
        }
    }
}
