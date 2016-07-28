﻿using System;
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
        /// Whether to set local file names based on cmis:contentStreamName (true) or cmis:name (false)
        /// true is typically a good choice on Documentum
        /// false is typically a good choice on Alfresco
        /// </summary>
        public bool UseCmisStreamName { get; set; }

        public bool IgnoreIfSameLowercaseNames { get; set; }

        public CmisProfile()
        {
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


        /// <summary>
        /// Prepare the given OperationContext for use with this CMIS profile.
        /// </summary>
        /// <param name="operationContext"></param>
        public void ConfigureOperationContext(IOperationContext operationContext)
        {
            if (IgnoreIfSameLowercaseNames)
            {
                // Depending on the CMIS profile, order by stream name or document name.
                if (UseCmisStreamName)
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
