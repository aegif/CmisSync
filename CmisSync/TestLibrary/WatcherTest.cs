﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

using CmisSync.Lib;


namespace TestLibrary
{
    using NUnit.Framework;

    /// <summary></summary>
    [TestFixture]
    public class WatcherTest
    {
        private static readonly string TestFolderParent = Directory.GetCurrentDirectory();
        private static readonly string TestFolder = Path.Combine(TestFolderParent, "test");
        private static readonly int NormalNumber = 10;
        private static readonly int HeavyNumber = 10000;
        private static readonly int FileInFolderNumber = 1000;

        private static int TestNumber;

        [TestFixtureSetUp]
        public void ClassInit()
        {
#if __MonoCS__
            Environment.SetEnvironmentVariable("MONO_MANAGED_WATCHER", "enabled");
#endif
            log4net.Config.XmlConfigurator.Configure(ConfigManager.CurrentConfig.GetLog4NetConfig());
        }

        [SetUp]
        public void TestInit()
        {
            Directory.CreateDirectory(TestFolder);
            WaitWatcher();
        }

        [TearDown]
        public void TestCleanup()
        {
            if (Directory.Exists(TestFolder))
            {
                Directory.Delete(TestFolder, true);
                Console.WriteLine("Deleted TestFolder");
            }
            File.Delete(oldnameOut);
            File.Delete(newnameOut);
        }

        [Test, Category("Fast")]
        public void TestEnableRaisingEvents()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                CreateTestFile(1);
                WaitWatcher();
                Assert.AreEqual(0, watcher.GetChangeList().Count);

                watcher.EnableRaisingEvents = true;

                CreateTestFile(2);
                string name = GetPathname();
                WaitWatcher(40000,watcher,1);
                Assert.AreEqual(1, watcher.GetChangeList().Count);
                Assert.AreEqual(name, watcher.GetChangeList()[0]);

                CreateTestFile(3);
                name = GetPathname();
                WaitWatcher(40000,watcher,2);
                Assert.AreEqual(2, watcher.GetChangeList().Count);
                Assert.AreEqual(name, watcher.GetChangeList()[1]);

                watcher.EnableRaisingEvents = false;

                CreateTestFile(4);
                WaitWatcher();
                Assert.AreEqual(2, watcher.GetChangeList().Count);

                watcher.EnableRaisingEvents = true;

                CreateTestFile(5);
                name = GetPathname();
                WaitWatcher(40000,watcher,3);
                Assert.AreEqual(3, watcher.GetChangeList().Count);
                Assert.AreEqual(name, watcher.GetChangeList()[2]);
            }
        }

        /// <summary></summary>
        public class FileSystemEventCount
        {
            /// <summary></summary>
            public int Count { get; private set; }

            public void OnFileSystemEvent(object e, FileSystemEventArgs args)
            {
                Count += 1;
            }
        };

        [Test, Category("Fast")]
        public void TestEnableEvent()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                FileSystemEventCount count = new FileSystemEventCount();
                watcher.ChangeEvent += count.OnFileSystemEvent;

                CreateTestFile(1);
                WaitWatcher();
                Assert.AreEqual(0, count.Count);

                watcher.EnableRaisingEvents = true;

                CreateTestFile(2);
                WaitWatcher();
                Assert.AreEqual(0, count.Count);

                watcher.EnableRaisingEvents = false;
                watcher.EnableEvent = true;

                CreateTestFile(3);
                WaitWatcher();
                Assert.AreEqual(0, count.Count);

                watcher.EnableRaisingEvents = true;

                CreateTestFile(4);
                WaitWatcher(40000, count, (c) =>
                {
                    return c.Count >= 1;
                });
                Assert.LessOrEqual(1, count.Count);
                int number = count.Count;

                CreateTestFile(5);
                WaitWatcher(40000, count, (c) =>
                {
                    return c.Count >= number + 1;
                });
                Assert.LessOrEqual(number + 1, count.Count);
                watcher.EnableEvent = false;
                number = count.Count;


                CreateTestFile(6);
                Assert.AreEqual(number, count.Count);
                number = count.Count;

                watcher.EnableEvent = true;

                CreateTestFile(7);
                WaitWatcher(40000, count, (c) =>
                {
                    return c.Count >= number + 1;
                });
                Assert.LessOrEqual(number + 1, count.Count);
            }
        }

        [Test, Category("Fast")]
        public void TestRemove()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;

                List<string> names = new List<string>();
                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile();
                    names.Add(GetPathname());
                }
                WaitWatcher(40000,watcher,NormalNumber);
                Assert.AreEqual(NormalNumber, watcher.GetChangeList().Count);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.AreEqual(NormalNumber - i, watcher.GetChangeList().Count);
                    if (i % 2 == 0)
                    {
                        watcher.RemoveChange(names[i]);
                    }
                    else
                    {
                        watcher.RemoveChange(names[i],Watcher.ChangeTypes.Created);
                    }
                }
                names.Clear();

                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile();
                }
                WaitWatcher(40000,watcher,NormalNumber);
                Assert.AreEqual(NormalNumber, watcher.GetChangeList().Count);
                watcher.RemoveAll();
                Assert.AreEqual(0, watcher.GetChangeList().Count);
            }
        }

        [Test, Category("Fast")]
        public void TestRemoveInsert()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                for (int i = 0; i < NormalNumber; ++i)
                {
                    watcher.InsertChange(i.ToString(), Watcher.ChangeTypes.None);
                }
                Assert.AreEqual(0, watcher.GetChangeList().Count);

                for (int i = 0; i < NormalNumber; ++i)
                {
                    watcher.InsertChange(i.ToString(), Watcher.ChangeTypes.Created);
                }
                Assert.AreEqual(NormalNumber, watcher.GetChangeList().Count);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.AreEqual(i.ToString(), watcher.GetChangeList()[i]);
                    Assert.AreEqual(Watcher.ChangeTypes.Created, watcher.GetChangeType(i.ToString()));
                }

                for (int i = 0; i < NormalNumber; ++i)
                {
                    watcher.InsertChange(i.ToString(), Watcher.ChangeTypes.Deleted);
                }
                Assert.AreEqual(NormalNumber, watcher.GetChangeList().Count);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.AreEqual(i.ToString(), watcher.GetChangeList()[i]);
                    Assert.AreEqual(Watcher.ChangeTypes.Created, watcher.GetChangeType(i.ToString()));
                }

                for (int i = 0; i < NormalNumber; ++i)
                {
                    watcher.RemoveChange(i.ToString(), Watcher.ChangeTypes.Deleted);
                }
                Assert.AreEqual(NormalNumber, watcher.GetChangeList().Count);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.AreEqual(i.ToString(), watcher.GetChangeList()[i]);
                    Assert.AreEqual(Watcher.ChangeTypes.Created, watcher.GetChangeType(i.ToString()));
                }

                for (int i = 0; i < NormalNumber; ++i)
                {
                    watcher.RemoveChange(i.ToString(), Watcher.ChangeTypes.Created);
                }
                Assert.AreEqual(0, watcher.GetChangeList().Count);
            }
        }

        [Test, Category("Fast")]
        public void TestChangeTypeCreated()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;

                List<string> names = new List<string>();
                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile();
                    names.Add(GetPathname());
                }
                WaitWatcher(40000, watcher, (w) =>
                {
                    return watcher.GetChangeType(names[NormalNumber -1]) == Watcher.ChangeTypes.Created;
                });
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.AreEqual(
                        Watcher.ChangeTypes.Created,
                        watcher.GetChangeType(names[i]));
                }
            }
        }

        [Test, Category("Slow")]
        [Ignore]
        public void TestChangeTypeCreatedHeavy()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;

                List<string> names = new List<string>();
                for (int i = 0; i < HeavyNumber; ++i)
                {
                    CreateTestFile(0, i / FileInFolderNumber);
                    names.Add(GetPathname(i / FileInFolderNumber));
                }
                Assert.IsTrue(watcher.EnableRaisingEvents);
                int totalNumber = HeavyNumber + (HeavyNumber - 1) / FileInFolderNumber;
                WaitWatcher(60000,watcher,totalNumber);
                Assert.AreEqual(totalNumber, watcher.GetChangeList().Count);
                for (int i = 0; i < HeavyNumber; ++i)
                {
#if __MonoCS__
                    List<Watcher.ChangeTypes> types = new List<Watcher.ChangeTypes>();
                    types.Add(Watcher.ChangeTypes.Created);
                    types.Add(Watcher.ChangeTypes.Changed);
                    Assert.Contains(
                        watcher.GetChangeType(names[i]), types);
#else
                    Assert.AreEqual(
                        watcher.GetChangeType(names[i]),
                        Watcher.ChangeTypes.Created,
                        names[i]);
#endif
                }
            }
        }

        [Test, Category("Fast")]
        public void TestChangeTypeChanged()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;

                List<string> names = new List<string>();
                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile();
                    names.Add(GetPathname());
                }
                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile(names[i], i + 1);
                }
                WaitWatcher(40000,watcher,NormalNumber);
                WaitWatcher(40000,watcher,(w)=>
                {
#if __MonoCS__
                    List<Watcher.ChangeTypes> types = new List<Watcher.ChangeTypes>();
                    types.Add(Watcher.ChangeTypes.Created);
                    types.Add(Watcher.ChangeTypes.Changed);
#endif
                    for (int i = 0; i < NormalNumber; ++i)
                    {
#if __MonoCS__
                        if (!types.Contains(w.GetChangeType(names[i])))
#else
                        if (w.GetChangeType(names[i]) != Watcher.ChangeTypes.Changed)
#endif
                        {
                            return false;
                        }
                    }
                    return true;
                });
                List<string> changeList = watcher.GetChangeList();
                Assert.AreEqual(NormalNumber, changeList.Count);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.Contains(names[i], changeList);
#if __MonoCS__
                    List<Watcher.ChangeTypes> types = new List<Watcher.ChangeTypes>();
                    types.Add(Watcher.ChangeTypes.Created);
                    types.Add(Watcher.ChangeTypes.Changed);
                    Assert.Contains(
                        watcher.GetChangeType(names[i]), types);
#else
                    Assert.AreEqual(
                        watcher.GetChangeType(names[i]),
                        Watcher.ChangeTypes.Changed);
#endif
                }
            }
        }

        [Test, Category("Slow")]
        [Ignore]
        public void TestChangeTypeChangedHeavy()
        {
            //Assert.Fail("TODO");
        }

        [Test, Category("Fast")]
        public void TestChangeTypeDeleted()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;

                List<string> names = new List<string>();
                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile();
                    names.Add(GetPathname());
                }
                WaitWatcher(40000,watcher,NormalNumber);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    File.Delete(names[i]);
                }
                WaitWatcher(40000,watcher,(w) =>
                {
                    return w.GetChangeType(names[NormalNumber-1])
                        == Watcher.ChangeTypes.Deleted;
                });
                List<string> changeList = watcher.GetChangeList();
                Assert.AreEqual(NormalNumber, changeList.Count);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.Contains(names[i], changeList);
                    Assert.AreEqual(
                        watcher.GetChangeType(names[i]),
                        Watcher.ChangeTypes.Deleted);
                }
            }
        }

        [Test, Category("Slow")]
        [Ignore]
        public void TestChangeTypeDeleteHeavy()
        {
            //Assert.Fail("TODO");
        }

        [Test, Category("Fast")]
        public void TestChangeTypeNone()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                Assert.AreEqual(Watcher.ChangeTypes.None, watcher.GetChangeType(GetPathname()));
            }
        }

        //Filenames for move tests
        private static readonly string oldnameOut = Path.Combine(TestFolderParent, "test.old");
        private static readonly string newnameOut = Path.Combine(TestFolderParent, "test.new");
        private static readonly string oldname = Path.Combine(TestFolder, "test.old");
        private static readonly string newname = Path.Combine(TestFolder, "test.new");

        [Test, Category("Fast")]
        public void TestChangeTypeForMoveInsideSyncedFolder()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldname, 1);
                WaitWatcher(40000,watcher,1);
                File.Move(oldname, newname);
                WaitWatcher(40000,watcher,2);
                WaitWatcher(40000,watcher,(w)=>
                {
                    return w.GetChangeType(oldname) == Watcher.ChangeTypes.Deleted;
                });
                List<string> changeList = watcher.GetChangeList();
                Assert.AreEqual(2, changeList.Count);
                Assert.Contains(oldname, changeList);
                Assert.Contains(newname, changeList);
                Assert.AreEqual(Watcher.ChangeTypes.Deleted, watcher.GetChangeType(oldname));
                Assert.AreEqual(Watcher.ChangeTypes.Created, watcher.GetChangeType(newname));
            }
        }

        [Test, Category("Fast")]
        public void TestChangeTypeForMoveIntoSyncedFolder()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldnameOut, 1);
                WaitWatcher();
                File.Move(oldnameOut, newname);
                WaitWatcher(40000,watcher,1);
                Assert.AreEqual(1, watcher.GetChangeList().Count);
                Assert.AreEqual(newname, watcher.GetChangeList()[0]);
                Assert.AreEqual(Watcher.ChangeTypes.Created, watcher.GetChangeType(newname));
            }
        }

        [Test, Category("Fast")]
        public void TestChangeTypeForMoveOutOfSyncedFolder()
        {

            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldname, 1);
                WaitWatcher(40000,watcher,(w) => 
                {
#if __MonoCS__
                    List<Watcher.ChangeTypes> types = new List<Watcher.ChangeTypes>();
                    types.Add(Watcher.ChangeTypes.Created);
                    types.Add(Watcher.ChangeTypes.Changed);
                    return types.Contains(w.GetChangeType(oldname));
#else
                    return w.GetChangeType(oldname) == Watcher.ChangeTypes.Changed;
#endif
                });
                File.Move(oldname, newnameOut);
                Console.WriteLine("moved:"+oldname);
                WaitWatcher(40000,watcher,(w) => 
                {
                    return w.GetChangeType(oldname) == Watcher.ChangeTypes.Deleted;
                });
                Assert.AreEqual(1, watcher.GetChangeList().Count);
                Assert.AreEqual(oldname, watcher.GetChangeList()[0]);
                Assert.AreEqual(Watcher.ChangeTypes.Deleted, watcher.GetChangeType(oldname));
            }
        }

        [Test, Category("Fast")]
        public void TestChangeTypeForMoveInNotSyncedFolder()
        {

            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldnameOut, 1);
                WaitWatcher();
                File.Move(oldnameOut, newnameOut);
                WaitWatcher();
                Assert.AreEqual(0, watcher.GetChangeList().Count);
            }
        }

        [Test, Category("Slow")]
        [Ignore]
        public void TestChangeTypeForMoveHeavy()
        {
            //Assert.Fail("TODO");
        }

        [Test, Category("Fast")]
        [Ignore]
        public void TestChangeTypeMix()
        {
            //Assert.Fail("TODO");
        }

        [Test, Category("Slow")]
        [Ignore]
        public void TestChangeTypeMixHeavy()
        {
            //Assert.Fail("TODO");
        }

        private string GetNextPathname(int level)
        {
            return GetPathname(Interlocked.Increment(ref TestNumber), level);
        }

        private string GetPathname(int level = 0)
        {
            return GetPathname(TestNumber, level);
        }

        private string GetPathname(int number,int level)
        {
            string pathname = TestFolder;
            for (int i = 1; i <= level; ++i)
            {
                pathname = System.IO.Path.Combine(pathname, String.Format("folder-{0}", i));
                if (!Directory.Exists(pathname))
                {
                    Directory.CreateDirectory(pathname);
                }
            }
            return System.IO.Path.Combine(pathname, String.Format("test-{0}.bin", number));
        }

        private void CreateTestFile(string name, int sizeInKB)
        {
            Random random = new Random();
            byte[] data = new byte[1024];

            using (FileStream stream = File.OpenWrite(name))
            {
                // Write random data
                for (int i = 0; i < sizeInKB; i++)
                {
                    random.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        private void CreateTestFile(int sizeInKB = 0, int level = 0)
        {
            Random random = new Random();
            byte[] data = new byte[1024];

            using (FileStream stream = File.OpenWrite(GetNextPathname(level)))
            {
                // Write random data
                for (int i = 0; i < sizeInKB; i++)
                {
                    random.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        public static void WaitWatcher(int milliseconds = 10)
        {
#if __MonoCS__
            milliseconds = milliseconds * 10;
#endif
            Thread.Sleep(milliseconds);
        }

        /// <summary></summary>
        /// <param name="milliseconds"></param>
        /// <param name="watcher"></param>
        /// <param name="expect"></param>
        public static void WaitWatcher(int milliseconds, Watcher watcher, int expect)
        {
#if __MonoCS__
            milliseconds = milliseconds * 10;
#endif
            while (milliseconds >= 0)
            {
                if (watcher.GetChangeList().Count >= expect)
                {
                    return;
                }
                Thread.Sleep(10);
                milliseconds = milliseconds - 10;
            }
            Console.WriteLine("Timeout");
        }

        /// <summary></summary>
        /// <param name="milliseconds"></param>
        /// <param name="watcher"></param>
        /// <param name="checkStop"></param>
        public static void WaitWatcher(int milliseconds, Watcher watcher, Func<Watcher,bool> checkStop)
        {
#if __MonoCS__
            milliseconds = milliseconds * 10;
#endif
            while (milliseconds >= 0)
            {
                if (checkStop(watcher))
                {
                    return;
                }
                Thread.Sleep(10);
                milliseconds = milliseconds - 10;
            }
            Console.WriteLine("Timeout");
        }

        public static void WaitWatcher(int milliseconds, FileSystemEventCount count, Func<FileSystemEventCount,bool> checkStop)
        {
#if __MonoCS__
            milliseconds = milliseconds * 10;
#endif
            while (milliseconds >= 0)
            {
                if (checkStop(count))
                {
                    return;
                }
                Thread.Sleep(10);
                milliseconds = milliseconds - 10;
            }
            Console.WriteLine("Timeout");
        }
    }
}
