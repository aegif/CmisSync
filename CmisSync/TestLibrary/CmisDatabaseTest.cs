using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;


namespace TestLibrary
{
    using NUnit.Framework;

    [TestFixture]
    class CmisDatabaseTest
    {
        private static readonly string RootPath = Path.Combine(ConfigManager.CurrentConfig.FoldersPath, "CmisDatabaseTest");
        private static readonly string DatabasePath = Path.Combine(RootPath, "CmisDatabaseTest.cmissync");

        [TestFixtureSetUp]
        public void ClassInit()
        {
            File.Delete(ConfigManager.CurrentConfig.GetLogFilePath());
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());

            Directory.CreateDirectory(RootPath);
        }

        [TestFixtureTearDown]
        public void ClassCleanup()
        {
            Directory.Delete(RootPath, true);
        }

        [SetUp]
        public void TestInit()
        {
            File.Delete(DatabasePath);
        }

        [Test, Category("Database")]
        public void TestMoveFile()
        {
            using (Database database = new Database(DatabasePath))
            {
                string oldPath = Path.Combine(RootPath, "1.old");
                CreateTestFile(oldPath, 10);
                string newPath = Path.Combine(RootPath, "1.new");
                
                database.AddFile(oldPath, "1", DateTime.Now, null);
                Assert.True(database.ContainsFile(oldPath));
                Assert.False(database.ContainsFile(newPath));
                database.MoveFile(oldPath, newPath);
                Assert.False(database.ContainsFile(oldPath));
                Assert.True(database.ContainsFile(newPath));
            }
        }

        [Test, Category("Database")]
        public void TestMoveFolder()
        {
            using (Database database = new Database(DatabasePath))
            {
                Directory.CreateDirectory(Path.Combine(RootPath, "folder1/folder2"));

                string oldPath = Path.Combine(RootPath, "folder1/folder2", "1.test");
                CreateTestFile(oldPath, 10);
                string newPath = Path.Combine(RootPath, "sub1/folder2", "1.test");

                database.AddFile(oldPath, "1", DateTime.Now, null);
                database.AddFolder(Path.Combine(RootPath, "folder1"), "D1", DateTime.Now);
                database.AddFolder(Path.Combine(RootPath, "folder1/folder2"), "D12", DateTime.Now);
                Assert.True(database.ContainsFile(oldPath));
                Assert.True(database.ContainsFolder(Path.Combine(RootPath, "folder1")));
                Assert.True(database.ContainsFolder(Path.Combine(RootPath, "folder1/folder2")));
                Assert.False(database.ContainsFile(newPath));
                Assert.False(database.ContainsFolder(Path.Combine(RootPath, "sub1")));
                Assert.False(database.ContainsFolder(Path.Combine(RootPath, "sub1/folder2")));
                database.MoveFolder(Path.Combine(RootPath, "folder1"), Path.Combine(RootPath, "sub1"));
                Assert.False(database.ContainsFile(oldPath));
                Assert.False(database.ContainsFolder(Path.Combine(RootPath, "folder1")));
                Assert.False(database.ContainsFolder(Path.Combine(RootPath, "folder1/folder2")));
                Assert.True(database.ContainsFile(newPath));
                Assert.True(database.ContainsFolder(Path.Combine(RootPath, "sub1")));
                Assert.True(database.ContainsFolder(Path.Combine(RootPath, "sub1/folder2")));
            }
        }

        private void CreateTestFile(string path, int sizeInKB)
        {
            Random random = new Random();
            byte[] data = new byte[1024];

            using (FileStream stream = File.OpenWrite(path))
            {
                for (int i = 0; i < sizeInKB; i++)
                {
                    random.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }
        }
    }
}
