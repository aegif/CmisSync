using System;
using System.Text;
using CmisSync.Lib;

namespace CmisSync
{
    /// <summary>
    /// Conversion of path between CMIS and Microsft Windows.
    /// Many paths that are valid on CMIS are invalid (or would have a different meaning) on Windows.
    /// 
    /// The trick here is to replace Windows-forbidden characters with their equivalent two-bytes representation.
    /// The same two-bytes representations must not be used with a different meaning on the Windows side.
    /// </summary>
    public class WindowsPathRepresentationConverter : IPathRepresentationConverter
    {
        /// <summary>
        /// Convert a path from CMIS to Windows.
        /// </summary>
        public string RemoteToLocal(string remotePath)
        {
            if (String.IsNullOrEmpty(remotePath))
            {
                return remotePath;
            }

            string path = remotePath;

            // On the CMIS side, backward slash is not a special character, it can be part of a document's name.
            path = path.Replace('\\', '￥');

            // CMIS slash to Windows backward slash.
            path = path.Replace('/', '\\'); // Convert CMIS file separator to Windows file separator.

            // Other characters.
            //path = path.Replace('<', '＜'); // The < character is allowed on CMIS, but not on Windows, so thr trick is to use its two-bytes representation.
            //path = path.Replace('>', '＞');
            //path = path.Replace(':', '：');
            //path = path.Replace('*', '＊');
            //path = path.Replace('?', '？');
            //path = path.Replace('|', '｜');

            // Only for tests on ECMs that have the same character restrictions as Windows, such as Alfresco.
            //path = path.Replace('&', '＆');

            return path;
        }


        /// <summary>
        /// Convert a path from Windows to CMIS.
        /// </summary>
        public string LocalToRemote(string localPath)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                return localPath;
            }

            string path = localPath;

            // Revert back from two-bytes representation. Backward slashes have no special meaning on the CMIS side.
            path = path.Replace('￥', '\\');

            // Windows backward slashe to CMIS slash.
            path = path.Replace('\\', '/'); // Convert Windows file separator to CMIS file separator.

            // Other characters
            //path = path.Replace('＜', '<'); // Revert back from two-bytes representation.
            //path = path.Replace('＞', '>');
            //path = path.Replace('：', ':');
            //path = path.Replace('＊', '*');
            //path = path.Replace('？', '?');
            //path = path.Replace('｜', '|');

            // Only for tests on ECMs that have the same character restrictions as Windows, such as Alfresco.
            //path = path.Replace('＆', '&');

            return path;
        }
    }
}

