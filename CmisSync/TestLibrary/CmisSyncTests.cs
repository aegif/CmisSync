using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data.Impl;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

/**
 * Unit Tests for CmisSync.
 * 
 * To use them, first create a JSON file containing the credentials/parameters to your CMIS server(s)
 * Put it in TestLibrary/test-servers.json and use this format:
[
    [
        "unittest1",
        "/mylocalpath",
        "/myremotepath",
        "http://example.com/p8cmis/resources/Service",
        "myuser",
        "mypassword",
        "repository987080"
    ],
    [
        "unittest2",
        "/mylocalpath",
        "/myremotepath",
        "http://example.org:8080/Nemaki/cmis",
        "myuser",
        "mypassword",
        "repo3"
    ]
]
 */
namespace TestLibrary
{
    using System.Collections;
    using CmisSync.Lib;
    using CmisSync.Lib.Cmis;
    using CmisSync.Lib.Sync;
    using log4net.Appender;
    using NUnit.Framework;
    using CmisSync.Auth;
    using CmisSync.Lib.Database;

    /// <summary></summary>
    [TestFixture]
    public class CmisSyncTests
    {
        private readonly string CMISSYNCDIR = ConfigManager.CurrentConfig.FoldersPath;
        private readonly int HeavyNumber = 10;
        private readonly int HeavyFileSize = 1024;

        public CmisSyncTests()
        {
        }

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
            foreach( string file in Directory.GetFiles(CMISSYNCDIR)) {
                if(file.EndsWith(".cmissync"))
                {
                    File.Delete(file);
                }
                    
            }
        }

        /// <summary></summary>
        public static IEnumerable<object[]> TestServers
        {
            get
            {
                string path = "../../test-servers.json";
                bool exists = File.Exists(path);

                if (!exists)
                {
                    path = "../CmisSync/TestLibrary/test-servers.json";
                }

                return JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText(path));
            }
        }

        /// <summary></summary>
        public static IEnumerable<object[]> TestServersFuzzy
        {
            get
            {
                string path = "../../test-servers-fuzzy.json";
                bool exists = File.Exists(path);

                if (!exists)
                {
                    path = "../CmisSync/TestLibrary/test-servers-fuzzy.json";
                }

                return JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText(path));
            }
        }

        /// <summary></summary>
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
        }

        private void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private void CleanDirectory(string path)
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
                    if (file.Name.EndsWith(".sync"))
                    {
                        continue;
                    }

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
        }

        private ISession CreateSession(RepoInfo repoInfo)
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

        public IDocument CreateDocument(IFolder folder, string name, string content)
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

        public IFolder CreateFolder(IFolder folder, string name)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, name);
            properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");

            return folder.CreateFolder(properties);
        }

        public IDocument CopyDocument(IFolder folder, IDocument source, string name)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, name);
            properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

            return folder.CreateDocumentFromSource(source, properties, null);
        }

        public IDocument RenameDocument(IDocument source, string name)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, name);

            return (IDocument)source.UpdateProperties(properties);
        }

        public IFolder RenameFolder(IFolder source, string name)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, name);

            return (IFolder)source.UpdateProperties(properties);
        }

        public void CreateHeavyFolder(string root)
        {
            for (int iFolder = 0; iFolder < HeavyNumber; ++iFolder)
            {
                string folder = Path.Combine(root, iFolder.ToString());
                Directory.CreateDirectory(folder);
                for (int iFile = 0; iFile < HeavyNumber; ++iFile)
                {
                    string file = Path.Combine(folder, iFile.ToString());
                    using (Stream stream = File.OpenWrite(file))
                    {
                        byte[] content = new byte[HeavyFileSize];
                        for (int i = 0; i < HeavyFileSize; ++i)
                        {
                            content[i] = (byte)('A'+iFile%10);
                        }
                        stream.Write(content, 0, content.Length);
                    }
                }
            }
        }

        public bool CheckHeavyFolder(string root)
        {
            for (int iFolder = 0; iFolder < HeavyNumber; ++iFolder)
            {
                string folder = Path.Combine(root, iFolder.ToString());
                if (!Directory.Exists(folder))
                {
                    return false;
                }
                for (int iFile = 0; iFile < HeavyNumber; ++iFile)
                {
                    string file = Path.Combine(folder, iFile.ToString());
                    FileInfo info = new FileInfo(file);
                    if(!info.Exists || info.Length != HeavyFileSize)
                    {
                        return false;
                    }
                    try
                    {
                        using (Stream stream = File.OpenRead(file))
                        {
                            byte[] content = new byte[HeavyFileSize];
                            stream.Read(content, 0, HeavyFileSize);
                            for (int i = 0; i < HeavyFileSize; ++i)
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

        public void CreateHeavyFolderRemote(IFolder root)
        {
            for (int iFolder = 0; iFolder < HeavyNumber; ++iFolder)
            {
                IFolder folder = CreateFolder(root, iFolder.ToString());
                for (int iFile = 0; iFile < HeavyNumber; ++iFile)
                {
                    string content = new string((char)('A' + iFile % 10), HeavyFileSize);
                    CreateDocument(folder, iFile.ToString(), content);
                }
            }
        }

        // /////////////////////////// TESTS ///////////////////////////

        [Test, Category("Fast")]
        public void Placebo()
        {
            Assert.AreEqual(4, 2 + 2);
        }

        [Test, Category("Fast")]
        public void TestCrypto()
        {
            String[] test_pws = { "", "test", "Whatever", "Something to try" };
            foreach (String pass in test_pws)
            {
                String crypted = Crypto.Obfuscate(pass);
                Assert.AreEqual(Crypto.Deobfuscate(crypted), pass);
            }
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void GetRepositories(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            ServerCredentials credentials = new ServerCredentials()
            {
                Address = new Uri(url),
                UserName = user,
                Password = password
            };
            Dictionary<string, string> repos = CmisUtils.GetRepositories(credentials);
            foreach (KeyValuePair<string, string> pair in repos)
            {
                Console.WriteLine(pair.Key + " : " + pair.Value);
            }
            Assert.NotNull(repos);
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void Sync(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    cmis, activityListener))
                {
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }

        /*[Test, TestCaseSource("TestServers"), Category("Slow")]
        public void ClientSideSmallFileAddition(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    cmis,
                    activityListener))
                using (Watcher watcher = new Watcher(localDirectory))
                {
                    synchronizedFolder.resetFailedOperationsCounter();
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    // Create random small file.
                    string filename = LocalFilesystemActivityGenerator.GetNextFileName();
                    string remoteFilePath = (remoteFolderPath + "/" + filename).Replace("//", "/");
                    watcher.EnableRaisingEvents = true;
                    LocalFilesystemActivityGenerator.CreateRandomFile(localDirectory, 3);
                    WatcherTest.WaitWatcher((int)repoInfo.PollInterval, watcher, 1);
                    // Sync again.
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder,delegate {
                        try{
                            IDocument d = (IDocument)CreateSession(repoInfo).GetObjectByPath(remoteFilePath);
                            if(d!=null)
                                return true;
                        }catch(Exception)
                        {return false;}
                        return false;
                    }));
                    Console.WriteLine("Second sync done.");
                    // Check that file is present server-side.
                    IDocument doc = (IDocument)CreateSession(repoInfo).GetObjectByPath(remoteFilePath);
                    Assert.NotNull(doc);
                    Assert.AreEqual(filename, doc.ContentStreamFileName);
                    Assert.AreEqual(filename, doc.Name);

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void ClientSideBigFileAddition(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
                using (Watcher watcher = new Watcher(localDirectory))
                {
                    synchronizedFolder.resetFailedOperationsCounter();
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    // Create random big file.
                    string filename = LocalFilesystemActivityGenerator.GetNextFileName();
                    string remoteFilePath = (remoteFolderPath + "/" + filename).Replace("//", "/");
                    watcher.EnableRaisingEvents = true;
                    LocalFilesystemActivityGenerator.CreateRandomFile(localDirectory, 1000); // 1 MB ... no that big to not load servers too much.
                    WatcherTest.WaitWatcher((int)repoInfo.PollInterval, watcher, 1);
                    // Sync again.
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder,delegate {
                        try{
                            IDocument d = (IDocument)CreateSession(repoInfo).GetObjectByPath(remoteFilePath);
                            if(d!=null)
                                return true;
                        }catch(Exception)
                        {return false;}
                        return false;
                    }));
                    Console.WriteLine("Second sync done.");
                    // Check that file is present server-side.
                    IDocument doc = (IDocument)CreateSession(repoInfo).GetObjectByPath(remoteFilePath);
                    Assert.NotNull(doc);
                    Assert.AreEqual(filename, doc.ContentStreamFileName);
                    Assert.AreEqual(filename, doc.Name);

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }*/

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void ResumeBigFile(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            string canonical_name2 = canonical_name + ".BigFile";
            string localDirectory2 = Path.Combine(CMISSYNCDIR, canonical_name2);
            CleanDirectory(localDirectory);
            CleanDirectory(localDirectory2);
            Console.WriteLine("Synced to clean state.");

            string filename = "ResumeBigFile.File";
            int fileSizeInMB = 10;
            string file = Path.Combine(localDirectory, filename);
            string file2 = Path.Combine(localDirectory2, filename);

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            repoInfo.ChunkSize = 1024 * 1024;
            RepoInfo repoInfo2 = new RepoInfo(
                    canonical_name2,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            repoInfo2.ChunkSize = 1024 * 1024;

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
            {
                synchronizedFolder.resetFailedOperationsCounter();
                synchronizedFolder.Sync();
                CleanAll(localDirectory);
                WatcherTest.WaitWatcher();
                synchronizedFolder.Sync();
                Console.WriteLine("Synced to clean state.");
            }

            //  create file
            byte[] data = new byte[1024 * 1024];
            new Random().NextBytes(data);
            using (FileStream stream = File.OpenWrite(file))
            {
                for (int i = 0; i < fileSizeInMB; i++)
                {
                    stream.Write(data, 0, data.Length);
                }
            }
            string remoteFilePath = (remoteFolderPath + "/" + filename).Replace("//", "/");

            Console.WriteLine(String.Format("Upload big file size: {0}MB", fileSizeInMB));
            for (int currentFileSizeInMB = 0, retry = 0; currentFileSizeInMB < fileSizeInMB && retry < 100; ++retry)
            {
                using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
                using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
                {
                    //  disable the chunk upload
                    //synchronizedFolder.SyncInBackground();
                    //System.Threading.Thread.Sleep(1000);
                    synchronizedFolder.Sync();
                }

                try
                {
                    IDocument doc = (IDocument)CreateSession(repoInfo).GetObjectByPath(remoteFilePath);
                    long fileSize = doc.ContentStreamLength ?? 0;
                    Assert.IsTrue(0 == fileSize % (1024 * 1024));
                    currentFileSizeInMB = (int)(fileSize / 1024 / 1024);
                }
                catch (Exception)
                {
                }
                Console.WriteLine("Upload big file, current size: {0}MB", currentFileSizeInMB);
            }

            Console.WriteLine(String.Format("Download big file size: {0}MB", fileSizeInMB));
            for (int currentFileSizeInMB = 0, retry = 0; currentFileSizeInMB < fileSizeInMB && retry < 100; ++retry)
            {
                using (CmisRepo cmis2 = new CmisRepo(repoInfo2, activityListener))
                using (CmisRepo.SynchronizedFolder synchronizedFolder2 =
                    new CmisRepo.SynchronizedFolder(repoInfo2, cmis2, activityListener))
                {
                    synchronizedFolder2.SyncInBackground(true);
                    System.Threading.Thread.Sleep(1000);
                }

                string file2Tmp = file2 + ".sync";
                FileInfo info = new FileInfo(file2);
                FileInfo infoTmp = new FileInfo(file2Tmp);
                if (infoTmp.Exists)
                {
                    currentFileSizeInMB = (int)(infoTmp.Length / 1024 / 1024);
                }
                else if (info.Exists)
                {
                    currentFileSizeInMB = (int)(info.Length / 1024 / 1024);
                }
                Console.WriteLine("Download big file, current size: {0}MB", currentFileSizeInMB);
            }

            string checksum1 = Database.Checksum(file);
            string checksum2 = Database.Checksum(file2);
            Assert.IsTrue(checksum1 == checksum2);

            using (CmisRepo cmis2 = new CmisRepo(repoInfo2, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder2 =
                    new CmisRepo.SynchronizedFolder(repoInfo2, cmis2, activityListener))
            {
                // Clean.
                Console.WriteLine("Clean all.");
                Clean(localDirectory2, synchronizedFolder2);
            }
        }

        // Goal: Make sure that CmisSync works for uploading modified files.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncUploads(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
                        // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
                using (Watcher watcher = new Watcher(localDirectory))
                {
                    // Clear local and remote folder
                    synchronizedFolder.Sync();
                    CleanAll(localDirectory);
                    WatcherTest.WaitWatcher();
                    synchronizedFolder.Sync();
                    // Create a list of file names
                    List<string> files = new List<string>();
                    for(int i = 1 ; i <= 10; i++) {
                        string filename =  String.Format("file{0}.bin", i.ToString());
                        files.Add(filename);
                    }
                    // Sizes of the files
                    int[] sizes = {1024, 2048, 324, 3452, 0, 43256};
                    // Create and modify all files and start syncing to ensure that any local modification is uploaded correctly
                    foreach ( int length in sizes )
                    {
                        foreach(string filename in files)
                        {
                            createOrModifyBinaryFile(Path.Combine(localDirectory, filename), length);
                        }
                        // Ensure, all local files are available
                        Assert.AreEqual(files.Count, Directory.GetFiles(localDirectory).Length);
                        // Sync until all remote files do have got the same content length like the local one
                        Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                            foreach(string filename in files)
                            {
                                try{
                                    string remoteFilePath = (remoteFolderPath + "/" + filename).Replace("//", "/");
                                    IDocument d = (IDocument)CreateSession(repoInfo).GetObjectByPath(remoteFilePath);
                                    if(d == null || d.ContentStreamLength != length)
                                        return false;
                                }catch(Exception)
                                {return false;}
                            }
                            return true;
                        }));
                        // Check, if all local files are available
                        Assert.AreEqual(files.Count, Directory.GetFiles(localDirectory).Length);
                    }
                }
            }
        }

        /// <summary>
        /// Creates or modifies binary file.
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

        // Goal: Make sure that CmisSync works for remote changes.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncRemote(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
                using (Watcher watcher = new Watcher(localDirectory))
                {
                    synchronizedFolder.Sync();
                    CleanAll(localDirectory);
                    WatcherTest.WaitWatcher();
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    ISession session = CreateSession(repoInfo);
                    IFolder folder = (IFolder)session.GetObjectByPath(remoteFolderPath);

                    string name1 = "SyncChangeLog.1";
                    string path1 = Path.Combine(localDirectory, name1);

                    string name2 = "SyncChangeLog.2";
                    string path2 = Path.Combine(localDirectory, name2);

                    //  create document
                    Console.WriteLine(" Remote create file");
                    Assert.IsFalse(File.Exists(path1));
                    IDocument doc1 = CreateDocument(folder, name1, "SyncChangeLog");
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return File.Exists(path1);
                    }));
                    Assert.IsTrue(File.Exists(path1));

                    //TODO: AtomPub does not support copy
                    ////  copy document
                    //Console.WriteLine(" Remote copy file");
                    //Assert.IsFalse(File.Exists(path2));
                    //IDocument doc2 = CopyDocument(folder, doc1, name2);
                    //synchronizedFolder.Sync();
                    //Assert.IsTrue(File.Exists(path2));

                    //  rename document
                    Console.WriteLine(" Remote rename file");
                    Assert.IsTrue(File.Exists(path1));
                    Assert.IsFalse(File.Exists(path2));
                    IDocument doc2 = RenameDocument(doc1, name2);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return !File.Exists(path1) && File.Exists(path2);
                    }));
                    Assert.IsFalse(File.Exists(path1));
                    Assert.IsTrue(File.Exists(path2));

                    //  create folder
                    Console.WriteLine(" Remote create folder");
                    Assert.IsFalse(Directory.Exists(path1));
                    IFolder folder1 = CreateFolder(folder, name1);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return Directory.Exists(path1);
                    }));
                    Assert.IsTrue(Directory.Exists(path1));

                    //  move document
                    Console.WriteLine(" Remote move file");
                    string filename = Path.Combine(path1, name2);
                    Assert.IsFalse(File.Exists(filename));
                    doc2.Move(folder, folder1);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return File.Exists(filename);
                    }));

                    //  delete document
                    Console.WriteLine(" Remote delete file");
                    Assert.IsTrue(File.Exists(filename));
                    doc2.DeleteAllVersions();
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return !File.Exists(filename);
                    }));
                    Assert.IsFalse(File.Exists(filename));

                    //  rename folder
                    Console.WriteLine(" Remote rename folder");
                    Assert.IsTrue(Directory.Exists(path1));
                    Assert.IsFalse(Directory.Exists(path2));
                    IFolder folder2 = RenameFolder(folder1, name2);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return !Directory.Exists(path1) && Directory.Exists(path2);
                    }));
                    Assert.IsFalse(Directory.Exists(path1));
                    Assert.IsTrue(Directory.Exists(path2));

                    //  move folder
                    Console.WriteLine(" Remote move folder");
                    Assert.IsFalse(Directory.Exists(path1));
                    folder1 = CreateFolder(folder, name1);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return Directory.Exists(path1) && !Directory.Exists(Path.Combine(path2, name1));
                    }));
                    Assert.IsTrue(Directory.Exists(path1));
                    Assert.IsFalse(Directory.Exists(Path.Combine(path2, name1)));
                    folder1.Move(folder, folder2);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return !Directory.Exists(path1) && Directory.Exists(Path.Combine(path2, name1));
                    }));
                    Assert.IsFalse(Directory.Exists(path1));
                    Assert.IsTrue(Directory.Exists(Path.Combine(path2, name1)));

                    //  move folder with sub folder and sub file
                    Console.WriteLine(" Remote move folder with subfolder and subfile");
                    Assert.IsFalse(File.Exists(Path.Combine(path2, name1, name1)));
                    Assert.IsFalse(Directory.Exists(Path.Combine(path2, name1, name2)));
                    CreateDocument(folder1, name1, "SyncChangeLog");
                    CreateFolder(folder1, name2);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return File.Exists(Path.Combine(path2, name1, name1)) && Directory.Exists(Path.Combine(path2, name1, name2));
                    }));
                    Assert.IsTrue(File.Exists(Path.Combine(path2, name1, name1)));
                    Assert.IsTrue(Directory.Exists(Path.Combine(path2, name1, name2)));
                    folder1.Move(folder2, folder);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return File.Exists(Path.Combine(path1, name1)) && Directory.Exists(Path.Combine(path1, name2));
                    }));
                    Assert.IsTrue(File.Exists(Path.Combine(path1, name1)));
                    Assert.IsTrue(Directory.Exists(Path.Combine(path1, name2)));

                    //  delete folder tree
                    Console.WriteLine(" Remote delete folder tree");
                    Assert.IsTrue(Directory.Exists(path1));
                    folder1.DeleteTree(true, null, true);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return !Directory.Exists(path1);
                    }, 20));
                    Assert.IsFalse(Directory.Exists(path1));
                    Assert.IsTrue(Directory.Exists(path2));
                    folder2.DeleteTree(true, null, true);
                    WaitUntilSyncIsDone(synchronizedFolder, delegate {
                        return !Directory.Exists(path2);
                    });
                    if(Directory.Exists(path2))
                        synchronizedFolder.ForceFullSync();
                    Assert.IsFalse(Directory.Exists(path2));

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }

        // Goal: Make sure that CmisSync works for remote heavy folder changes.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void SyncRemoteHeavyFolder(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
                using (Watcher watcher = new Watcher(localDirectory))
                {
                    synchronizedFolder.Sync();
                    CleanAll(localDirectory);
                    WatcherTest.WaitWatcher();
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    ISession session = CreateSession(repoInfo);
                    IFolder folder = (IFolder)session.GetObjectByPath(remoteFolderPath);

                    string name1 = "SyncChangeLog.1";
                    string path1 = Path.Combine(localDirectory, name1);

                    string name2 = "SyncChangeLog.2";
                    string path2 = Path.Combine(localDirectory, name2);

                    //  create heavy folder
                    Console.WriteLine(" Remote create heavy folder");
                    Assert.IsFalse(Directory.Exists(path1));
                    IFolder folder1 = CreateFolder(folder, name1);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate
                    {
                        return Directory.Exists(path1);
                    }));
                    Assert.IsTrue(Directory.Exists(path1));
                    CreateHeavyFolderRemote(folder1);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate
                    {
                        return CheckHeavyFolder(path1);
                    }));
                    Assert.IsTrue(CheckHeavyFolder(path1));

                    //  rename heavy folder
                    Console.WriteLine(" Remote rename heavy folder");
                    IFolder folder2 = RenameFolder(folder1, name2);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate
                    {
                        return CheckHeavyFolder(path2);
                    }));
                    Assert.IsTrue(CheckHeavyFolder(path2));

                    //  move heavy folder
                    Console.WriteLine(" Remote move heavy folder");
                    folder1 = CreateFolder(folder, name1);
                    folder2.Move(folder, folder1);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate
                    {
                        return CheckHeavyFolder(Path.Combine(path1,name2));
                    }));
                    Assert.IsTrue(CheckHeavyFolder(Path.Combine(path1, name2)));

                    //  delete heavy folder
                    Console.WriteLine(" Remote delete heavy folder");
                    folder1.DeleteTree(true, null, true);
                    Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate
                    {
                        return !Directory.Exists(path1);
                    }));
                    Assert.IsFalse(Directory.Exists(path1));

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }
        
        // Goal: Make sure that CmisSync works for equality.
        /*[Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncRenamedFiles(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            string canonical_name2 = canonical_name + ".equality";
            string localDirectory2 = Path.Combine(CMISSYNCDIR, canonical_name2);
            CleanDirectory(localDirectory);
            CleanDirectory(localDirectory2);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            RepoInfo repoInfo2 = new RepoInfo(
                    canonical_name2,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
            using (CmisRepo cmis2 = new CmisRepo(repoInfo2, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder2 =
                    new CmisRepo.SynchronizedFolder(repoInfo2, cmis2, activityListener))
            using (Watcher watcher = new Watcher(localDirectory))
            using (Watcher watcher2 = new Watcher(localDirectory2))
            {
                synchronizedFolder.resetFailedOperationsCounter();
                synchronizedFolder2.resetFailedOperationsCounter();
                synchronizedFolder.Sync();
                synchronizedFolder2.Sync();
                CleanAll(localDirectory);
                CleanAll(localDirectory2);
                WatcherTest.WaitWatcher();
                synchronizedFolder.Sync();
                synchronizedFolder2.Sync();
                Console.WriteLine("Synced to clean state.");

                //  create file
                // remote filename = /SyncEquality.File
                Console.WriteLine("create file test.");
                string filename = "SyncRename.File";
                string file = Path.Combine(localDirectory, filename);
                string file2 = Path.Combine(localDirectory2, filename);
                int localFilesCount = 0;
                int localFilesCount2 = 0;
                Assert.IsFalse(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                watcher.EnableRaisingEvents = true;
                int length = 1024;
                using (Stream stream = File.OpenWrite(file))
                {
                    byte[] content = new byte[length];
                    stream.Write(content, 0, content.Length);
                }
                Assert.IsTrue(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                WatcherTest.WaitWatcher((int)repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                Assert.IsTrue(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate {
                    synchronizedFolder.Sync();
                    FileInfo info = new FileInfo(file2);
                    return info.Exists && info.Length == length;
                }), String.Format("The new file \"{0}\"should exist and it should have got the length \"{1}\"", file2, length));
                Assert.IsTrue(File.Exists(file));
                Assert.IsTrue(File.Exists(file2));
                localFilesCount = Directory.GetFiles(localDirectory).Length;
                localFilesCount2 = Directory.GetFiles(localDirectory2).Length;
                Assert.AreEqual(localFilesCount, localFilesCount2, String.Format("Both local folder should contain one file before renaming a file, but there are {0} and {1} files", localFilesCount, localFilesCount2));
                // Only one file should exist
                Assert.AreEqual(1, localFilesCount2, String.Format("There should exist only one file in the local folder before renaming, but there are {0}", localFilesCount2));
                string renamedfilename = "SyncRenameTarget.File";
                string renamedfile = Path.Combine(localDirectory, renamedfilename);
                string renamedfile2 = Path.Combine(localDirectory2, renamedfilename);
                watcher.EnableRaisingEvents = true;
                File.Move(file, renamedfile);
                WatcherTest.WaitWatcher((int) repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                Assert.IsTrue(File.Exists(renamedfile));
                Assert.IsTrue(!File.Exists(renamedfile2));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate {
                    synchronizedFolder.Sync();
                    FileInfo info = new FileInfo(renamedfile2);
                    return info.Exists && info.Length == length;
                }));
                localFilesCount = Directory.GetFiles(localDirectory).Length;
                localFilesCount2 = Directory.GetFiles(localDirectory2).Length;
                Assert.AreEqual(localFilesCount, localFilesCount2, String.Format("Both local folder should contain one file, but there are {0} and {1} files", localFilesCount, localFilesCount2));
                // Only one file should exist
                Assert.AreEqual(1, localFilesCount2, String.Format("There should exist only one file in the local folder, but there are {0}", localFilesCount2));
            }
        }

        // Goal: Make sure that CmisSync works for equality.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncEquality(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            string canonical_name2 = canonical_name + ".equality";
            string localDirectory2 = Path.Combine(CMISSYNCDIR, canonical_name2);
            CleanDirectory(localDirectory);
            CleanDirectory(localDirectory2);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            RepoInfo repoInfo2 = new RepoInfo(
                    canonical_name2,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
            using (CmisRepo cmis2 = new CmisRepo(repoInfo2, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder2 =
                    new CmisRepo.SynchronizedFolder(repoInfo2, cmis2, activityListener))
            using (Watcher watcher = new Watcher(localDirectory))
            using (Watcher watcher2 = new Watcher(localDirectory2))
            {
                synchronizedFolder.resetFailedOperationsCounter();
                synchronizedFolder2.resetFailedOperationsCounter();
                synchronizedFolder.Sync();
                synchronizedFolder2.Sync();
                CleanAll(localDirectory);
                CleanAll(localDirectory2);
                WatcherTest.WaitWatcher();
                synchronizedFolder.Sync();
                synchronizedFolder2.Sync();
                Console.WriteLine("Synced to clean state.");

                //  create file
                // remote filename = /SyncEquality.File
                Console.WriteLine("create file test.");
                string filename = "SyncEquality.File";
                string file = Path.Combine(localDirectory, filename);
                string file2 = Path.Combine(localDirectory2, filename);
                Assert.IsFalse(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                watcher.EnableRaisingEvents = true;
                int length = 1024;
                using (Stream stream = File.OpenWrite(file))
                {
                    byte[] content = new byte[length];
                    stream.Write(content, 0, content.Length);
                }
                Assert.IsTrue(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                WatcherTest.WaitWatcher((int)repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                synchronizedFolder.Sync();
                Assert.IsTrue(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate {
                        FileInfo info = new FileInfo(file2);
                        return info.Exists && info.Length == length;
                    }));
                Assert.IsTrue(File.Exists(file));
                Assert.IsTrue(File.Exists(file2));

                //  create folder
                // remote folder name = /SyncEquality.Folder
                Console.WriteLine("create folder test.");
                string foldername = "SyncEquality.Folder";
                string folder = Path.Combine(localDirectory, foldername);
                string folder2 = Path.Combine(localDirectory2, foldername);
                Assert.IsFalse(Directory.Exists(folder));
                Assert.IsFalse(Directory.Exists(folder2));
                watcher.EnableRaisingEvents = true;
                Directory.CreateDirectory(folder);
                Assert.IsTrue(Directory.Exists(folder));
                Assert.IsFalse(Directory.Exists(folder2));
                WatcherTest.WaitWatcher((int)repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                synchronizedFolder.Sync();
                Assert.IsTrue(Directory.Exists(folder));
                Assert.IsFalse(Directory.Exists(folder2));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate {
                        return Directory.Exists(folder2);
                    }));
                Assert.IsTrue(Directory.Exists(folder));
                Assert.IsTrue(Directory.Exists(folder2));

                //  move file
                // /SyncEquality.File -> /SyncEquality.Folder/SyncEquality.File
                Console.WriteLine("move file test.");
                string source = file;
                file = Path.Combine(folder, filename);
                file2 = Path.Combine(folder2, filename);
                Assert.IsFalse(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                watcher.EnableRaisingEvents = true;
                File.Move(source, file);
                Assert.IsTrue(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                WatcherTest.WaitWatcher((int)repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                synchronizedFolder.Sync();
                Assert.IsTrue(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate {
                        return File.Exists(file2);
                    }));
                Assert.IsTrue(File.Exists(file));
                Assert.IsTrue(File.Exists(file2));

                //  move folder
                // create a folder as move target = /SyncEquality.Folder.2/
                Console.WriteLine("move folder test.");
                string foldername2 = "SyncEquality.Folder.2";
                folder = Path.Combine(localDirectory, foldername2);
                folder2 = Path.Combine(localDirectory2, foldername2);
                Assert.IsFalse(Directory.Exists(folder));
                Assert.IsFalse(Directory.Exists(folder2));
                watcher.EnableRaisingEvents = true;
                Directory.CreateDirectory(folder);
                Assert.IsTrue(Directory.Exists(folder));
                Assert.IsFalse(Directory.Exists(folder2));
                WatcherTest.WaitWatcher((int)repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                synchronizedFolder.Sync();
                Assert.IsTrue(Directory.Exists(folder));
                Assert.IsFalse(Directory.Exists(folder2));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate {
                        return Directory.Exists(folder2);
                    }));
                Assert.IsTrue(Directory.Exists(folder));
                Assert.IsTrue(Directory.Exists(folder2));
                //move to the created folder
                // moved folder = /SyncEquality.Folder/
                // target folder = /SyncEquality.Folder.2/
                // result = /SyncEquality.Folder.2/SyncEquality.Folder/
                file = Path.Combine(folder, foldername, filename);
                file2 = Path.Combine(folder2, foldername, filename);
                Assert.IsFalse(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                watcher.EnableRaisingEvents = true;
                Directory.Move(
                    Path.Combine(localDirectory, foldername),
                    Path.Combine(folder, foldername));
                Assert.IsTrue(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                WatcherTest.WaitWatcher((int)repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                synchronizedFolder.Sync();
                Assert.IsTrue(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate {
                        return File.Exists(file2);
                    }));
                Assert.IsTrue(File.Exists(file));
                Assert.IsTrue(File.Exists(file2));

                //change filecontent
                // remote file path = /SyncEquality.Folder.2/SyncEquality.Folder/SyncEquality.File
                Console.WriteLine("update file test.");
                int filecount = Directory.GetFiles(Path.Combine(folder, foldername)).Count();
                int filecount2 = Directory.GetFiles(Path.Combine(folder2, foldername)).Count();
                length = 2048;
                Assert.IsTrue(filecount == filecount2);
                Assert.IsTrue(filecount == 1);
                Console.WriteLine(" filecontent size = "+ length.ToString());
                watcher.EnableRaisingEvents = true;
                using (Stream stream = File.OpenWrite(file))
                {
                    byte[] content = new byte[length];
                    stream.Write(content, 0, content.Length);
                }
                WatcherTest.WaitWatcher((int)repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                synchronizedFolder.Sync();
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate {
                    if(filecount2 == Directory.GetFiles(Path.Combine(folder2, foldername)).Count())
                    {
                        FileInfo info = new FileInfo(file2);
                        return info.Exists && info.Length == length;
                    } else {
                        return false;
                    }
                    }, 20));
                Assert.AreEqual(filecount, Directory.GetFiles(Path.Combine(folder, foldername)).Count());
                Assert.AreEqual(filecount2, Directory.GetFiles(Path.Combine(folder2, foldername)).Count());
                Console.WriteLine(" checking file content equality");
                using (Stream stream = File.OpenRead(file))
                using (Stream stream2 = File.OpenRead(file2))
                {
                    Assert.IsTrue(stream.Length == stream2.Length && stream2.Length == length);
                    byte[] content = new byte[length];
                    byte[] content2 = new byte[length];
                    stream.Read(content,0,length);
                    stream.Read(content2,0,length);
                    for(int i = 0; i < length; i++)
                        Assert.AreEqual(content[i], content2[i]);
                }

                //  delete file
                // remote file path = /SyncEquality.Folder.2/SyncEquality.Folder/SyncEquality.File
                Console.WriteLine("delete file test.");
                Assert.IsTrue(File.Exists(file));
                Assert.IsTrue(File.Exists(file2));
                watcher.EnableRaisingEvents = true;
                File.Delete(file);
                Assert.IsFalse(File.Exists(file));
                Assert.IsTrue(File.Exists(file2));
                WatcherTest.WaitWatcher((int)repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                    return WaitUntilSyncIsDone(synchronizedFolder2, delegate{
                        return !File.Exists(file) && !File.Exists(file2);
                    }, 1);
                    }, 20));
                Assert.IsFalse(File.Exists(file));
                Assert.IsFalse(File.Exists(file2));

                //  delete folder tree
                // delete remote folder = /SyncEquality.Folder.2/
                Console.WriteLine("delete folder tree test.");
                Assert.IsTrue(Directory.Exists(folder));
                Assert.IsTrue(Directory.Exists(folder2));
                watcher.EnableRaisingEvents = true;
                Directory.Delete(folder, true);
                Assert.IsFalse(Directory.Exists(folder));
                Assert.IsTrue(Directory.Exists(folder2));
                WatcherTest.WaitWatcher((int)repoInfo2.PollInterval, watcher, 1);
                watcher.EnableRaisingEvents = false;
                watcher.RemoveAll();
                synchronizedFolder.Sync();
                Assert.IsFalse(Directory.Exists(folder));
                Assert.IsTrue(Directory.Exists(folder2));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate {
                        return !Directory.Exists(folder2);
                    }, 20));
                Assert.IsFalse(Directory.Exists(folder));
                Assert.IsFalse(Directory.Exists(folder2));

                // Clean.
                Console.WriteLine("Clean all.");
                Clean(localDirectory, synchronizedFolder);
                Clean(localDirectory2, synchronizedFolder2);
            }
        }

        // Goal: Make sure that CmisSync works for empty files without creating conflict files.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncEmptyFileEquality(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            string canonical_name2 = canonical_name + ".equality";
            string localDirectory2 = Path.Combine(CMISSYNCDIR, canonical_name2);
            CleanDirectory(localDirectory);
            CleanDirectory(localDirectory2);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            RepoInfo repoInfo2 = new RepoInfo(
                    canonical_name2,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
            using (CmisRepo cmis2 = new CmisRepo(repoInfo2, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder2 =
                    new CmisRepo.SynchronizedFolder(repoInfo2, cmis2, activityListener))
            using (Watcher watcher = new Watcher(localDirectory))
            using (Watcher watcher2 = new Watcher(localDirectory2))
            {
                synchronizedFolder.resetFailedOperationsCounter();
                synchronizedFolder2.resetFailedOperationsCounter();
                synchronizedFolder.Sync();
                synchronizedFolder2.Sync();
                CleanAll(localDirectory);
                CleanAll(localDirectory2);
                WatcherTest.WaitWatcher();
                synchronizedFolder.Sync();
                synchronizedFolder2.Sync();
                Console.WriteLine("Synced to clean state.");
                string filename = "empty-file.bin";
                string file = Path.Combine(localDirectory, filename);
                string file2 = Path.Combine(localDirectory2, filename);
                watcher.EnableRaisingEvents = true;
                // Writing an empty file to the first local folder
                using(FileStream stream = File.Create(Path.Combine(localDirectory, filename))){
                    stream.Close();
                };
                WatcherTest.WaitWatcher((int)repoInfo.PollInterval, watcher, 1);
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate {
                    return WaitUntilSyncIsDone(synchronizedFolder2, delegate{
                        int files = Directory.GetFiles(localDirectory).Length;
                        int files2 = Directory.GetFiles(localDirectory2).Length;
                        Assert.LessOrEqual(files, 1, String.Format("There are more files ({0}) as has been created in the source repo", files));
                        Assert.LessOrEqual(files2, 1, String.Format("There are more files ({0}) as has been created in the target repo", files));
                        return File.Exists(file) && File.Exists(file2);
                    }, 1);
                    }, 20));
            }
        }*/

        // Goal: Make sure that CmisSync works for heavy folder.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void SyncEqualityHeavyFolder(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            string canonical_name2 = canonical_name + ".equality";
            string localDirectory2 = Path.Combine(CMISSYNCDIR, canonical_name2);
            CleanDirectory(localDirectory);
            CleanDirectory(localDirectory2);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            RepoInfo repoInfo2 = new RepoInfo(
                    canonical_name2,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
            using (CmisRepo cmis2 = new CmisRepo(repoInfo2, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder2 =
                    new CmisRepo.SynchronizedFolder(repoInfo2, cmis2, activityListener))
            using (Watcher watcher = new Watcher(localDirectory))
            using (Watcher watcher2 = new Watcher(localDirectory2))
            {
                synchronizedFolder.resetFailedOperationsCounter();
                synchronizedFolder2.resetFailedOperationsCounter();
                synchronizedFolder.Sync();
                synchronizedFolder2.Sync();
                CleanAll(localDirectory);
                CleanAll(localDirectory2);
                WatcherTest.WaitWatcher();
                synchronizedFolder.Sync();
                synchronizedFolder2.Sync();
                Console.WriteLine("Synced to clean state.");

                string oldname = "SyncEquality.Old";
                string newname = "SyncEquality.New";

                //  test heavy folder create
                Console.WriteLine(" Local create heavy folder");
                string oldfolder = Path.Combine(localDirectory, oldname);
                string oldfolder2 = Path.Combine(localDirectory2, oldname);
                Directory.CreateDirectory(oldfolder);
                CreateHeavyFolder(oldfolder);
                Assert.IsTrue(CheckHeavyFolder(oldfolder));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate
                {
                    synchronizedFolder.Sync();
                    return CheckHeavyFolder(oldfolder2);
                }, 10));
                Assert.IsTrue(CheckHeavyFolder(oldfolder2));

                //  test heavy folder rename
                Console.WriteLine(" Local rename heavy folder");
                string newfolder = Path.Combine(localDirectory, newname);
                string newfolder2 = Path.Combine(localDirectory2, newname);
                Directory.Move(oldfolder, newfolder);
                Assert.IsTrue(CheckHeavyFolder(newfolder));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate
                {
                    synchronizedFolder.Sync();
                    return CheckHeavyFolder(newfolder2);
                }, 10));
                Assert.IsTrue(CheckHeavyFolder(newfolder2));

                //  test heavy folder move
                Console.WriteLine(" Local move heavy folder");
                Directory.CreateDirectory(oldfolder);
                Directory.Move(newfolder,Path.Combine(oldfolder,newname));
                newfolder = Path.Combine(oldfolder, newname);
                newfolder2 = Path.Combine(oldfolder2, newname);
                Assert.IsTrue(CheckHeavyFolder(newfolder));
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate
                {
                    synchronizedFolder.Sync();
                    return CheckHeavyFolder(newfolder2);
                }, 10));
                Assert.IsTrue(CheckHeavyFolder(newfolder2));

                //  test heavy folder delete
                Console.WriteLine(" Local delete heavy folder");
                Directory.Delete(newfolder, true);
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder2, delegate
                {
                    synchronizedFolder.Sync();
                    return !Directory.Exists(newfolder2);
                }, 10));
                Assert.IsTrue(!Directory.Exists(newfolder2));

                // Clean.
                Console.WriteLine("Clean all.");
                Clean(localDirectory, synchronizedFolder);
                Clean(localDirectory2, synchronizedFolder2);
            }
        }

        // Goal: Make sure that CmisSync works for concurrent heavy folder.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void SyncConcurrentHeavyFolder(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    100,
                    false,
                    DateTime.MinValue,
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
            {
                synchronizedFolder.resetFailedOperationsCounter();
                synchronizedFolder.Sync();
                CleanAll(localDirectory);
                WatcherTest.WaitWatcher();
                synchronizedFolder.Sync();
                Console.WriteLine("Synced to clean state.");

                //  Invoke the backend synchronize for concurrent
                cmis.Initialize();

                ISession session = CreateSession(repoInfo);
                IFolder folder = (IFolder)session.GetObjectByPath(remoteFolderPath);

                string name1 = "SyncConcurrent.1";
                string path1 = Path.Combine(localDirectory, name1);
                string name2 = "SyncConcurrent.2";
                string path2 = Path.Combine(localDirectory, name2);

                //  create heavy folder in concurrent
                Console.WriteLine(" Concurrent create heavy folder");
                Assert.IsFalse(Directory.Exists(path1));
                IFolder folder1 = CreateFolder(folder, name1);
                Assert.IsTrue(WaitUntilDone(delegate
                {
                    return Directory.Exists(path1);
                }));
                Assert.IsTrue(Directory.Exists(path1));
                CreateHeavyFolderRemote(folder1);
                Assert.IsTrue(WaitUntilSyncIsDone(synchronizedFolder, delegate
                {
                    return CheckHeavyFolder(path1);
                }));
                Assert.IsTrue(CheckHeavyFolder(path1));

                //  rename heavy folder in concurrent 
                Console.WriteLine(" Concurrent rename heavy folder");
                IFolder folder2 = RenameFolder(folder1, name2);
                Assert.IsTrue(WaitUntilDone(delegate
                {
                    return CheckHeavyFolder(path2);
                }));
                Assert.IsTrue(CheckHeavyFolder(path2));

                //  move heavy folder in concurrent
                Console.WriteLine(" Concurrent move heavy folder");
                folder1 = CreateFolder(folder, name1);
                folder2.Move(folder, folder1);
                Assert.IsTrue(WaitUntilDone(delegate
                {
                    return CheckHeavyFolder(Path.Combine(path1, name2));
                }));
                Assert.IsTrue(CheckHeavyFolder(Path.Combine(path1, name2)));

                //  delete heavy folder in concurrent
                Console.WriteLine(" Remote delete heavy folder");
                folder1.DeleteTree(true, null, true);
                Assert.IsTrue(WaitUntilDone(delegate
                {
                    return !Directory.Exists(path1);
                }));
                Assert.IsFalse(Directory.Exists(path1));

                //  create and delete heavy folder in concurrent
                Console.WriteLine(" Remote create and delete heavy folder");
                cmis.Suspend();
                folder1 = CreateFolder(folder, name1);
                CreateHeavyFolderRemote(folder1);
                cmis.Resume();
                folder1.DeleteTree(true, null, true);
                Assert.IsTrue(WaitUntilDone(delegate
                {
                    return !Directory.Exists(path1);
                }));
                Assert.IsFalse(Directory.Exists(path1));

                // Clean.
                Console.WriteLine("Clean all.");
                Clean(localDirectory, synchronizedFolder);
            }
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void ClientSideDirectoryAndSmallFilesAddition(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    cmis, activityListener))
                {
                    synchronizedFolder.resetFailedOperationsCounter();
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    // Create directory and small files.
                    LocalFilesystemActivityGenerator.CreateDirectoriesAndFiles(localDirectory);

                    // Sync again.
                    synchronizedFolder.Sync();
                    Console.WriteLine("Second sync done.");

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }

        // Goal: Make sure that CmisSync does not crash when syncing while modifying locally.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncWhileModifyingFiles(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    cmis, activityListener))
                {
                    synchronizedFolder.resetFailedOperationsCounter();
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    // Sync a few times in a different thread.
                    bool syncing = true;
                    BackgroundWorker bw = new BackgroundWorker();
                    bw.DoWork += new DoWorkEventHandler(
                        delegate(Object o, DoWorkEventArgs args)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                Console.WriteLine("Sync F" + i.ToString());
                                synchronizedFolder.Sync();
                            }
                        }
                    );
                    bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                        delegate(object o, RunWorkerCompletedEventArgs args)
                        {
                            syncing = false;
                        }
                    );
                    bw.RunWorkerAsync();

                    // Keep creating/removing a file as long as sync is going on.
                    int count = 10000;
                    while (syncing)
                    {
                        count--;
                        if (count <= 0)
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                        //Console.WriteLine("Create/remove " + LocalFilesystemActivityGenerator.id);
                        LocalFilesystemActivityGenerator.CreateRandomFile(localDirectory, 3);
                        CleanAll(localDirectory);
                    }

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }

        // Goal: Make sure that CmisSync does not crash when syncing while adding/removing files/folders locally.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncWhileModifyingFolders(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    CMISSYNCDIR,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    cmis,
                    new Mock<IActivityListener>().Object))
                {
                    synchronizedFolder.resetFailedOperationsCounter();
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    // Sync a few times in a different thread.
                    bool syncing = true;
                    BackgroundWorker bw = new BackgroundWorker();
                    bw.DoWork += new DoWorkEventHandler(
                        delegate(Object o, DoWorkEventArgs args)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                Console.WriteLine("Sync D" + i.ToString());
                                synchronizedFolder.Sync();
                            }
                        }
                    );
                    bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                        delegate(object o, RunWorkerCompletedEventArgs args)
                        {
                            syncing = false;
                        }
                    );
                    bw.RunWorkerAsync();

                    // Keep creating/removing a file as long as sync is going on.
                    int count = 1000;
                    while (syncing)
                    {
                        count--;
                        if (count <= 0)
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                        //Console.WriteLine("Create/remove.");
                        LocalFilesystemActivityGenerator.CreateDirectoriesAndFiles(localDirectory);
                        CleanAll(localDirectory);
                    }

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }

        // Write a file and immediately check whether it has been created.
        // Should help to find out whether CMIS servers are synchronous or not.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void WriteThenRead(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            string fileName = "test.txt";
            var cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
//NOTGDS2            cmisParameters[SessionParameter.Password] = CmisSync.Auth.Crypto.Deobfuscate(password);
            cmisParameters[SessionParameter.Password] = password;
            cmisParameters[SessionParameter.RepositoryId] = repositoryId;

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.CreateSession(cmisParameters);

            // IFolder root = session.GetRootFolder();
            IFolder root = (IFolder)session.GetObjectByPath(remoteFolderPath);

            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(PropertyIds.Name, fileName);
            properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

            ContentStream contentStream = new ContentStream();
            contentStream.FileName = fileName;
            contentStream.MimeType = MimeType.GetMIMEType(fileName); // Should CmisSync try to guess?
            byte[] bytes = Encoding.UTF8.GetBytes("Hello,world!");
            contentStream.Stream = new MemoryStream(bytes);
            contentStream.Length = bytes.Length;

            // Create file.
            DotCMIS.Enums.VersioningState? state = null;
            // In Alfresco, this statement causes a "Conflict" response.
            /* if (true != session.RepositoryInfo.Capabilities.IsAllVersionsSearchableSupported)
            {
                state = DotCMIS.Enums.VersioningState.None;
            } */
            session.CreateDocument(properties, root, contentStream, state);
            // Check whether file is present.
            IItemEnumerable<ICmisObject> children = root.GetChildren();
            bool found = false;
            foreach (ICmisObject child in children)
            {
                string childFileName = (string)child.GetPropertyValue(PropertyIds.Name);
                Console.WriteLine(childFileName);
                if (childFileName.Equals(fileName))
                {
                    found = true;
                }
            }
            Assert.True(found);

            // Clean.
            IDocument doc = (IDocument)session.GetObjectByPath((remoteFolderPath + "/" + fileName).Replace("//", "/"));
            doc.DeleteAllVersions();
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void DotCmisToIBMConnections(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            var cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
//NOTGDS2            cmisParameters[SessionParameter.Password] = CmisSync.Auth.Crypto.Deobfuscate(password);
            cmisParameters[SessionParameter.Password] = password;
            cmisParameters[SessionParameter.RepositoryId] = repositoryId;

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.GetRepositories(cmisParameters)[0].CreateSession();

            Console.WriteLine("Depth: 1");
            IFolder root = session.GetRootFolder();
            IItemEnumerable<ICmisObject> children = root.GetChildren();
            foreach (var folder in children.OfType<IFolder>())
            {
                Console.WriteLine(folder.Path);
            }

            Console.WriteLine("Depth: 2");
            root = session.GetRootFolder();
            children = root.GetChildren();
            foreach (var folder in children.OfType<IFolder>())
            {
                Console.WriteLine(folder.Path);
                IItemEnumerable<ICmisObject> subChildren = folder.GetChildren();
                foreach (var subFolder in subChildren.OfType<IFolder>()) // Exception happens here, see https://issues.apache.org/jira/browse/CMIS-593
                {
                    Console.WriteLine(subFolder.Path);
                }
            }
        }

        [Test, TestCaseSource("TestServersFuzzy"), Category("Slow")]
        public void GetRepositoriesFuzzy(string url, string user, string password)
        {
            ServerCredentials credentials = new ServerCredentials()
            {
                Address = new Uri(url),
                UserName = user,
                Password = password
            };
            Tuple<CmisServer, Exception> server = CmisUtils.GetRepositoriesFuzzy(credentials);
            Assert.NotNull(server.Item1);
        }

        /// <summary>
        /// Waits the until sync is done.
        /// </summary>
        /// <returns>
        /// True, if checkStop has been true, otherwise false.
        /// </returns>
        /// <param name='synchronizedFolder'>
        /// Synchronized folder, which should be used for sync and waiting.
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
        public static bool WaitUntilSyncIsDone(CmisRepo.SynchronizedFolder synchronizedFolder, Func<bool> checkStop, int maxTries = 4,  int pollInterval = 5000)
        {
            int i = 0;
            while(i < maxTries)
            {
                try{
                    synchronizedFolder.Sync();
                }catch(DotCMIS.Exceptions.CmisRuntimeException e){
                    Console.WriteLine("{0} Exception caught and swallowed, retry.", e);
                    System.Threading.Thread.Sleep(pollInterval);
                    continue;
                }
                if(checkStop())
                    return true;
                Console.WriteLine(String.Format("Retry Sync in {0}ms", pollInterval));
                System.Threading.Thread.Sleep(pollInterval);
                i++;
            }
            Console.WriteLine("Sync call was not successful");
            return false;
        }

        /// <summary>
        /// Waits until checkStop is true or waiting duration is reached.
        /// </summary>
        /// <returns>
        /// True if checkStop is true, otherwise waits for pollInterval miliseconds and checks again until the wait threshold is reached.
        /// </returns>
        /// <param name='checkStop'>
        /// Checks if the condition, which is waited for is <c>true</c>.
        /// </param>
        /// <param name='wait'>
        /// Waiting threshold. If this is reached, <c>false</c> will be returned.
        /// </param>
        /// <param name='pollInterval'>
        /// Sleep duration between two condition validations by calling checkStop.
        /// </param>
        public static bool WaitUntilDone(Func<bool> checkStop, int wait = 300000, int pollInterval = 1000)
        {
            while (wait > 0)
            {
                System.Threading.Thread.Sleep(pollInterval);
                wait -= pollInterval;
                if (checkStop())
                    return true;
                Console.WriteLine(String.Format("Retry Wait in {0}ms", pollInterval));
            }
            Console.WriteLine("Wait was not successful");
            return false;
        }
    }
}
