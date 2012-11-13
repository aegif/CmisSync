using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Xunit;
using SparkleLib.Cmis;
using SparkleLib;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;
using System.IO;
using Moq;

namespace TestLibrary
{
    public class ConnectionTests : IDisposable
    {
        List<TestServer> testServers;

        public ConnectionTests()
        {
            SparkleConfig.DefaultConfig = new SparkleConfig(@"C:\Users\nico\AppData\Roaming\cmissync", "config.xml");
            testServers = new List<TestServer>();
            // Add your CMIS test server(s) below
            // testServers.Add(new TestServer("unittest0", "/localPath", "/remotePath", "http://servername:port/path", "username", "password", "repository"));
            // TODO 1
        }

        public void Dispose()
        {
            DeleteDirectoryIfExists(@"C:\Users\nico\CmisSync\unittest1");
            DeleteDirectoryIfExists(@"C:\Users\nico\CmisSync\unittest2");
            DeleteDirectoryIfExists(@"C:\Users\nico\CmisSync\unittest3");
        }

        private void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        [Fact]
        public void Placebo()
        {
            Assert.Equal(4, 2 + 2);
        }

        [Fact]
        public void TruncatePath()
        {
            CmisDatabase db = new CmisDatabase(@"C:\User Homes\nico\CmisSync\myfolder");
            string shortened = db.Normalize(@"C:\User Homes\nico\CmisSync\myfolder\dir1\dir2\file.txt");
            Assert.Equal("dir1/dir2/file.txt", shortened);
        }

        [Fact]
        public void GetRepositories()
        {
            testServers.ForEach(delegate(TestServer testServer)
            {
                CmisUtils.GetRepositories(testServer.url, testServer.user, testServer.password);
            });
        }

        [Fact]
        public void Sync()
        {
            testServers.ForEach(delegate(TestServer testServer)
            {
                ActivityListener activityListener = new Mock<ActivityListener>().Object;
                Cmis cmis = new Cmis(
                    testServer.canonical_name,
                    testServer.localPath,
                    testServer.remoteFolderPath,
                    testServer.url,
                    testServer.user,
                    testServer.password,
                    testServer.repositoryId,
                    activityListener
                );
                cmis.Sync();
            });
        }

        [Fact]
        public void ClientSideChanges()
        {
            testServers.ForEach(delegate(TestServer testServer)
            {
                ActivityListener activityListener = new Mock<ActivityListener>().Object;
                // Create checkout.
                Cmis cmis = new Cmis(
                    testServer.canonical_name,
                    testServer.localPath,
                    testServer.remoteFolderPath,
                    testServer.url,
                    testServer.user,
                    testServer.password,
                    testServer.repositoryId,
                    activityListener
                );
                cmis.Sync();

                // Generate local filesystem activity
                string path = Path.Combine(@"C:\Users\nico\CmisSync", testServer.canonical_name);
                LocalFilesystemActivityGenerator.GenerateActivity(path);
            });
        }

        [Fact]
        public void DotCmisToFileNet()
        {
            var cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            // TODO 2

            SessionFactory factory = SessionFactory.NewInstance();
            IList<IRepository> repositories = factory.GetRepositories(cmisParameters);
            foreach (var repository in repositories)
            {
                Console.WriteLine(repository.Id);
            }
        }

        [Fact]
        public void DotCmisToIBMConnections()
        {
            var cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            // TODO 3

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
    }

    public class TestServer
    {
        public string canonical_name, localPath, remoteFolderPath,
             url, user, password, repositoryId;
        public TestServer(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            this.canonical_name = canonical_name;
            this.localPath = localPath;
            this.remoteFolderPath = remoteFolderPath;
            this.url = url;
            this.user = user;
            this.password = password;
            this.repositoryId = repositoryId;
        }

    }
}
