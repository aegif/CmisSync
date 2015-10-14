using System;
using System.IO;
using log4net;

namespace CmisSync.Lib
{
    /// <summary>
    /// Create a "lock" file in folder that will prevent the user from
    /// moving/deleting/renaming the folder as long as CmisSync is running.
    /// </summary>
    public class FolderLock : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(FolderLock));
        private static readonly string FILENAME = "lock";

        private string lockFilePath;
        private FileStream lockFile;
        private bool disposed = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public FolderLock(string folderPath)
        {
            try
            {
                // Create lock file.
                Logger.Info("Creating folder lock file: " + folderPath);
                lockFilePath = Path.Combine(folderPath, FILENAME);
                if (!File.Exists(lockFilePath))
                {
                    File.WriteAllLines(lockFilePath, new string[0]);
                }
                File.SetAttributes(lockFilePath, File.GetAttributes(lockFilePath) | FileAttributes.Hidden | FileAttributes.System);
                lockFile = File.Open(lockFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (Exception e)
            {
                Logger.Error("Could not create folder lock: " + lockFilePath, e);
            }
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~FolderLock()
        {
            Dispose(false);
        }


        /// <summary>
        /// Implement IDisposable interface. 
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    try
                    {
                        if (lockFile != null)
                        {
                            lockFile.Close();
                        }
                        if (File.Exists(lockFilePath))
                        {
                            File.Delete(lockFilePath);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Could not dispose folder lock: " + lockFilePath, e);
                    }
                }
                this.disposed = true;
            }
        }
    }
}
