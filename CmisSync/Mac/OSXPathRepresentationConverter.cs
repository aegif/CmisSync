using System;
using System.Text;
using MonoMac.Foundation;
using CmisSync.Lib;

namespace CmisSync
{
    public class OSXPathRepresentationConverter : IPathRepresentationConverter
    {
    
        public string LocalToRemote(string localPath)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                return localPath;
            }

            return localPath.Normalize(NormalizationForm.FormC);
        }

        public string RemoteToLocal(string remotePath)
        {
            if (String.IsNullOrEmpty(remotePath))
            {
                return remotePath;
            }

            var url = NSUrl.FromFilename(remotePath);
            if (remotePath.StartsWith("/"))
            {
                return url.Path;
            }

            return url.Path.Substring(1);
        }
    }
}

