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
using Xunit.Extensions;
using Newtonsoft.Json;

/**
 * Unit Tests for CmisSync.
 * 
 * To use them, first create a JSON file containing the credentials/parameters to your CMIS server(s)
 * Call it test-servers.json and use this format:
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
    public class CmisSyncTests : IDisposable
    {
        public CmisSyncTests()
        {
            SparkleConfig.DefaultConfig = new SparkleConfig(@"C:\Users\nico\AppData\Roaming\cmissync", "config.xml");
        }

        public static IEnumerable<object[]> TestServers
        {
            get
            {
                return JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText("../../test-servers.json"));
            }
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


        // /////////////////////////// TESTS ///////////////////////////


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

        [Theory, PropertyData("TestServers")]
        public void GetRepositories(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            string[] repos = CmisUtils.GetRepositories(url, user, password);
            Assert.NotNull(repos);
        }

        [Theory, PropertyData("TestServers")]
        public void Sync(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            ActivityListener activityListener = new Mock<ActivityListener>().Object;
            CmisDirectory cmisDirectory = new CmisDirectory(
                canonical_name,
                localPath,
                remoteFolderPath,
                url,
                user,
                password,
                repositoryId,
                activityListener
            );
            cmisDirectory.Sync();
        }

        [Theory, PropertyData("TestServers")]
        public void ClientSideChanges(string canonical_name, string localPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            ActivityListener activityListener = new Mock<ActivityListener>().Object;
            // Create checkout.
            CmisDirectory cmisDirectory = new CmisDirectory(
                canonical_name,
                localPath,
                remoteFolderPath,
                url,
                user,
                password,
                repositoryId,
                activityListener
            );
            cmisDirectory.Sync();

            // Generate local filesystem activity
            string path = Path.Combine(@"C:\Users\nico\CmisSync", canonical_name);
            LocalFilesystemActivityGenerator.GenerateActivity(path);
        }

        [Fact]
        public void DotCmisToIBMConnections()
        {
            var cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            // TODO Insert here your own credentials to an IBM Connections server

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
}
