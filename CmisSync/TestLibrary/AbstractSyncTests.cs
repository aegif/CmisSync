using CmisSync.Lib;
using CmisSync.Lib.Cmis;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using log4net.Appender;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static CmisSync.Lib.Sync.CmisRepo;

namespace TestLibrary
{
    public class AbstractSyncTests
    {
        protected readonly string cmisSyncDirectory = ConfigManager.CurrentConfig.FoldersPath;
        protected readonly int HEAVY_FACTOR = 10;
        protected readonly int HEAVY_FILE_SIZE = 1024;

        class TrustAlways : ICertificatePolicy
        {
            public bool CheckValidationResult(ServicePoint sp, X509Certificate certificate, WebRequest request, int error)
            {
                // For testing, always accept any certificate
                return true;
            }
        }

        [TestFixtureSetUp]
        public void ClassInit()
        {
#if __MonoCS__
            Environment.SetEnvironmentVariable("MONO_MANAGED_WATCHER", "enabled");
#endif
            ServicePointManager.CertificatePolicy = new TrustAlways();
        }

        [SetUp]
        public void Init()
        {
            // Redirect log to be easily seen when running test.
            log4net.Config.BasicConfigurator.Configure(new TraceAppender());
        }

        [TearDown]
        public void TearDown()
        {
            // Clear remaining resources.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            // System.Threading.Thread.Sleep(30 * 1000);
        }


        /*/// <summary></summary>
        /// <param name="localDirectory"></param>
        /// <param name="synchronizedFolder"></param>
        private void Clean(string localDirectory, CmisRepo.SynchronizedFolder synchronizedFolder)
        {
            // Sync deletions to server.
            synchronizedFolder.Sync();
            CleanAll(localDirectory);
            synchronizedFolder.Sync();

            // Remove checkout folder.
            Directory.Delete(localDirectory);
        }*/

        protected void Clean(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Remove SQLite database.
            string databasePath = Path.Combine(ConfigFolder(), canonicalName + ".cmissync");
            File.Delete(databasePath);

            // Delete all content of the local path.
            DirectoryInfo localFolder = new DirectoryInfo(localPath);
            foreach (FileInfo file in localFolder.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in localFolder.GetDirectories())
            {
                dir.Delete(true);
            }

            // Delete all content of the remote path.
            Dictionary<string, string> cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] = password;
            cmisParameters[SessionParameter.RepositoryId] = repositoryId;
            cmisParameters[SessionParameter.ConnectTimeout] = "-1";
            ISession session = SessionFactory.NewInstance().CreateSession(cmisParameters);
            Folder folder = (Folder)session.GetObjectByPath(remoteFolderPath);
            foreach (ICmisObject cmisObject in folder.GetChildren())
            {
                if (cmisObject is Folder)
                {
                    Folder subFolder = (Folder)cmisObject;
                    subFolder.DeleteTree(
                        true /* all versions */,
                        UnfileObject.Delete,
                        true /* continue on failure */);
                }
                else
                {
                    cmisObject.Delete(true /* all versions */);
                }
            }
        }

        protected string ConfigFolder()
        {
            return Path.GetDirectoryName(ConfigManager.CurrentConfigFile);
        }

        protected void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        /*private void CleanDirectory(string path)
        {
            // Delete recursively.
            DeleteDirectoryIfExists(path);

            // Delete database.
            string database = path + ".cmissync";
            if (File.Exists(database))
            {
                try
                {
                    File.Delete(database);
                }
                catch (IOException ex)
                {
                    Console.WriteLine("Exception on testing side, ignoring " + database + ":" + ex);
                }
            }

            // Prepare empty directory.
            Directory.CreateDirectory(path);
        }

        private void CleanAll(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);

            try
            {
                // Delete all local files/folders.
                foreach (FileInfo file in directory.GetFiles())
                {
                    /*if (file.Name.EndsWith(".sync"))
                    {
                        continue;
                    }*//*

                    try
                    {
                        file.Delete();
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine("Exception on testing side, ignoring " + file.FullName + ":" + ex);
                    }
                }
                foreach (DirectoryInfo dir in directory.GetDirectories())
                {
                    CleanAll(dir.FullName);

                    try
                    {
                        dir.Delete();
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine("Exception on testing side, ignoring " + dir.FullName + ":" + ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception on testing side, ignoring " + ex);
            }
        }*/

        protected ISession CreateSession(RepoInfo repoInfo)
        {
            Dictionary<string, string> cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = repoInfo.Address.ToString();
            cmisParameters[SessionParameter.User] = repoInfo.User;
            cmisParameters[SessionParameter.Password] = repoInfo.Password.ToString();
            cmisParameters[SessionParameter.RepositoryId] = repoInfo.RepoID;
            cmisParameters[SessionParameter.ConnectTimeout] = "-1";

            return SessionFactory.NewInstance().CreateSession(cmisParameters);
        }

        protected IDocument CreateDocument(IFolder folder, string name, string content)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, name);
            properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

            ContentStream contentStream = new ContentStream();
            contentStream.FileName = name;
            contentStream.MimeType = MimeType.GetMIMEType(name);
            contentStream.Length = content.Length;
            contentStream.Stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

            return folder.CreateDocument(properties, contentStream, null);
        }

        protected IFolder CreateFolder(IFolder folder, string name)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, name);
            properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");

            return folder.CreateFolder(properties);
        }

        protected IDocument CopyDocument(IFolder folder, IDocument source, string name)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, name);
            properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

            return folder.CreateDocumentFromSource(source, properties, null);
        }

        protected IDocument RenameDocument(IDocument source, string name)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, name);

            return (IDocument)source.UpdateProperties(properties);
        }

        protected IFolder RenameFolder(IFolder source, string name)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, name);

            return (IFolder)source.UpdateProperties(properties);
        }

        protected void CreateHeavyFolder(string root)
        {
            for (int iFolder = 0; iFolder < HEAVY_FACTOR; ++iFolder)
            {
                string folder = Path.Combine(root, iFolder.ToString());
                Directory.CreateDirectory(folder);
                for (int iFile = 0; iFile < HEAVY_FACTOR; ++iFile)
                {
                    string file = Path.Combine(folder, iFile.ToString());
                    using (Stream stream = File.OpenWrite(file))
                    {
                        byte[] content = new byte[HEAVY_FILE_SIZE];
                        for (int i = 0; i < HEAVY_FILE_SIZE; ++i)
                        {
                            content[i] = (byte)('A' + iFile % 10);
                        }
                        stream.Write(content, 0, content.Length);
                    }
                }
            }
        }

        protected bool CheckHeavyFolder(string root)
        {
            for (int iFolder = 0; iFolder < HEAVY_FACTOR; ++iFolder)
            {
                string folder = Path.Combine(root, iFolder.ToString());
                if (!Directory.Exists(folder))
                {
                    return false;
                }
                for (int iFile = 0; iFile < HEAVY_FACTOR; ++iFile)
                {
                    string file = Path.Combine(folder, iFile.ToString());
                    FileInfo info = new FileInfo(file);
                    if (!info.Exists || info.Length != HEAVY_FILE_SIZE)
                    {
                        return false;
                    }
                    try
                    {
                        using (Stream stream = File.OpenRead(file))
                        {
                            byte[] content = new byte[HEAVY_FILE_SIZE];
                            stream.Read(content, 0, HEAVY_FILE_SIZE);
                            for (int i = 0; i < HEAVY_FILE_SIZE; ++i)
                            {
                                if (content[i] != (byte)('A' + iFile % 10))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        protected void CreateHeavyFolderRemote(IFolder root)
        {
            for (int iFolder = 0; iFolder < HEAVY_FACTOR; ++iFolder)
            {
                IFolder folder = CreateFolder(root, iFolder.ToString());
                for (int iFile = 0; iFile < HEAVY_FACTOR; ++iFile)
                {
                    string content = new string((char)('A' + iFile % 10), HEAVY_FILE_SIZE);
                    CreateDocument(folder, iFile.ToString(), content);
                }
            }
        }


        /// <summary>
        /// Creates or modifies a binary file.
        /// </summary>
        /// <returns>
        /// Path of the created or modified binary file
        /// </returns>
        /// <param name='file'>
        /// File path
        /// </param>
        /// <param name='length'>
        /// Length (default is 1024)
        /// </param>
        private string createOrModifyBinaryFile(string file, int length = 1024)
        {
            using (Stream stream = File.Open(file, FileMode.Create))
            {
                byte[] content = new byte[length];
                stream.Write(content, 0, content.Length);
            }
            return file;
        }

        /// <summary>
        /// Sync and wait, several times until a particular condition is satisfied.
        /// </summary>
        /// <returns>
        /// True if the condition got satisfied, false if the condition did not get satisfied within the time limit.
        /// </returns>
        /// <param name='synchronizedFolder'>
        /// Folder to synchronize.
        /// </param>
        /// <param name='checkStop'>
        /// If returns <c>true</c> wait returns true, otherwise a retry will be executed until reaching maxTries.
        /// </param>
        /// <param name='maxTries'>
        /// Number of retries, until false is returned, if checkStop could not be true.
        /// </param>
        /// <param name='pollInterval'>
        /// Sleep interval duration in miliseconds between synchronization calls.
        /// </param>
        public static bool SyncAndWaitUntilCondition(SynchronizedFolder synchronizedFolder, Func<bool> checkStop, int maxTries = 4, int pollInterval = 5000)
        {
            int i = 0;
            while (i < maxTries)
            {
                try
                {
                    synchronizedFolder.Sync();
                }
                catch (DotCMIS.Exceptions.CmisRuntimeException e)
                {
                    Console.WriteLine("{0} Exception caught and swallowed, retry.", e);
                    System.Threading.Thread.Sleep(pollInterval);
                    continue;
                }
                if (checkStop())
                    return true;
                Console.WriteLine(String.Format("Retry Sync in {0}ms", pollInterval));
                System.Threading.Thread.Sleep(pollInterval);
                i++;
            }
            Console.WriteLine("Sync call was not successful");
            return false;
        }

        /// <summary>
        /// Waits until a particular condition (expressed as checkStop) is true, or waiting limit is exceeded.
        /// </summary>
        /// <returns>
        /// True if the condition got satisfied, false if the condition did not get satisfied within the time limit.
        /// </returns>
        /// <param name='condition'>
        /// Function that checks for a particular condition, which is expected to become <c>true</c>.
        /// </param>
        /// <param name='timeLimit'>
        /// If this time limit is reached, <c>false</c> will be returned.
        /// </param>
        /// <param name='checkInterval'>
        /// Sleep duration between two checks of the condition.
        /// </param>
        public static bool WaitUntilCondition(Func<bool> condition, int timeLimit = 10000, int checkInterval = 1000)
        {
            while (timeLimit > 0)
            {
                System.Threading.Thread.Sleep(checkInterval);
                timeLimit -= checkInterval;
                if (condition())
                {
                    return true;
                }
                Console.WriteLine(String.Format("Sleeping for {0} ms", checkInterval));
            }
            Console.WriteLine("Wait limit exceeded, giving up");
            return false;
        }
    }
}
