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
 * To use them, first create a JSON file containing the credentials/parameters to a test folder on your CMIS server(s)
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
 * Warning: All previous content of these folders will be deleted by the tests.
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

    [TestFixture]
    public class SyncTests : AbstractSyncTests
    {
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

                try
                {
                    return JsonConvert.DeserializeObject<List<object[]>>(
                       File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    return Enumerable.Empty<object[]>();
                }
            }
        }

        public SyncTests()
        {
        }

        [Test, Category("Fast")]
        public void Placebo()
        {
            Assert.AreEqual(4, 2 + 2);
        }

        [Test, Category("Fast")]
        public void Obfuscation()
        {
            String[] test_pws = { "", "test", "Whatever", "Something to try" };
            foreach (String pass in test_pws)
            {
                String crypted = Crypto.Obfuscate(pass);
                Assert.AreEqual(Crypto.Deobfuscate(crypted), pass);
            }
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void ConnectToTestServers(string canonicalName, string localPath, string remoteFolderPath,
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
        public void EmptySync(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    ConfigFolder(),
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            CmisRepo cmisRepo = new CmisRepo(repoInfo, activityListener, false);
            CmisRepo.SynchronizedFolder synchronizedFolder =
                new CmisRepo.SynchronizedFolder(repoInfo, cmisRepo, activityListener);

            bool success = synchronizedFolder.Sync();
            Assert.IsTrue(success);
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void ClientSideSmallFileAddition(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    ConfigFolder(),
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            CmisRepo cmisRepo = new CmisRepo(repoInfo, activityListener, false);
            CmisRepo.SynchronizedFolder synchronizedFolder =
                new CmisRepo.SynchronizedFolder(repoInfo, cmisRepo, activityListener);

            // Create random small file.
            string filename = LocalFilesystemActivityGenerator.GetNextFileName();
            string remoteFilePath = (remoteFolderPath + "/" + filename).Replace("//", "/");
            LocalFilesystemActivityGenerator.CreateRandomFile(
                Path.Combine(cmisSyncDirectory, canonicalName), 3);

            // Sync.
            bool success = synchronizedFolder.Sync();
            Assert.IsTrue(success);

            // Check that file is present server-side.
            IDocument doc = (IDocument)CreateSession(repoInfo).GetObjectByPath(remoteFilePath, true);
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void ClientSideBigFileAddition(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    ConfigFolder(),
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            CmisRepo cmis = new CmisRepo(repoInfo, activityListener, false);
            CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener);
            synchronizedFolder.Sync();
            Console.WriteLine("Synced to clean state.");

            // Create random big file.
            string filename = LocalFilesystemActivityGenerator.GetNextFileName();
            string remoteFilePath = (remoteFolderPath + "/" + filename).Replace("//", "/");
            LocalFilesystemActivityGenerator.CreateRandomFile(localPath, 1000); // 1 MB ... no that big to not load servers too much.

            // Sync.
            bool success = synchronizedFolder.Sync();
            Assert.IsTrue(success);

            // Check that file is present server-side.
            IDocument doc = (IDocument)CreateSession(repoInfo).GetObjectByPath(remoteFilePath, true);
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
        }

        /*[Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void ResumeBigFile(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonicalName);
            string canonicalName2 = canonicalName + ".BigFile";
            string localDirectory2 = Path.Combine(CMISSYNCDIR, canonicalName2);
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
                    canonicalName,
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
                    canonicalName2,
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

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener, false))
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
                using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener, false))
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
                using (CmisRepo cmis2 = new CmisRepo(repoInfo2, activityListener, false))
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

            using (CmisRepo cmis2 = new CmisRepo(repoInfo2, activityListener, false))
            using (CmisRepo.SynchronizedFolder synchronizedFolder2 =
                    new CmisRepo.SynchronizedFolder(repoInfo2, cmis2, activityListener))
            {
                // Clean.
                Console.WriteLine("Clean all.");
                Clean(localDirectory2, synchronizedFolder2);
            }
        }*/

        // Goal: Make sure that CmisSync works for uploading modified files.
        /*[Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncUploads(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonicalName);
            CleanDirectory(localDirectory);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
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

            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener, false))
            {
                using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
                {
                    // Clear local and remote folder
                    synchronizedFolder.Sync();
                    CleanAll(localDirectory);
                    synchronizedFolder.Sync();
                    // Create a list of file names
                    List<string> files = new List<string>();
                    for(int i = 1 ; i <= 10; i++)
                    {
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
                                try
                                {
                                    string remoteFilePath = (remoteFolderPath + "/" + filename).Replace("//", "/");
                                    IDocument d = (IDocument)CreateSession(repoInfo).GetObjectByPath(remoteFilePath);
                                    if (d == null || d.ContentStreamLength != length)
                                    {
                                        return false;
                                    }
                                }
                                catch (Exception)
                                {
                                    return false;
                                }
                            }
                            return true;
                        }));
                        // Check, if all local files are available
                        Assert.AreEqual(files.Count, Directory.GetFiles(localDirectory).Length);
                    }
                }
            }
        }*/

        // Goal: Make sure that CmisSync works for remote changes.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void FileAndFolderOperations(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    ConfigFolder(),
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);

            CmisRepo cmis = new CmisRepo(repoInfo, activityListener, false);
            CmisRepo.SynchronizedFolder synchronizedFolder =
                new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener);

            ISession session = CreateSession(repoInfo);
            IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath, true);

            string name1 = "test.1";
            string path1 = Path.Combine(localPath, name1);

            string name2 = "test.2";
            string path2 = Path.Combine(localPath, name2);

            Console.WriteLine("Create remote document");
            Assert.IsFalse(File.Exists(path1));
            IDocument doc1 = CreateRemoteDocument(remoteFolder, name1, "test");
            bool success = synchronizedFolder.Sync();
            Assert.IsTrue(success);
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return File.Exists(path1);
            }));

            Console.WriteLine("Rename remote document");
            IDocument doc2 = RenameRemoteDocument(doc1, name2);
            success = synchronizedFolder.Sync();
            Assert.IsTrue(success);
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return !File.Exists(path1) && File.Exists(path2);
            }));
            
            Console.WriteLine("Create remote folder");
            IFolder remoteSubFolder = CreateRemoteFolder(remoteFolder, name1);
            success = synchronizedFolder.Sync();
            Assert.IsTrue(success);
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return Directory.Exists(path1);
            }));

            Console.WriteLine("Move remote document");
            string filename = Path.Combine(path1, name2);
            doc2.Move(remoteFolder, remoteSubFolder);
            success = synchronizedFolder.Sync();
            Assert.IsTrue(success);
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return File.Exists(filename);
            }));

            Console.WriteLine("Delete remote document");
            doc2.DeleteAllVersions();
            success = synchronizedFolder.Sync();
            Assert.IsTrue(success);
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return !File.Exists(filename);
            }));

            //  rename folder
            Console.WriteLine(" Remote rename folder");
            Assert.IsTrue(Directory.Exists(path1));
            Assert.IsFalse(Directory.Exists(path2));
            IFolder folder2 = RenameRemoteFolder(remoteSubFolder, name2);
            Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder, delegate {
                return !Directory.Exists(path1) && Directory.Exists(path2);
            }));
            Assert.IsFalse(Directory.Exists(path1));
            Assert.IsTrue(Directory.Exists(path2));

            //  move folder
            Console.WriteLine(" Remote move folder");
            Assert.IsFalse(Directory.Exists(path1));
            remoteSubFolder = CreateRemoteFolder(remoteFolder, name1);
            Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder, delegate {
                return Directory.Exists(path1) && !Directory.Exists(Path.Combine(path2, name1));
            }));
            Assert.IsTrue(Directory.Exists(path1));
            Assert.IsFalse(Directory.Exists(Path.Combine(path2, name1)));
            remoteSubFolder.Move(remoteFolder, folder2);
            Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder, delegate {
                return !Directory.Exists(path1) && Directory.Exists(Path.Combine(path2, name1));
            }));
            Assert.IsFalse(Directory.Exists(path1));
            Assert.IsTrue(Directory.Exists(Path.Combine(path2, name1)));

            //  move folder with sub folder and sub file
            Console.WriteLine(" Remote move folder with subfolder and subfile");
            Assert.IsFalse(File.Exists(Path.Combine(path2, name1, name1)));
            Assert.IsFalse(Directory.Exists(Path.Combine(path2, name1, name2)));
            CreateRemoteDocument(remoteSubFolder, name1, "test");
            CreateRemoteFolder(remoteSubFolder, name2);
            Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder, delegate {
                return File.Exists(Path.Combine(path2, name1, name1)) && Directory.Exists(Path.Combine(path2, name1, name2));
            }));
            Assert.IsTrue(File.Exists(Path.Combine(path2, name1, name1)));
            Assert.IsTrue(Directory.Exists(Path.Combine(path2, name1, name2)));
            remoteSubFolder.Move(folder2, remoteFolder);
            Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder, delegate {
                return File.Exists(Path.Combine(path1, name1)) && Directory.Exists(Path.Combine(path1, name2));
            }));
            Assert.IsTrue(File.Exists(Path.Combine(path1, name1)));
            Assert.IsTrue(Directory.Exists(Path.Combine(path1, name2)));

            //  delete folder tree
            Console.WriteLine(" Remote delete folder tree");
            Assert.IsTrue(Directory.Exists(path1));
            remoteSubFolder.DeleteTree(true, null, true);
            Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder, delegate {
                return !Directory.Exists(path1);
            }, 20));
            Assert.IsFalse(Directory.Exists(path1));
            Assert.IsTrue(Directory.Exists(path2));
            folder2.DeleteTree(true, null, true);
            SyncAndWaitUntilCondition(synchronizedFolder, delegate {
                return !Directory.Exists(path2);
            });
            if (Directory.Exists(path2))
            {
                success = synchronizedFolder.Sync();
            }
            Assert.IsFalse(Directory.Exists(path2));
        }

        // Goal: Make sure that CmisSync works for remote heavy folder changes.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void HeavyFileAndFolderOperations(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    ConfigFolder(),
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);

            CmisRepo cmisRepo = new CmisRepo(repoInfo, activityListener, false);
            CmisRepo.SynchronizedFolder synchronizedFolder =
            new CmisRepo.SynchronizedFolder(repoInfo, cmisRepo, activityListener);
            ISession session = CreateSession(repoInfo);
            IFolder folder = (IFolder)session.GetObjectByPath(remoteFolderPath, true);

            string name1 = "test.1";
            string path1 = Path.Combine(localPath, name1);

            string name2 = "test.2";
            string path2 = Path.Combine(localPath, name2);

            //  create heavy folder
            Console.WriteLine(" Remote create heavy folder");
            Assert.IsFalse(Directory.Exists(path1));
            IFolder folder1 = CreateRemoteFolder(folder, name1);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return Directory.Exists(path1);
            }));
            CreateHeavyRemoteFolder(folder1);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return CheckHeavyLocalFolder(path1);
            }));

            //  rename heavy folder
            Console.WriteLine(" Remote rename heavy folder");
            IFolder folder2 = RenameRemoteFolder(folder1, name2);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return CheckHeavyLocalFolder(path2);
            }));

            //  move heavy folder
            Console.WriteLine(" Remote move heavy folder");
            folder1 = CreateRemoteFolder(folder, name1);
            folder2.Move(folder, folder1);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return CheckHeavyLocalFolder(Path.Combine(path1,name2));
            }));

            //  delete heavy folder
            Console.WriteLine(" Remote delete heavy folder");
            folder1.DeleteTree(true, null, true);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return !Directory.Exists(path1);
            }));
        }
        
        // Goal: Make sure that CmisSync works for equality.
        /*[Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncRenamedFiles(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonicalName);
            string canonicalName2 = canonicalName + ".equality";
            string localDirectory2 = Path.Combine(CMISSYNCDIR, canonicalName2);
            CleanDirectory(localDirectory);
            CleanDirectory(localDirectory2);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
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
                    canonicalName2,
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
        public void SyncEquality(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonicalName);
            string canonicalName2 = canonicalName + ".equality";
            string localDirectory2 = Path.Combine(CMISSYNCDIR, canonicalName2);
            CleanDirectory(localDirectory);
            CleanDirectory(localDirectory2);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
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
                    canonicalName2,
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
        public void SyncEmptyFileEquality(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonicalName);
            string canonicalName2 = canonicalName + ".equality";
            string localDirectory2 = Path.Combine(CMISSYNCDIR, canonicalName2);
            CleanDirectory(localDirectory);
            CleanDirectory(localDirectory2);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
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
                    canonicalName2,
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
        /*[Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void SyncEqualityHeavyFolder(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);
            // Prepare checkout directory.
            string localDirectory = Path.Combine(cmisSyncDirectory, canonicalName);
            string canonicalName2 = canonicalName + ".equality";
            string localDirectory2 = Path.Combine(cmisSyncDirectory, canonicalName2);
            CleanLocalDirectory(localDirectory2);
            Console.WriteLine("Synced to clean state.");

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    cmisSyncDirectory,
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
                    canonicalName2,
                    cmisSyncDirectory,
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);
            using (CmisRepo cmis = new CmisRepo(repoInfo, activityListener,false))
            using (CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmis, activityListener))
            using (CmisRepo cmis2 = new CmisRepo(repoInfo2, activityListener, false))
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
                Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder2, delegate
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
                Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder2, delegate
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
                Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder2, delegate
                {
                    synchronizedFolder.Sync();
                    return CheckHeavyFolder(newfolder2);
                }, 10));
                Assert.IsTrue(CheckHeavyFolder(newfolder2));

                //  test heavy folder delete
                Console.WriteLine(" Local delete heavy folder");
                Directory.Delete(newfolder, true);
                Assert.IsTrue(SyncAndWaitUntilCondition(synchronizedFolder2, delegate
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
        }*/

        // Goal: Make sure that CmisSync works for concurrent heavy folder.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void SyncConcurrentHeavyFolder(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    ConfigFolder(),
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    100,
                    false,
                    DateTime.MinValue,
                    true);

            CmisRepo cmisRepo = new CmisRepo(repoInfo, activityListener, false);
            CmisRepo.SynchronizedFolder synchronizedFolder =
                    new CmisRepo.SynchronizedFolder(repoInfo, cmisRepo, activityListener);
            //  Invoke the backend synchronize for concurrent
            cmisRepo.Initialize();

            ISession session = CreateSession(repoInfo);
            IFolder folder = (IFolder)session.GetObjectByPath(remoteFolderPath, true);

            string name1 = "SyncConcurrent.1";
            string path1 = Path.Combine(localPath, name1);
            string name2 = "SyncConcurrent.2";
            string path2 = Path.Combine(localPath, name2);

            //  create heavy folder in concurrent
            IFolder folder1 = CreateRemoteFolder(folder, name1);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return Directory.Exists(path1);
            }));
            CreateHeavyRemoteFolder(folder1);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return CheckHeavyLocalFolder(path1);
            }));

            //  rename heavy folder in concurrent 
            Console.WriteLine(" Concurrent rename heavy folder");
            IFolder folder2 = RenameRemoteFolder(folder1, name2);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return CheckHeavyLocalFolder(path2);
            }));

            //  move heavy folder in concurrent
            Console.WriteLine(" Concurrent move heavy folder");
            folder1 = CreateRemoteFolder(folder, name1);
            folder2.Move(folder, folder1);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return CheckHeavyLocalFolder(Path.Combine(path1, name2));
            }));

            //  delete heavy folder in concurrent
            Console.WriteLine(" Remote delete heavy folder");
            folder1.DeleteTree(true, null, true);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return !Directory.Exists(path1);
            }));

            //  create and delete heavy folder in concurrent
            Console.WriteLine(" Remote create and delete heavy folder");
            cmisRepo.Disable();
            folder1 = CreateRemoteFolder(folder, name1);
            CreateHeavyRemoteFolder(folder1);
            cmisRepo.Enable();
            folder1.DeleteTree(true, null, true);
            synchronizedFolder.Sync();
            Assert.IsTrue(WaitUntilCondition(delegate
            {
                return !Directory.Exists(path1);
            }));
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void ClientSideDirectoryAndSmallFilesAddition(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    ConfigFolder(),
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            CmisRepo cmisRepo = new CmisRepo(repoInfo, activityListener, false);
            CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    cmisRepo, activityListener);

            // Create directory and small files.
            LocalFilesystemActivityGenerator.CreateDirectoriesAndFiles(localPath);

            // Sync.
            synchronizedFolder.Sync();
        }

        // Goal: Make sure that CmisSync does not crash when syncing while modifying locally.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncWhileModifyingFiles(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);

            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    ConfigFolder(),
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    new DateTime(1900, 01, 01),
                    true);

            CmisRepo cmisRepo = new CmisRepo(repoInfo, activityListener, false);
            CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    cmisRepo, activityListener);

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
                LocalFilesystemActivityGenerator.CreateRandomFile(localPath, 3);
                Directory.Delete(localPath);
            }
        }

        // Goal: Make sure that CmisSync does not crash when syncing while adding/removing files/folders locally.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void SyncWhileModifyingFolders(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Clean(canonicalName, localPath, remoteFolderPath, url, user, password, repositoryId);

            // Mock.
            IActivityListener activityListener = new Mock<IActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonicalName,
                    ConfigFolder(),
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000,
                    false,
                    DateTime.MinValue,
                    true);

            CmisRepo cmis = new CmisRepo(repoInfo, activityListener, false);
            CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                    repoInfo,
                    cmis,
                    new Mock<IActivityListener>().Object);
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
                LocalFilesystemActivityGenerator.CreateDirectoriesAndFiles(localPath);
                Directory.Delete(localPath);
            }
        }

        // Write a file and immediately check whether it has been created.
        // Should help to find out whether CMIS servers are synchronous or not.
        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void WriteThenRead(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            string fileName = "test.txt";
            var cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
            cmisParameters[SessionParameter.Password] = password;
            cmisParameters[SessionParameter.RepositoryId] = repositoryId;

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.CreateSession(cmisParameters);

            // IFolder root = session.GetRootFolder();
            IFolder root = (IFolder)session.GetObjectByPath(remoteFolderPath, true);

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
            IDocument doc = (IDocument)session.GetObjectByPath((remoteFolderPath + "/" + fileName).Replace("//", "/"), true);
            doc.DeleteAllVersions();
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        [Ignore]
        public void DotCmisToIBMConnections(string canonicalName, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            var cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = url;
            cmisParameters[SessionParameter.User] = user;
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
    }
}
