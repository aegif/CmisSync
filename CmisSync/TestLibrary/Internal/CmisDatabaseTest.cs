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
    using CmisSync.Lib.Database;
    using CmisSync.Lib.Config;

    [TestFixture]
    class CmisDatabaseTest
    {
        private static readonly string RootPath = Path.Combine(ConfigManager.CurrentConfig.FoldersPath, "CmisDatabaseTest");
        private static readonly string DatabasePath = Path.Combine(RootPath, "CmisDatabaseTest.cmissync");
        private static readonly byte[] FakeHash = {1,2,3,4,5};

        [TestFixtureSetUp]
        public void ClassInit()
        {
            //File.Delete(ConfigManager.CurrentConfig.GetLogFilePath());
            //log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());

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
        
        /*[Test, Category("Database")]
        public void TestSpecialCharacter()
        {
            string oldPath = Path.Combine(RootPath, "a'b'c");
            string newPath = Path.Combine(RootPath, "'a'b'c'");

            using (Database database = new Database(DatabasePath), )
            {
                CreateTestFile(oldPath, 10);
                database.AddFile(oldPath, "1", DateTime.Now, null, FakeHash);
                Assert.True(database.ContainsFile(oldPath));
                Assert.False(database.ContainsFile(newPath));
                database.MoveFile(oldPath, newPath);
                Assert.False(database.ContainsFile(oldPath));
                Assert.True(database.ContainsFile(newPath));
                database.RemoveFile(newPath);
                Assert.False(database.ContainsFile(oldPath));
                Assert.False(database.ContainsFile(newPath));
            }

            using (Database database = new Database(DatabasePath))
            {
                database.AddFolder(oldPath, "1", DateTime.Now);
                Assert.True(database.ContainsFolder(oldPath));
                Assert.False(database.ContainsFolder(newPath));
                database.MoveFolder(oldPath, newPath);
                Assert.False(database.ContainsFolder(oldPath));
                Assert.True(database.ContainsFolder(newPath));
                database.RemoveFolder(newPath);
                Assert.False(database.ContainsFolder(oldPath));
                Assert.False(database.ContainsFolder(newPath));
            }
        }

        [Test, Category("Database")]
        public void TestMoveFile()
        {
            using (Database database = new Database(DatabasePath))
            {
                string oldPath = Path.Combine(RootPath, "1.old");
                CreateTestFile(oldPath, 10);
                string newPath = Path.Combine(RootPath, "1.new");
                
                database.AddFile(oldPath, "1", DateTime.Now, null, FakeHash);
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

                database.AddFile(oldPath, "1", DateTime.Now, null, FakeHash);
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

        [Test, Category("Database")]
        public void TestFailedOperationCounter()
        {
            using (Database database = new Database(DatabasePath))
            {
                string path = Path.Combine(RootPath,"1.test");
                string path2 = Path.Combine(RootPath,"2.test");
                CreateTestFile(path, 1);
                CreateTestFile(path2, 1);
                File.SetLastWriteTimeUtc(path,File.GetLastWriteTimeUtc(path).Subtract(TimeSpan.FromMinutes(1)));
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 0);
                database.SetOperationRetryCounter(path, 1, Database.OperationType.UPLOAD);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 1);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 0);
                database.SetOperationRetryCounter(path, 2, Database.OperationType.UPLOAD);
                database.SetOperationRetryCounter(path, 4, Database.OperationType.CHANGE);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 2);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 4);
                database.SetOperationRetryCounter(path, 3, Database.OperationType.DOWNLOAD);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 3);
                database.SetOperationRetryCounter(path, 5, Database.OperationType.DELETE);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 5);
                database.SetOperationRetryCounter(path, 0, Database.OperationType.DELETE);
                CreateTestFile(path, 1);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 0);
                database.SetOperationRetryCounter(path, 1, Database.OperationType.UPLOAD);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 1);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 0);
                database.SetOperationRetryCounter(path2, 2, Database.OperationType.UPLOAD);
                database.DeleteAllFailedOperations(path);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 0);
                database.SetOperationRetryCounter(path, 1, Database.OperationType.UPLOAD);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 1);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 0);
                Assert.True(database.GetOperationRetryCounter(path2, Database.OperationType.UPLOAD) == 2);
                Assert.True(database.GetOperationRetryCounter(path2, Database.OperationType.DOWNLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path2, Database.OperationType.CHANGE) == 0);
                Assert.True(database.GetOperationRetryCounter(path2, Database.OperationType.DELETE) == 0);
                database.DeleteAllFailedOperations();
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path2, Database.OperationType.UPLOAD) == 0);
                database.SetOperationRetryCounter(path, -1, Database.OperationType.UPLOAD);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 0);
                database.SetOperationRetryCounter(path, 1, Database.OperationType.UPLOAD);
                database.SetOperationRetryCounter(path, 1, Database.OperationType.CHANGE);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 1);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 1);
                database.SetOperationRetryCounter(path, 1, Database.OperationType.DOWNLOAD);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 1);
                database.SetOperationRetryCounter(path, 1, Database.OperationType.DELETE);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 1);
                Console.WriteLine("bla");
                database.SetOperationRetryCounter(path, 2, Database.OperationType.UPLOAD);
                database.SetOperationRetryCounter(path, 3, Database.OperationType.CHANGE);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 2);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 3);
                database.SetOperationRetryCounter(path, 4, Database.OperationType.DOWNLOAD);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 4);
                database.SetOperationRetryCounter(path, 5, Database.OperationType.DELETE);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 5);
                database.DeleteAllFailedOperations();
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.UPLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DOWNLOAD) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.CHANGE) == 0);
                Assert.True(database.GetOperationRetryCounter(path, Database.OperationType.DELETE) == 0);
            }
        }*/

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
