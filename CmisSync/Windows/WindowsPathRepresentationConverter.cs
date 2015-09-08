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

            string path = localPath.Replace('\\', '/');

            path = path.Replace('＜', '<');
            path = path.Replace('＞', '>');

            return path;
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

            string path = remotePath.Replace('/', '\\');

            path = path.Replace('<', '＜');
            path = path.Replace('>', '＞');

            return path;
        }
    }
}

