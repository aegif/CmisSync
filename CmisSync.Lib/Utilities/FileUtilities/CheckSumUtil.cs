using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CmisSync.Lib.Sync.SynchronizeItem;

namespace CmisSync.Lib.Utilities.FileUtilities
{
    public static class CheckSumUtil
    {
        /// <summary>
        /// Calculate the SHA1 checksum of a file.
        /// Code from http://stackoverflow.com/a/1993919/226958
        /// </summary>
        public static string Checksum (string filePath)
        {
            using (var fs = new FileStream (filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var bs = new BufferedStream (fs)) {
                using (var sha1 = new SHA1Managed ()) {
                    byte [] hash = sha1.ComputeHash (bs);
                    return ChecksumToString (hash);
                }
            }
        }

        /// <summary>
        /// Calculate the SHA1 checksum of a syncitem.
        /// Code from http://stackoverflow.com/a/1993919/226958
        /// </summary>
        /// <param name="item">sync item</param>
        public static string Checksum (SyncItem item)
        {
            using (var fs = new FileStream (item.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var bs = new BufferedStream (fs)) {
                using (var sha1 = new SHA1Managed ()) {
                    byte [] hash = sha1.ComputeHash (bs);
                    return ChecksumToString (hash);
                }
            }
        }

        /// <summary>
        /// Transforms a given hash into a string
        /// </summary>
        private static string ChecksumToString (byte [] hash)
        {
            if (hash == null || hash.Length == 0) return String.Empty;
            StringBuilder formatted = new StringBuilder (2 * hash.Length);
            foreach (byte b in hash) {
                formatted.AppendFormat ("{0:X2}", b);
            }
            return formatted.ToString ();
        }
    }
}
