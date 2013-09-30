using System;
using System.IO;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;

using NUnit.Framework;


namespace TestLibrary
{
    [TestFixture]
    class ChunkedStreamTest
    {
        private readonly string TestFilePath = Path.Combine(ConfigManager.CurrentConfig.FoldersPath, "ChunkedStreamTest.txt");
        private readonly string DatabasePath = Path.Combine(ConfigManager.CurrentConfig.FoldersPath, "ChunkedStreamTest.cmissync");
        private readonly int ChunkSize = 1024;

        [TestFixtureSetUp]
        public void ClassInit()
        {
            //File.Delete(ConfigManager.CurrentConfig.GetLogFilePath());
            //log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
        }

        [SetUp]
        public void TestInit()
        {
            File.Delete(DatabasePath);
            File.Delete(TestFilePath);
        }

        private void FillArray<T>(T[] array, T value)
        {
            for (int i = 0; i < array.Length; ++i)
            {
                array[i] = value;
            }
        }

        private bool EqualArray<T>(T[] array1, T[] array2, int size)
        {
            for (int i = 0; i < size && i < array1.Length && i < array2.Length; ++i)
            {
                if (!array1[i].Equals(array2[i]))
                {
                    return false;
                }
            }
            return true;
        }

        [Test]
        public void TestSeek()
        {
            //Assert.Fail("TODO");
        }

        [Test]
        public void TestWrite()
        {
            //using (Database database = new Database(DatabasePath))
            using (Stream file = new FileStream(TestFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            using (ChunkedStream chunked = new ChunkedStream(file, ChunkSize))
            {
                byte[] buffer = new byte[2 * ChunkSize];
                FillArray<byte>(buffer, (byte)'a');


                Assert.AreEqual(0, chunked.ChunkPosition);
                Assert.AreEqual(0, chunked.Position);
                Assert.AreEqual(0, chunked.Length);

                chunked.Write(buffer, 0, 1);
                Assert.AreEqual(1, file.Position);
                Assert.AreEqual(1, chunked.Position);
                Assert.AreEqual(1, chunked.Length);

                System.ArgumentOutOfRangeException e = Assert.Catch<System.ArgumentOutOfRangeException>(() => chunked.Write(buffer, 0, ChunkSize));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(ChunkSize, e.ActualValue);
                Assert.AreEqual(1, file.Position);
                Assert.AreEqual(1, chunked.Position);
                Assert.AreEqual(1, chunked.Length);

                chunked.Write(buffer, 1, ChunkSize - 1);
                Assert.AreEqual(ChunkSize, file.Position);
                Assert.AreEqual(ChunkSize, chunked.Position);
                Assert.AreEqual(ChunkSize, chunked.Length);

                e = Assert.Catch<System.ArgumentOutOfRangeException>(() => chunked.Write(buffer, 0, 1));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(1, e.ActualValue);
                Assert.AreEqual(ChunkSize, file.Position);
                Assert.AreEqual(ChunkSize, chunked.Position);
                Assert.AreEqual(ChunkSize, chunked.Length);


                chunked.ChunkPosition = ChunkSize;
                Assert.AreEqual(ChunkSize, chunked.ChunkPosition);
                Assert.AreEqual(ChunkSize, file.Position);
                Assert.AreEqual(0, chunked.Position);
                Assert.AreEqual(0, chunked.Length);

                chunked.Write(buffer, 0, ChunkSize);
                Assert.AreEqual(2 * ChunkSize, file.Position);
                Assert.AreEqual(ChunkSize, chunked.Position);
                Assert.AreEqual(ChunkSize, chunked.Length);

                e = Assert.Catch<System.ArgumentOutOfRangeException>(() => chunked.Write(buffer, 0, 1));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(1, e.ActualValue);
                Assert.AreEqual(2 * ChunkSize, file.Position);
                Assert.AreEqual(ChunkSize, chunked.Position);
                Assert.AreEqual(ChunkSize, chunked.Length);

                
                chunked.ChunkPosition = 4 * ChunkSize;
                Assert.AreEqual(4 * ChunkSize, chunked.ChunkPosition);
                Assert.AreEqual(4 * ChunkSize, file.Position);
                Assert.AreEqual(0, chunked.Position);
                Assert.AreEqual(0, chunked.Length);

                chunked.Write(buffer, 1, ChunkSize - 1);
                Assert.AreEqual(5 * ChunkSize - 1, file.Position);
                Assert.AreEqual(ChunkSize - 1, chunked.Position);
                Assert.AreEqual(ChunkSize - 1, chunked.Length);

                e = Assert.Catch<System.ArgumentOutOfRangeException>(() => chunked.Write(buffer, 0, ChunkSize));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(ChunkSize, e.ActualValue);
                Assert.AreEqual(5 * ChunkSize - 1, file.Position);
                Assert.AreEqual(ChunkSize - 1, chunked.Position);
                Assert.AreEqual(ChunkSize - 1, chunked.Length);

                chunked.Write(buffer, 0, 1);
                Assert.AreEqual(5 * ChunkSize, file.Position);
                Assert.AreEqual(ChunkSize, chunked.Position);
                Assert.AreEqual(ChunkSize, chunked.Length);

                e = Assert.Catch<System.ArgumentOutOfRangeException>(() => chunked.Write(buffer, 0, 1));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(1, e.ActualValue);
                Assert.AreEqual(5 * ChunkSize, file.Position);
                Assert.AreEqual(ChunkSize, chunked.Position);
                Assert.AreEqual(ChunkSize, chunked.Length);
            }
        }

        [Test]
        public void TestRead()
        {
            //using (Database database = new Database(DatabasePath))
            {
                using (Stream file = File.OpenWrite(TestFilePath))
                {
                    byte[] buffer = new byte[ChunkSize];

                    FillArray<byte>(buffer, (byte)'1');
                    file.Write(buffer, 0, ChunkSize);

                    FillArray<byte>(buffer, (byte)'2');
                    file.Write(buffer, 0, ChunkSize);

                    FillArray<byte>(buffer, (byte)'3');
                    file.Write(buffer, 0, 3);
                }

                using (Stream file = new FileStream(TestFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                using (ChunkedStream chunked = new ChunkedStream(file, ChunkSize))
                {
                    byte[] buffer = new byte[ChunkSize];
                    byte[] result = new byte[ChunkSize];


                    Assert.AreEqual(0, chunked.ChunkPosition);
                    Assert.AreEqual(0, chunked.Position);
                    Assert.AreEqual(ChunkSize, chunked.Length);

                    FillArray<byte>(buffer, (byte)'1');

                    Assert.AreEqual(1, chunked.Read(result, 0, 1));
                    Assert.IsTrue(EqualArray(buffer, result, 1));
                    Assert.AreEqual(0, chunked.ChunkPosition);
                    Assert.AreEqual(1, chunked.Position);
                    Assert.AreEqual(ChunkSize, chunked.Length);

                    Assert.AreEqual(ChunkSize - 1, chunked.Read(result, 1, ChunkSize));
                    Assert.IsTrue(EqualArray(buffer, result, ChunkSize));
                    Assert.AreEqual(0, chunked.ChunkPosition);
                    Assert.AreEqual(ChunkSize, chunked.Position);
                    Assert.AreEqual(ChunkSize, chunked.Length);

                    Assert.AreEqual(0, chunked.Read(result, 0, ChunkSize));
                    Assert.AreEqual(0, chunked.ChunkPosition);
                    Assert.AreEqual(ChunkSize, chunked.Position);
                    Assert.AreEqual(ChunkSize, chunked.Length);


                    chunked.ChunkPosition = 2 * ChunkSize;
                    Assert.AreEqual(2 * ChunkSize, chunked.ChunkPosition);
                    Assert.AreEqual(0, chunked.Position);
                    Assert.AreEqual(3, chunked.Length);

                    FillArray<byte>(buffer, (byte)'3');

                    Assert.AreEqual(3, chunked.Read(result, 0, ChunkSize));
                    Assert.IsTrue(EqualArray(buffer, result, 3));
                    Assert.AreEqual(2 * ChunkSize, chunked.ChunkPosition);
                    Assert.AreEqual(3, chunked.Position);
                    Assert.AreEqual(3, chunked.Length);


                    chunked.ChunkPosition = ChunkSize;
                    Assert.AreEqual(ChunkSize, chunked.ChunkPosition);
                    Assert.AreEqual(0, chunked.Position);
                    Assert.AreEqual(ChunkSize, chunked.Length);

                    FillArray<byte>(buffer, (byte)'2');

                    for (int i = 0; i < ChunkSize; ++i)
                    {
                        Assert.AreEqual(1, chunked.Read(result, i, 1));
                    }
                    Assert.IsTrue(EqualArray(buffer, result, ChunkSize));
                    Assert.AreEqual(ChunkSize, chunked.ChunkPosition);
                    Assert.AreEqual(ChunkSize, chunked.Position);
                    Assert.AreEqual(ChunkSize, chunked.Length);
                }
            }
        }

        [Test]
        public void TestWriteResume()
        {
            //Assert.Fail("TODO");
        }

        [Test]
        public void TestReadResume()
        {
            //Assert.Fail("TODO");
        }
    }




}
