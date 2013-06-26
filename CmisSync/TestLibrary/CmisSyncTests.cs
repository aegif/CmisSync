﻿using System;
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

    public class CmisSyncTests
    {
        private string CMISSYNCDIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CmisSync");

        public CmisSyncTests()
        {
            // Config.DefaultConfig = new Config(@"C:\Users\win7pro32bit\AppData\Roaming\cmissync", "config.xml"); // TODO relative path
        }

        public static IEnumerable<object[]> TestServers
        {
            get
            {
                return JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText("../../test-servers.json"));
            }
        }

        public static IEnumerable<object[]> TestServersFuzzy
        {
            get
            {
                return JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText("../../test-servers-fuzzy.json"));
            }
        }

        public void Dispose()
        {
        }
        
        private void Clean(string localDirectory, CmisRepo.SynchronizedFolder synchronizedFolder)
        {
            DirectoryInfo directory = new DirectoryInfo(localDirectory);
            // Delete all local files/folders.
            foreach (FileInfo file in directory.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in directory.GetDirectories())
            {
                dir.Delete(true);
            }
            // Sync deletions to server.
            synchronizedFolder.Sync();
            // Remove checkout folder.
            directory.Delete(false); // Not recursive, should not contain anything at this point.
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
            File.Delete(database);
            if (File.Exists(database))
            {
                File.Delete(database);
            }

            // Prepare empty directory.
            Directory.CreateDirectory(path);
        }


        // /////////////////////////// TESTS ///////////////////////////


        [Test]
        public void Placebo()
        {
            Assert.AreEqual(4, 2 + 2);
        }

        [Test, TestCaseSource("TestServers")]
        public void GetRepositories(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            Dictionary<string, string> repos = CmisUtils.GetRepositories(url, user, password);
            Assert.NotNull(repos);
        }

        [Test, TestCaseSource("TestServers")]
        public void Sync(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clean.
            CleanDirectory(Path.Combine(CMISSYNCDIR, canonical_name));
            // Mock.
            ActivityListener activityListener = new Mock<ActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo =  new RepoInfo(
                    canonical_name,
                    ".",
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000);
            CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                repoInfo,
                activityListener,
                new CmisRepo(repoInfo, activityListener)
            );
            synchronizedFolder.Sync();
        }

        [Test, TestCaseSource("TestServers")]
        public void ClientSideSmallFileAddition(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            // Mock.
            ActivityListener activityListener = new Mock<ActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    ".",
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000);
            CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                repoInfo,
                activityListener,
                new CmisRepo(repoInfo, activityListener)
            );
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
            Clean(localDirectory, synchronizedFolder);
        }

        [Test, TestCaseSource("TestServers")]
        public void ClientSideBigFileAddition(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            // Mock.
            ActivityListener activityListener = new Mock<ActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    ".",
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000);
            CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                repoInfo,
                activityListener,
                new CmisRepo(repoInfo, activityListener)
            );
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
            Clean(localDirectory, synchronizedFolder);
        }

        [Test, TestCaseSource("TestServers")]
        public void ClientSideDirectoryAndSmallFilesAddition(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            // Mock.
            ActivityListener activityListener = new Mock<ActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    ".",
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000);
            CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                repoInfo,
                activityListener,
                new CmisRepo(repoInfo, activityListener)
            );
            synchronizedFolder.Sync();
            Console.WriteLine("Synced to clean state.");

            // Create directory and small files.
            LocalFilesystemActivityGenerator.CreateDirectoriesAndFiles(localDirectory);

            // Sync again.
            synchronizedFolder.Sync();
            Console.WriteLine("Post sync done.");

            // Clean.
            Clean(localDirectory, synchronizedFolder);
        }

        // Goal: Make sure that CmisSync does not crash when syncing while modifying locally.
        [Test, TestCaseSource("TestServers")]
        public void SyncWhileModifyingFile(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            CleanDirectory(localDirectory);
            // Mock.
            ActivityListener activityListener = new Mock<ActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    ".",
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000);
            CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                repoInfo,
                activityListener,
                new CmisRepo(repoInfo, activityListener)
            );
            synchronizedFolder.Sync();
            Console.WriteLine("Synced to clean state.");

            // Sync a few times in a different thread.
            bool syncing = true;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(
                delegate(Object o, DoWorkEventArgs args)
                {
                    //// Clean.
                    //CleanDirectory(Path.Combine(CMISSYNCDIR, canonical_name));
                    // Mock.
                    ActivityListener activityListener2 = new Mock<ActivityListener>().Object;
                    // Sync.
                    CmisRepo.SynchronizedFolder synchronizedFolder2 = new CmisRepo.SynchronizedFolder(
                        repoInfo,
                        activityListener,
                        new CmisRepo(repoInfo, activityListener)
                    );
                    for (int i = 0; i < 10; i++)
                    {
                        Console.WriteLine("Sync n" + i);
                        synchronizedFolder2.Sync();
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
                DirectoryInfo localDirectoryInfo = new DirectoryInfo(localDirectory);
                foreach (FileInfo file in localDirectoryInfo.GetFiles())
                {
                    if (file.Name.EndsWith(".sync"))
                    {
                        continue;
                    }

                    try
                    {
                        file.Delete();
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Exception on testing side, ignoring");
                    }
                }
            }

            // Clean.
            Clean(localDirectory, synchronizedFolder);
        }

        // Goal: Make sure that CmisSync does not crash when syncing while adding/removing files/folders locally.
        [Test, TestCaseSource("TestServers")]
        public void SyncWhileModifyingFolders(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare checkout directory.
            string localDirectory = Path.Combine(CMISSYNCDIR, canonical_name);
            
            //CleanDirectory(localDirectory);
            //Console.WriteLine("Synced to clean state.");
            
            // Mock.
            ActivityListener activityListener = new Mock<ActivityListener>().Object;
            // Sync.
            RepoInfo repoInfo = new RepoInfo(
                    canonical_name,
                    ".",
                    remoteFolderPath,
                    url,
                    user,
                    password,
                    repositoryId,
                    5000);
            CmisRepo.SynchronizedFolder synchronizedFolder = new CmisRepo.SynchronizedFolder(
                repoInfo,
                activityListener,
                new CmisRepo(repoInfo, activityListener)
            );
            
            // Sync a few times in a different thread.
            bool syncing = true;
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(
                delegate(Object o, DoWorkEventArgs args)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Console.WriteLine("Sync.");
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
                Console.WriteLine("Create/remove.");
                LocalFilesystemActivityGenerator.CreateDirectoriesAndFiles(localDirectory);
                DirectoryInfo localDirectoryInfo = new DirectoryInfo(localDirectory);
                foreach (FileInfo file in localDirectoryInfo.GetFiles())
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Exception on testing side, ignoring");
                    }
                }
                foreach (DirectoryInfo directory in localDirectoryInfo.GetDirectories())
                {
                    try
                    {
                        directory.Delete();
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Exception on testing side, ignoring");
                    }
                }
            }

            // Clean.
            Clean(localDirectory, synchronizedFolder);
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
            cmisParameters[SessionParameter.Password] = password;
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
            contentStream.Stream = File.OpenRead("../../../TestLibraryRunner/Program.cs");

            // Create file.
            session.CreateDocument(properties, root, contentStream, null);

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
            IDocument doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + fileName);
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

        [Test, TestCaseSource("TestServersFuzzy")]
        public void GetRepositoriesFuzzy(string url, string user, string password)
        {
            CmisServer server = CmisUtils.GetRepositoriesFuzzy(url, user, password);
            Assert.NotNull(server);
        }
    }
}
