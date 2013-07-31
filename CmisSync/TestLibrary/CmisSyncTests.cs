using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;
using System.IO;
using Moq;
using Newtonsoft.Json;
using DotCMIS.Data.Impl;
using System.ComponentModel;
using NUnit.Framework;
using System.Security.Cryptography.X509Certificates;
using System.Net;

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
    using NUnit.Framework;
    using CmisSync.Lib.Cmis;
    using CmisSync.Lib;
    using CmisSync.Lib.Sync;

    [TestFixture]
    public class CmisSyncTests
    {
        private readonly string CMISSYNCDIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DataSpace Sync");

        
        public CmisSyncTests()
        {
        }

        class TrustAlways : ICertificatePolicy
        {
            public bool CheckValidationResult (ServicePoint sp, X509Certificate certificate, WebRequest request, int error)
            {
                // For testing, always accept any certificate
                return true;
            }
        }

        [TestFixtureSetUp]
        public void ClassInit()
        {
            ServicePointManager.CertificatePolicy = new TrustAlways();
            File.Delete(ConfigManager.CurrentConfig.GetLogFilePath());
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
        }


        public static IEnumerable<object[]> TestServers
        {
            get
            {
                string path = "../../test-servers.json";
                bool exists = File.Exists(path);

                if (!exists) {
                    path= "../CmisSync/TestLibrary/test-servers.json";
                }

                return JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText(path));
            }
        }


        public static IEnumerable<object[]> TestServersFuzzy
        {
            get
            {
                string path = "../../test-servers-fuzzy.json";
                bool exists = File.Exists(path);

                if (!exists) {
                    path= "../CmisSync/TestLibrary/test-servers-fuzzy.json";
                }

                return JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText(path));
            }
        }


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
            catch (IOException ex)
            {
                Console.WriteLine("Exception on testing side, ignoring " + ex);
            }
        }


        // /////////////////////////// TESTS ///////////////////////////


        [Test]
        public void Placebo()
        {
            Assert.AreEqual(4, 2 + 2);
        }


        [Test]
        public void TestCrypto()
        {
            String[] test_pws = { "", "test", "Whatever", "Something to try" };
            foreach (String pass in test_pws) {
                String crypted = Crypto.Obfuscate(pass);
                Assert.AreEqual(Crypto.Deobfuscate(crypted), pass);
            }
        }


        [Test, TestCaseSource("TestServers")]
        public void GetRepositories(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Dictionary<string, string> repos = CmisUtils.GetRepositories(new Uri(url), user, password);
            foreach (KeyValuePair<string,string> pair in repos)
            {
                Console.WriteLine(pair.Key + " : " + pair.Value);
            }
            Assert.NotNull(repos);
        }


        [Test, TestCaseSource("TestServers")]
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
                    5000);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    activityListener,
                    cmis))
                {
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }


        [Test, TestCaseSource("TestServers")]
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
                    5000);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    activityListener,
                    cmis))
                {
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    // Create random small file.
                    LocalFilesystemActivityGenerator.CreateRandomFile(localDirectory, 3);

                    // Sync again.
                    synchronizedFolder.Sync();
                    Console.WriteLine("Second sync done.");

                    // Check that file is present server-side.
                    // TODO

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }


        [Test, TestCaseSource("TestServers")]
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
                    ".",
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    activityListener,
                    cmis))
                {
                    synchronizedFolder.Sync();
                    Console.WriteLine("Synced to clean state.");

                    // Create random big file.
                    LocalFilesystemActivityGenerator.CreateRandomFile(localDirectory, 1000); // 1 MB ... no that big to not load servers too much.

                    // Sync again.
                    synchronizedFolder.Sync();
                    Console.WriteLine("Second sync done.");

                    // Check that file is present server-side.
                    // TODO

                    // Clean.
                    Console.WriteLine("Clean all.");
                    Clean(localDirectory, synchronizedFolder);
                }
            }
        }


        [Test, TestCaseSource("TestServers")]
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
                    5000);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    activityListener,
                    cmis))
                {
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
        [Test, TestCaseSource("TestServers")]
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
                    5000);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    activityListener,
                    cmis))
                {
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
                    while (syncing)
                    {
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
        [Test, TestCaseSource("TestServers")]
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
                    5000);

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    activityListener,
                    cmis))
                {
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
                    while (syncing)
                    {
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
        // Should help see whether CMIS servers are synchronous or not.
        [Test, TestCaseSource("TestServers")]
        public void WriteThenRead(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            string fileName = "test.txt";
            var cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] = Crypto.Deobfuscate(password);
            cmisParameters[SessionParameter.RepositoryId] = repositoryId;

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.GetRepositories(cmisParameters)[0].CreateSession();

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
            if (true != session.RepositoryInfo.Capabilities.IsAllVersionsSearchableSupported)
            {
                state = DotCMIS.Enums.VersioningState.None;
            }
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
            IDocument doc = (IDocument)session.GetObjectByPath((remoteFolderPath + "/" + fileName).Replace("//","/"));
            doc.DeleteAllVersions();
        }


        [Test, TestCaseSource("TestServers")]
        public void DotCmisToIBMConnections(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            var cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] = Crypto.Deobfuscate(password);
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


        [Test, TestCaseSource("TestServersFuzzy")]
        public void GetRepositoriesFuzzy(string url, string user, string password)
        {
            Tuple<CmisServer, Exception> server = CmisUtils.GetRepositoriesFuzzy(new Uri(url), user, password);
            Assert.NotNull(server.Item1);
        }
    }
}
