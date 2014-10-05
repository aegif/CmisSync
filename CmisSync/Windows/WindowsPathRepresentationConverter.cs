using System;
using System.Text;
using CmisSync.Lib;

namespace CmisSync
{
    /// <summary></summary>
    public class WindowsPathRepresentationConverter : IPathRepresentationConverter
    {
        /// <summary></summary>
        /// <param name="localPath"></param>
        /// <returns></returns>
        public string LocalToRemote(string localPath)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                return localPath;
            }

            return localPath.Replace('\\', '/');
        }

        /// <summary></summary>
        /// <param name="remotePath"></param>
        /// <returns></returns>
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

