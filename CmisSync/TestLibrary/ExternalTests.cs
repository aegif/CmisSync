using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
// using System.Windows.Automation; Requires .NET 4.5 it seems.
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data.Impl;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

/**
 * Tests for the CmisSync.exe program, called externally without any knowledge about the internals of the algorithm.
 * 
 * To use them, first create a JSON file containing the credentials/parameters to a test folder on your CMIS server(s)
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
 * Warning: All previous content of these folders will be deleted by the tests.
 */
namespace TestLibrary
{
    using System.Collections;
    using CmisSync.Lib;
    using CmisSync.Lib.Cmis;
    using CmisSync.Lib.Sync;
    using log4net.Appender;
    using NUnit.Framework;
    using CmisSync.Auth;
    using CmisSync.Lib.Database;
    using System.Diagnostics;
    using System.Threading;
    using DotCMIS.Exceptions;

    [TestFixture]
    public class ExternalTests : AbstractSyncTests
    {
        public static IEnumerable<object[]> TestServers
        {
            get
            {
                string path = "../../test-servers.json";

                if (!File.Exists(path))
                {
                    path = "../CmisSync/TestLibrary/test-servers.json";
                }

                if (!File.Exists(path))
                {
                    throw new Exception("You must create a test-servers.json file before running tests, see documentation in header of SyncTests.cs");
                }

                return JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText(path));
            }
        }

        public static IEnumerable<object[]> TestServersFuzzy
        {
            get
            {
                string path = "../../test-servers-fuzzy.json";
                bool exists = File.Exists(path);

                if (!exists)
                {
                    path = "../CmisSync/TestLibrary/test-servers-fuzzy.json";
                }

                try
                {
                    return JsonConvert.DeserializeObject<List<object[]>>(
                       File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    return Enumerable.Empty<object[]>();
                }
            }
        }

        public ExternalTests()
        {
        }

        /*public static IEnumerable<AutomationElement> EnumNotificationIcons()
        {
            foreach (var button in AutomationElement.RootElement.Find(
                            "User Promoted Notification Area").EnumChildButtons())
            {
                yield return button;
            }

            foreach (var button in AutomationElement.RootElement.Find(
                          "System Promoted Notification Area").EnumChildButtons())
            {
                yield return button;
            }

            var chevron = AutomationElement.RootElement.Find("Notification Chevron");
            if (chevron != null && chevron.InvokeButton())
            {
                foreach (var button in AutomationElement.RootElement.Find(
                                   "Overflow Notification Area").EnumChildButtons())
                {
                    yield return button;
                }
            }
        }*/

        [Test, Category("Fast")]
        public void Placebo()
        {
            Assert.AreEqual(4, 2 + 2);
        }

        [Test, Category("Fast")]
        public void StartStopUI()
        {
            // TODO Change this to your CmisSync
            Process process = Process.Start(@"C:\Users\nico\src\CmisSync\CmisSync\Windows\bin\Debug\CmisSync.exe");
            if (null == process)
                Assert.Fail("Could not start process, maybe an existing process has been reused?");

            //Wait for CmisSync's UI to start properly.
            Thread.Sleep(2000);

            /*foreach (var icon in EnumNotificationIcons())
            {
                var name = icon.GetCurrentPropertyValue(AutomationElement.NameProperty) as string;
                Console.WriteLine(name);
                if (name.StartsWith("CmisSync"))
                {
                    Console.WriteLine(@"Click!");
                    icon.InvokeButton();
                    break;
                }
            }*/

            // Close as if the user had clicked "Exit".
            process.Kill(); // More violent than we would want.
            //process.Close(); // Only close the connection to the program, not the program itself.
            //process.CloseMainWindow(); // Does not work because CmisSync lives as a tray icon rather than a window.

            // Wait for CmisSync to finish what it is doing and exit normally. This might take a few minutes if a big sync was going on.
            process.WaitForExit();
            Console.Write("Exiting StartStopUI");
        }


        // TODO Change this to your CmisSync, either debug or installed version.
        private string CONSOLE_EXE = @"C:\Users\nico\src\CmisSync\CmisSync.Console\bin\Debug\CmisSync.Console.exe";


        [Test, Category("Fast")]
        public void StartStopConsolePerpetual()
        {
            // TODO Change this to your installed CmisSync
            Process process = Process.Start(CONSOLE_EXE, "-p");
            if (null == process)
                Assert.Fail("Could not start process, maybe an existing process has been reused?");

            process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);

            // The process should continue running perpetually.
            Thread.Sleep(30 * 1000); // Wait for 30 seconds.
            Assert.IsFalse(process.HasExited);

            // Exit the process to avoid any possible interference with subsequent tests.
            process.Kill();
        }


        [Test, Category("Fast")]
        public void StartStopConsoleNonPerpetual()
        {
            // TODO Change this to your installed CmisSync
            Process process = Process.Start(CONSOLE_EXE);
            if (null == process)
                Assert.Fail("Could not start process, maybe an existing process has been reused?");

            process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);

            // Wait for CmisSync to finish what it is doing and exit normally. This might take a few minutes if a big sync was going on.
            process.WaitForExit();
            Console.Write("Exiting StartStopConsole");
        }


        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void Real(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

            // Create temporary folder for configuration and data files.
            string tempFolder = Path.Combine(Path.GetTempPath(), "CmisSync_test_" + Path.GetRandomFileName());
            Console.Write("Test folder: " + tempFolder);
            string configurationFolder = Path.Combine(tempFolder, "configuration");
            string localDataFolder = Path.Combine(tempFolder, "data");
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(configurationFolder);
            Directory.CreateDirectory(localDataFolder);

            // Create XML config file.
            string customConfigPath = Path.Combine(configurationFolder, "config.xml");
            string logPath = Path.Combine(configurationFolder, "debug_log.txt");
            string customConfig = File.ReadAllText(@"../../config.xml");
            // Replace variables in template
            customConfig = customConfig.Replace("{LOG}", logPath);
            customConfig = customConfig.Replace("{LOCAL_FOLDER}", localDataFolder);
            customConfig = customConfig.Replace("{REMOTE_FOLDER}", remoteFolderPath);
            customConfig = customConfig.Replace("{URL}", url);
            customConfig = customConfig.Replace("{USER}", user);
            customConfig = customConfig.Replace("{PASSWORD}", password);
            customConfig = customConfig.Replace("{REPOSITORY}", repositoryId);
            // Write config file.
            File.WriteAllText(customConfigPath, customConfig);

            // Start CmiSync.
            string arguments = "-p -c " + customConfigPath;
            Console.Write("Executing: " + CONSOLE_EXE + " " + arguments);
            Process process = Process.Start(CONSOLE_EXE, arguments);
            if (null == process)
                Assert.Fail("Could not start process, maybe an existing process has been reused?");
            process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);

            ////////////////////////////////////////////// File creation

            // Create random small file.
            int sizeKb = 3;
            string filename = "file.doc";
            LocalFilesystemActivityGenerator.CreateFile(Path.Combine(localDataFolder, filename), sizeKb);

            // Wait a few seconds so that sync gets a chance to sync things.
            Thread.Sleep(20 * 1000);

            // Check that file is present server-side.
            IDocument doc = null;
            try {
                doc = (IDocument)CreateSession(url, user, password, repositoryId)
                    .GetObjectByPath(remoteFolderPath + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e) {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            Assert.AreEqual(sizeKb * 1024, doc.ContentStreamLength);
            Assert.AreEqual("application/msword", doc.ContentStreamMimeType);
            // TODO compare date Assert.AreEqual(, doc.);
            string docId = doc.Id;

            ////////////////////////////////////////////// File append

            // Append to file
            string dataToAppend = "let's add a bit more data to that file";
            byte[] bytes = Encoding.UTF8.GetBytes(dataToAppend);
            using (var fileStream = new FileStream(Path.Combine(localDataFolder, filename), FileMode.Append, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fileStream))
            {
                bw.Write(bytes);
            }

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            try
            {
                doc = (IDocument)CreateSession(url, user, password, repositoryId)
                    .GetObjectByPath(remoteFolderPath + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            Assert.AreEqual(1024 * sizeKb + dataToAppend.Length, doc.ContentStreamLength);


            ////////////////////////////////////////////// Folder creation

            // Create local folder.
            string foldername = "folder 1";
            Directory.CreateDirectory(Path.Combine(localDataFolder, foldername));

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            IFolder folder = null;
            try
            {
                folder = (IFolder)CreateSession(url, user, password, repositoryId)
                    .GetObjectByPath(remoteFolderPath + "/" + foldername, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Folder failed to get synced to the server.", e);
            }


            ////////////////////////////////////////////// File rename

            // Rename the document
            string newFilename = "moved_" + filename;
            File.Move(Path.Combine(localDataFolder, filename), Path.Combine(localDataFolder, newFilename));
            filename = newFilename;

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check that the remote document has been renamed, and still has the same object id.
            doc = null;
            try
            {
                doc = (IDocument)CreateSession(url, user, password, repositoryId)
                    .GetObjectByPath(remoteFolderPath + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            string newDocId = doc.Id;
            //FIXME            Assert.AreEqual(docId, newDocId, "The remote document's id has changed when the local file was renamed: " + docId + " -> " + newDocId);

            ////////////////////////////////////////////// File delete

            // Delete the file.
            File.Delete(Path.Combine(localDataFolder, filename));

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check that file is not present server-side.
            doc = null;
            try
            {
                doc = (IDocument)CreateSession(url, user, password, repositoryId)
                    .GetObjectByPath(remoteFolderPath + "/" + filename, true);

                // Should have failed.
                Assert.Fail("Deleted document still exists on server: " + doc);
            }
            catch (CmisObjectNotFoundException e)
            {
                // That's the correct outcome
            }



            // Exit the process to avoid any possible interference with subsequent tests.
            process.Kill(); // TODO does not always get killed.
        }

        /// <summary>
        /// Delete all objects contained in a remote CMIS folder.
        /// </summary>
        private void ClearRemoteCMISFolder(string url, string user, string password, string repositoryId, string remoteFolderPath)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = url;
            parameters[SessionParameter.User] = user;
            parameters[SessionParameter.Password] = password;
            parameters[SessionParameter.RepositoryId] = repositoryId;

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.CreateSession(parameters);

            var folder = (IFolder)session.GetObjectByPath(remoteFolderPath, true);
            foreach (var child in folder.GetChildren())
            {
                child.Delete(true);
            }
        }

        public string CreateTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

    }
}
