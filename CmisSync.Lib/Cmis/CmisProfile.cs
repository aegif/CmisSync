using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS.Client;

namespace CmisSync.Lib.Cmis
{
    public class CmisProfile
    {
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

        public string localFilename(IDocument document)
        {
            return UseCmisStreamName ?
                document.ContentStreamFileName
                : document.Name;
        }
    }
}
