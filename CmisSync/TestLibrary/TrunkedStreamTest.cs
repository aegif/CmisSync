using System;
using System.IO;

using CmisSync.Lib;
using CmisSync.Lib.Cmis;

using NUnit.Framework;


namespace TestLibrary
{
    [TestFixture]
    class TrunkedStreamTest
    {
        private readonly string TestFilePath = Path.Combine(ConfigManager.CurrentConfig.FoldersPath, "TrunkedStreamTest.txt");
        private readonly string DatabasePath = Path.Combine(ConfigManager.CurrentConfig.FoldersPath, "TrunkedStreamTest.cmissync");
        private readonly int TrunkSize = 1024;

        [TestFixtureSetUp]
        public void ClassInit()
        {
            File.Delete(ConfigManager.CurrentConfig.GetLogFilePath());
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
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
            using (TrunkedStream trunked = new TrunkedStream(file, TrunkSize))
            {
                byte[] buffer = new byte[2 * TrunkSize];
                FillArray<byte>(buffer, (byte)'a');


                Assert.AreEqual(0, trunked.TrunkPosition);
                Assert.AreEqual(0, trunked.Position);
                Assert.AreEqual(0, trunked.Length);

                trunked.Write(buffer, 0, 1);
                Assert.AreEqual(1, file.Position);
                Assert.AreEqual(1, trunked.Position);
                Assert.AreEqual(1, trunked.Length);

                System.ArgumentOutOfRangeException e = Assert.Catch<System.ArgumentOutOfRangeException>(() => trunked.Write(buffer, 0, TrunkSize));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(TrunkSize, e.ActualValue);
                Assert.AreEqual(1, file.Position);
                Assert.AreEqual(1, trunked.Position);
                Assert.AreEqual(1, trunked.Length);

                trunked.Write(buffer, 1, TrunkSize - 1);
                Assert.AreEqual(TrunkSize, file.Position);
                Assert.AreEqual(TrunkSize, trunked.Position);
                Assert.AreEqual(TrunkSize, trunked.Length);

                e = Assert.Catch<System.ArgumentOutOfRangeException>(() => trunked.Write(buffer, 0, 1));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(1, e.ActualValue);
                Assert.AreEqual(TrunkSize, file.Position);
                Assert.AreEqual(TrunkSize, trunked.Position);
                Assert.AreEqual(TrunkSize, trunked.Length);


                trunked.TrunkPosition = TrunkSize;
                Assert.AreEqual(TrunkSize, trunked.TrunkPosition);
                Assert.AreEqual(TrunkSize, file.Position);
                Assert.AreEqual(0, trunked.Position);
                Assert.AreEqual(0, trunked.Length);

                trunked.Write(buffer, 0, TrunkSize);
                Assert.AreEqual(2 * TrunkSize, file.Position);
                Assert.AreEqual(TrunkSize, trunked.Position);
                Assert.AreEqual(TrunkSize, trunked.Length);

                e = Assert.Catch<System.ArgumentOutOfRangeException>(() => trunked.Write(buffer, 0, 1));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(1, e.ActualValue);
                Assert.AreEqual(2 * TrunkSize, file.Position);
                Assert.AreEqual(TrunkSize, trunked.Position);
                Assert.AreEqual(TrunkSize, trunked.Length);

                
                trunked.TrunkPosition = 4 * TrunkSize;
                Assert.AreEqual(4 * TrunkSize, trunked.TrunkPosition);
                Assert.AreEqual(4 * TrunkSize, file.Position);
                Assert.AreEqual(0, trunked.Position);
                Assert.AreEqual(0, trunked.Length);

                trunked.Write(buffer, 1, TrunkSize - 1);
                Assert.AreEqual(5 * TrunkSize - 1, file.Position);
                Assert.AreEqual(TrunkSize - 1, trunked.Position);
                Assert.AreEqual(TrunkSize - 1, trunked.Length);

                e = Assert.Catch<System.ArgumentOutOfRangeException>(() => trunked.Write(buffer, 0, TrunkSize));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(TrunkSize, e.ActualValue);
                Assert.AreEqual(5 * TrunkSize - 1, file.Position);
                Assert.AreEqual(TrunkSize - 1, trunked.Position);
                Assert.AreEqual(TrunkSize - 1, trunked.Length);

                trunked.Write(buffer, 0, 1);
                Assert.AreEqual(5 * TrunkSize, file.Position);
                Assert.AreEqual(TrunkSize, trunked.Position);
                Assert.AreEqual(TrunkSize, trunked.Length);

                e = Assert.Catch<System.ArgumentOutOfRangeException>(() => trunked.Write(buffer, 0, 1));
                Assert.AreEqual("count", e.ParamName);
                Assert.AreEqual(1, e.ActualValue);
                Assert.AreEqual(5 * TrunkSize, file.Position);
                Assert.AreEqual(TrunkSize, trunked.Position);
                Assert.AreEqual(TrunkSize, trunked.Length);
            }
        }

        [Test]
        public void TestRead()
        {
            //using (Database database = new Database(DatabasePath))
            {
                using (Stream file = File.OpenWrite(TestFilePath))
                {
                    byte[] buffer = new byte[TrunkSize];

                    FillArray<byte>(buffer, (byte)'1');
                    file.Write(buffer, 0, TrunkSize);

                    FillArray<byte>(buffer, (byte)'2');
                    file.Write(buffer, 0, TrunkSize);

                    FillArray<byte>(buffer, (byte)'3');
                    file.Write(buffer, 0, 3);
                }

                using (Stream file = new FileStream(TestFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                using (TrunkedStream trunked = new TrunkedStream(file, TrunkSize))
                {
                    byte[] buffer = new byte[TrunkSize];
                    byte[] result = new byte[TrunkSize];


                    Assert.AreEqual(0, trunked.TrunkPosition);
                    Assert.AreEqual(0, trunked.Position);
                    Assert.AreEqual(TrunkSize, trunked.Length);

                    FillArray<byte>(buffer, (byte)'1');

                    Assert.AreEqual(1, trunked.Read(result, 0, 1));
                    Assert.IsTrue(EqualArray(buffer, result, 1));
                    Assert.AreEqual(0, trunked.TrunkPosition);
                    Assert.AreEqual(1, trunked.Position);
                    Assert.AreEqual(TrunkSize, trunked.Length);

                    Assert.AreEqual(TrunkSize - 1, trunked.Read(result, 1, TrunkSize));
                    Assert.IsTrue(EqualArray(buffer, result, TrunkSize));
                    Assert.AreEqual(0, trunked.TrunkPosition);
                    Assert.AreEqual(TrunkSize, trunked.Position);
                    Assert.AreEqual(TrunkSize, trunked.Length);

                    Assert.AreEqual(0, trunked.Read(result, 0, TrunkSize));
                    Assert.AreEqual(0, trunked.TrunkPosition);
                    Assert.AreEqual(TrunkSize, trunked.Position);
                    Assert.AreEqual(TrunkSize, trunked.Length);


                    trunked.TrunkPosition = 2 * TrunkSize;
                    Assert.AreEqual(2 * TrunkSize, trunked.TrunkPosition);
                    Assert.AreEqual(0, trunked.Position);
                    Assert.AreEqual(3, trunked.Length);

                    FillArray<byte>(buffer, (byte)'3');

                    Assert.AreEqual(3, trunked.Read(result, 0, TrunkSize));
                    Assert.IsTrue(EqualArray(buffer, result, 3));
                    Assert.AreEqual(2 * TrunkSize, trunked.TrunkPosition);
                    Assert.AreEqual(3, trunked.Position);
                    Assert.AreEqual(3, trunked.Length);


                    trunked.TrunkPosition = TrunkSize;
                    Assert.AreEqual(TrunkSize, trunked.TrunkPosition);
                    Assert.AreEqual(0, trunked.Position);
                    Assert.AreEqual(TrunkSize, trunked.Length);

                    FillArray<byte>(buffer, (byte)'2');

                    for (int i = 0; i < TrunkSize; ++i)
                    {
                        Assert.AreEqual(1, trunked.Read(result, i, 1));
                    }
                    Assert.IsTrue(EqualArray(buffer, result, TrunkSize));
                    Assert.AreEqual(TrunkSize, trunked.TrunkPosition);
                    Assert.AreEqual(TrunkSize, trunked.Position);
                    Assert.AreEqual(TrunkSize, trunked.Length);
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
