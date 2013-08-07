using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

using CmisSync.Lib;


namespace TestLibrary
{
    using NUnit.Framework;

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
            Directory.CreateDirectory(TestFolder);
        }

        [TestFixtureTearDown]
        public void ClassCleanup()
        {
            if (Directory.Exists(TestFolder))
            {
                Directory.Delete(TestFolder, true);
            }
        }

        [SetUp]
        public void TestInit()
        {
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
                WaitWatcher(20000,watcher,1);
                Assert.AreEqual(1, watcher.GetChangeList().Count);
                Assert.AreEqual(name, watcher.GetChangeList()[0]);

                CreateTestFile(3);
                name = GetPathname();
                WaitWatcher(20000,watcher,2);
                Assert.AreEqual(2, watcher.GetChangeList().Count);
                Assert.AreEqual(name, watcher.GetChangeList()[1]);

                watcher.EnableRaisingEvents = false;

                CreateTestFile(4);
                WaitWatcher();
                Assert.AreEqual(2, watcher.GetChangeList().Count);

                watcher.EnableRaisingEvents = true;

                CreateTestFile(5);
                name = GetPathname();
                WaitWatcher(20000,watcher,3);
                Assert.AreEqual(3, watcher.GetChangeList().Count);
                Assert.AreEqual(name, watcher.GetChangeList()[2]);
            }
        }

        class FileSystemEventCount
        {
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
                WaitWatcher(20000, count, (c) =>
                {
                    return c.Count >= 1;
                });
                Assert.LessOrEqual(1, count.Count);
                int number = count.Count;

                CreateTestFile(5);
                WaitWatcher(20000, count, (c) =>
                {
                    return c.Count >= number + 1;
                });
                Assert.LessOrEqual(number + 1, count.Count);
                number = count.Count;

                watcher.EnableEvent = false;

                CreateTestFile(6);
                Assert.AreEqual(number, count.Count);
                number = count.Count;

                watcher.EnableEvent = true;

                CreateTestFile(7);
                WaitWatcher(20000, count, (c) =>
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
                WaitWatcher(20000,watcher,NormalNumber);
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
                WaitWatcher(20000,watcher,NormalNumber);
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
                WaitWatcher(20000,watcher,NormalNumber);
                Assert.AreEqual(NormalNumber, watcher.GetChangeList().Count);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.AreEqual(names[i], watcher.GetChangeList()[i]);
                    Assert.AreEqual(
                        Watcher.ChangeTypes.Created,
                        watcher.GetChangeType((string)watcher.GetChangeList()[i]));
                }
            }
        }

        [Test, Category("Slow")]
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
                WaitWatcher(30000,watcher,totalNumber);
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
                WaitWatcher(20000,watcher,NormalNumber);
                WaitWatcher(20000,watcher,(w)=>
                {
                    for (int i = 0; i < NormalNumber; ++i)
                    {
                        if (w.GetChangeType(names[i]) != Watcher.ChangeTypes.Changed)
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
                WaitWatcher(20000,watcher,NormalNumber);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    File.Delete(names[i]);
                }
                WaitWatcher(20000,watcher,(w) =>
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

        [Test, Category("Fast")]
        public void TestChangeTypeForMove()
        {
            string oldnameOut = Path.Combine(TestFolderParent, "test.old");
            string newnameOut = Path.Combine(TestFolderParent, "test.new");
            string oldname = Path.Combine(TestFolder, "test.old");
            string newname = Path.Combine(TestFolder, "test.new");

            using (Watcher watcher = new Watcher(TestFolder))
            {
                File.Delete(newname);
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldname, 1);
                WaitWatcher(20000,watcher,1);
                File.Move(oldname, newname);
                WaitWatcher(20000,watcher,2);
                WaitWatcher(20000,watcher,(w)=>
                {
                    return w.GetChangeType(oldname) == Watcher.ChangeTypes.Deleted;
                });
                List<string> changeList = watcher.GetChangeList();
                Assert.AreEqual(2, changeList.Count);
                Assert.Contains(oldname, changeList);
                Assert.Contains(newname, changeList);
                Assert.AreEqual(Watcher.ChangeTypes.Deleted, watcher.GetChangeType(oldname));
                Assert.AreEqual(Watcher.ChangeTypes.Created, watcher.GetChangeType(newname));
                File.Delete(newname);
            }

            using (Watcher watcher = new Watcher(TestFolder))
            {
                File.Delete(newname);
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldnameOut, 1);
                WaitWatcher();
                File.Move(oldnameOut, newname);
                WaitWatcher(20000,watcher,1);
                Assert.AreEqual(1, watcher.GetChangeList().Count);
                Assert.AreEqual(newname, watcher.GetChangeList()[0]);
                Assert.AreEqual(Watcher.ChangeTypes.Created, watcher.GetChangeType(newname));
                File.Delete(newname);
            }

            using (Watcher watcher = new Watcher(TestFolder))
            {
                File.Delete(newnameOut);
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldname, 1);
                WaitWatcher(20000,watcher,1);
                File.Move(oldname, newnameOut);
                WaitWatcher(20000,watcher,(w) => 
                {
                    return w.GetChangeType(oldname) == Watcher.ChangeTypes.Deleted;
                });
                Assert.AreEqual(1, watcher.GetChangeList().Count);
                Assert.AreEqual(oldname, watcher.GetChangeList()[0]);
                Assert.AreEqual(Watcher.ChangeTypes.Deleted, watcher.GetChangeType(oldname));
                File.Delete(newnameOut);
            }

            using (Watcher watcher = new Watcher(TestFolder))
            {
                File.Delete(newnameOut);
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldnameOut, 1);
                WaitWatcher();
                File.Move(oldnameOut, newnameOut);
                WaitWatcher();
                Assert.AreEqual(0, watcher.GetChangeList().Count);
                File.Delete(newnameOut);
            }
        }

        [Test, Category("Slow")]
        public void TestChangeTypeForMoveHeavy()
        {
            //Assert.Fail("TODO");
        }

        [Test, Category("Fast")]
        public void TestChangeTypeMix()
        {
            //Assert.Fail("TODO");
        }

        [Test, Category("Slow")]
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

        private void WaitWatcher(int milliseconds = 10)
        {
            Thread.Sleep(milliseconds);
        }

        private void WaitWatcher(int milliseconds,Watcher watcher,int expect)
        {
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

        private void WaitWatcher(int milliseconds,Watcher watcher,Func<Watcher,bool> checkStop)
        {
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

        private void WaitWatcher(int milliseconds,FileSystemEventCount count,Func<FileSystemEventCount,bool> checkStop)
        {
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
