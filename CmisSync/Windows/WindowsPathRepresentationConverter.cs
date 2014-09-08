using System;
using System.Text;
using CmisSync.Lib;

namespace CmisSync
{
    public class WindowsPathRepresentationConverter : IPathRepresentationConverter
    {
    
        public string LocalToRemote(string localPath)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                return localPath;
            }

            return localPath.Replace('\\', '/');
        }

        public string RemoteToLocal(string remotePath)
        {
            if (String.IsNullOrEmpty(remotePath))
            {
                return remotePath;
            }

            return remotePath.Replace('/', '\\');
        }
    }
}

