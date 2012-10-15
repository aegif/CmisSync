using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Xunit;
using SparkleLib.Cmis;
using SparkleLib;

namespace TestLibrary
{
    public class ConnectionTests
    {
        List<TestServer> testServers;

        public ConnectionTests()
        {
            SparkleConfig.DefaultConfig = new SparkleConfig("C:\\Users\\nico\\AppData\\Roaming\\cmissync", "config.xml");
            testServers = new List<TestServer>();
            // Add your CMIS test server(s) below
            // testServers.Add(new TestServer("unittest0", "/localPath", "/remotePath", "http://servername:port/path", "username", "password", "repository"));
        }

        [Fact]
        public void Placebo()
        {
            Assert.Equal(4, 2 + 2);
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
                Cmis cmis = new Cmis(
                    testServer.canonical_name,
                    testServer.localPath,
                    testServer.remoteFolderPath,
                    testServer.url,
                    testServer.user,
                    testServer.password,
                    testServer.repositoryId
                );
                cmis.Sync();
            });
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
