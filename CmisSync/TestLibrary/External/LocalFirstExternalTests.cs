using System.Collections.Generic;
using System.IO;
using System.Text;
using DotCMIS.Client;
using DotCMIS.Data.Impl;
using NUnit.Framework;
using System.Threading;
using DotCMIS.Exceptions;
using TestLibrary.External;

/**
 * Tests for the CmisSync.exe program that do something locally and then check the result on the remote server.
 */
namespace TestLibrary
{
    [TestFixture]
    public class LocalFirstExternalTests : AbstractExternalTests
    {
        // Referenced here because NUnit does not manage to find TestServers in parent class.
        public static IEnumerable<object[]> TestServers = AbstractExternalTests.TestServers;

        [Test, TestCaseSource("TestServers"), Category("Fast")]
        public void LocalFileCreation(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            new LocalFile(url, user, password, repositoryId, remoteFolderPath, sync, this);
        }


        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void LocalFileAppend(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            ISession session = ClearRemoteCMISFolderAndGetSession(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Local file creation

            // Create random small file.
            int sizeKb = 3;
            string filename = "file.doc";
            LocalFilesystemActivityGenerator.CreateFile(Path.Combine(sync.Folder(), filename), sizeKb);

            // Check that file is present server-side.
            Thread.Sleep(20 * 1000);
            IDocument doc = null;
            try
            {
                doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            Assert.AreEqual(sizeKb * 1024, doc.ContentStreamLength);
            Assert.AreEqual("application/msword", doc.ContentStreamMimeType);
            // TODO compare date Assert.AreEqual(, doc.);
            string docId = doc.Id;

            // Local file append

            // Append to file
            string dataToAppend = "let's add a bit more data to that file";
            byte[] bytes = Encoding.UTF8.GetBytes(dataToAppend);
            using (var fileStream = new FileStream(Path.Combine(sync.Folder(), filename), FileMode.Append, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fileStream))
            {
                bw.Write(bytes);
            }

            // Check on server
            Thread.Sleep(10 * 1000);
            try
            {
                doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            Assert.True(1 - (1024 * sizeKb + dataToAppend.Length) / doc.ContentStreamLength < 0.1); // Text file size can vary a bit
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void LocalFolderCreation(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            ISession session = ClearRemoteCMISFolderAndGetSession(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create local folder.
            string foldername = "folder 1";
            Directory.CreateDirectory(Path.Combine(sync.Folder(), foldername));

            // Check on server
            Thread.Sleep(10 * 1000);
            IFolder folder = null;
            try
            {
                folder = (IFolder)session.GetObjectByPath(remoteFolderPath + "/" + foldername, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Folder failed to get synced to the server.", e);
            }
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void LocalFileDeletion(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            ISession session = ClearRemoteCMISFolderAndGetSession(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create the file
            LocalFile localFile = new LocalFile(url, user, password, repositoryId, remoteFolderPath, sync, this);

            // FIXME Check on server. Or even create the file on server.

            // Delete the file.
            File.Delete(Path.Combine(sync.Folder(), localFile.FullPath));

            // Check that file is not present server-side.
            Thread.Sleep(15 * 1000);
            IDocument doc = null;
            try
            {
                doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + localFile.Filename, true);

                // Should have failed.
                Assert.Fail("Deleted document still exists on server: " + doc);
            }
            catch (CmisObjectNotFoundException e)
            {
                // That's the correct outcome
            }
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void LocalFileRename(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            ISession session = ClearRemoteCMISFolderAndGetSession(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            ////////////////////////////////////////////// Local file creation

            // Create random small file.
            int sizeKb = 3;
            string filename = "file.doc";
            LocalFilesystemActivityGenerator.CreateFile(Path.Combine(sync.Folder(), filename), sizeKb);

            // Wait a few seconds so that sync gets a chance to sync things.
            Thread.Sleep(20 * 1000);

            // Check that file is present server-side.
            IDocument doc = null;
            try
            {
                doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            Assert.AreEqual(sizeKb * 1024, doc.ContentStreamLength);
            Assert.AreEqual("application/msword", doc.ContentStreamMimeType);
            // TODO compare date Assert.AreEqual(, doc.);
            string docId = doc.Id;

            ////////////////////////////////////////////// Local file rename

            // Rename the document
            string newFilename = "moved_" + filename;
            File.Move(Path.Combine(sync.Folder(), filename), Path.Combine(sync.Folder(), newFilename));
            filename = newFilename;

            // Check that the remote document has been renamed, and still has the same object id.
            Thread.Sleep(10 * 1000);
            doc = null;
            try
            {
                doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            string newDocId = doc.Id;

            // Current status: The following line sometimes fails, sometimes succeeds. Probably a race condition.
            Assert.AreEqual(docId, newDocId, "The remote document's id has changed when the local file was renamed: " + docId + " -> " + newDocId);
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void _CurrentlyFailing_LocalFileMove(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            ISession session = ClearRemoteCMISFolderAndGetSession(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            ////////////////////////////////////////////// Local file creation

            // Create random small file.
            int sizeKb = 3;
            string filename = "file.doc";
            LocalFilesystemActivityGenerator.CreateFile(Path.Combine(sync.Folder(), filename), sizeKb);

            // Wait a few seconds so that sync gets a chance to sync things.
            Thread.Sleep(20 * 1000);

            // Check that file is present server-side.
            IDocument doc = null;
            try
            {
                doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            Assert.AreEqual(sizeKb * 1024, doc.ContentStreamLength);
            Assert.AreEqual("application/msword", doc.ContentStreamMimeType);
            // TODO compare date Assert.AreEqual(, doc.);
            string docId = doc.Id;

            ////////////////////////////////////////////// Local folder creation

            // Create local folder.
            string foldername = "folder 1";
            Directory.CreateDirectory(Path.Combine(sync.Folder(), foldername));

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(20 * 1000);

            IFolder folder = null;
            try
            {
                folder = (IFolder)session.GetObjectByPath(remoteFolderPath + "/" + foldername, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Folder failed to get synced to the server.", e);
            }

            // Move local file to other folder

            string destinationFilename = foldername + Path.DirectorySeparatorChar + filename;
            File.Move(Path.Combine(sync.Folder(), filename), Path.Combine(sync.Folder(), destinationFilename));

            // Check that the remote document has been renamed, and still has the same object id.
            Thread.Sleep(20 * 1000);
            doc = null;
            try
            {
                doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + foldername + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get moved on server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            string newDocId = doc.Id;

            // Current status: Fails.
            Assert.AreEqual(docId, newDocId, "The remote document's id has changed when the local file was moved: " + docId + " -> " + newDocId);
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void _CurrentlyFailing_LocalFolderMove(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            ISession session = ClearRemoteCMISFolderAndGetSession(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            ////////////////////////////////////////////// Move local folder

            // Create second local folder.
            string foldername1 = "folder 1";
            string foldername2 = "folder 2";
            Directory.CreateDirectory(Path.Combine(sync.Folder(), foldername1));
            Directory.CreateDirectory(Path.Combine(sync.Folder(), foldername2));

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(15 * 1000);

            string id = null;
            try
            {
                IFolder folder = (IFolder)session.GetObjectByPath(remoteFolderPath + "/" + foldername2, true);
                id = folder.Id;
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Folder failed to get synced to the server.", e);
            }

            // Move folder 2 into folder 1
            Directory.Move(Path.Combine(sync.Folder(), foldername2), Path.Combine(sync.Folder(), foldername1, foldername2));

            // Check on server.
            Thread.Sleep(10 * 1000);
            string newId = null;
            try
            {
                IFolder folder = (IFolder)session.GetObjectByPath(remoteFolderPath + "/" + foldername1 + "/" + foldername2, true);
                newId = folder.Id;
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Folder failed to get synced to the server.", e);
            }

            // Currently fails
            Assert.AreEqual(newId, id, "The remote folder's id has changed when the local folder was moved: " + id + " -> " + newId);
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void LocalFileCreationWithOnlyWatcher(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder.
            ISession session = ClearRemoteCMISFolderAndGetSession(url, user, password, repositoryId, remoteFolderPath);

            // Month-long sync interval, meaning that in fact only the watcher can trigger syncs.
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId, CmisSyncProcess.MONTH_LONG_SYNC_INTERVAL);

            // Wait a few seconds so that sync gets a chance to sync things.
            Thread.Sleep(20 * 1000);

            ////////////////////////////////////////////// Local file creation

            // Create random small file.
            int sizeKb = 3;
            string filename = "file.doc";
            LocalFilesystemActivityGenerator.CreateFile(Path.Combine(sync.Folder(), filename), sizeKb);

            // Check that file is present server-side.
            Thread.Sleep(20 * 1000);
            IDocument doc = null;
            try
            {
                doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + filename, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(doc);
            Assert.AreEqual(filename, doc.ContentStreamFileName);
            Assert.AreEqual(filename, doc.Name);
            Assert.AreEqual(sizeKb * 1024, doc.ContentStreamLength);
            Assert.AreEqual("application/msword", doc.ContentStreamMimeType);
            // TODO compare date Assert.AreEqual(, doc.);
            string docId = doc.Id;
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void _CurrentlyFailing_TwoCmisSync(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

            // Create syncs.
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);
            sync2 = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create local folder on 1.
            string foldername = "Subfolder0";
            Directory.CreateDirectory(Path.Combine(sync.Folder(), foldername));

            // Check on 2.
            Thread.Sleep(2 * 10 * 1000);
            Assert.True(Directory.Exists(Path.Combine(sync2.Folder(), foldername)));
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void Conflict(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            ISession session = ClearRemoteCMISFolderAndGetSession(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            ////////////////////////////////////////////// Initialization

            // Create random small file.
            int sizeKb = 3;
            string filename = "file.txt";
            LocalFilesystemActivityGenerator.CreateTextFile(Path.Combine(sync.Folder(), filename), sizeKb);

            // Wait a few seconds so that sync gets a chance to sync things.
            Thread.Sleep(20 * 1000);

            sync.Suspend();

            // Wait for 10 seconds to be sure suspension happened.
            Thread.Sleep(10 * 1000);

            ////////////////////////////////////////////// Modify on remote side

            byte[] content = UTF8Encoding.UTF8.GetBytes("Modified from remote side");
            ContentStream contentStream = new ContentStream();
            contentStream.FileName = "file.txt";
            contentStream.MimeType = "text/plain";
            contentStream.Length = content.Length;
            contentStream.Stream = new MemoryStream(content);

            IDocument doc = null;
            try
            {
                doc = (IDocument)session.GetObjectByPath(remoteFolderPath + "/" + filename, true);
                doc.SetContentStream(contentStream, true);
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }

            // Modify on local side, by appending to file.
            string dataToAppend = "Modified on local side";
            byte[] bytes = Encoding.UTF8.GetBytes(dataToAppend);
            using (var fileStream = new FileStream(Path.Combine(sync.Folder(), filename), FileMode.Append, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fileStream))
            {
                bw.Write(bytes);
            }

            // Conflict
            sync.Resume();
            Thread.Sleep(10 * 1000);

            // TODO check that conflicts happened correctly.
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void Issue772(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

            // Create syncs.
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);
            sync2 = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Wait so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Disconnect 1.
            sync.Suspend();

            // Create local folder on 2.
            string foldername = "Subfolder";
            Directory.CreateDirectory(Path.Combine(sync2.Folder(), foldername));

            // Wait so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Reconnect 1.
            sync.Resume();

            // Wait so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check that the folder is present on both.
            Assert.True(Directory.Exists(Path.Combine(sync.Folder(), foldername)));
            Assert.True(Directory.Exists(Path.Combine(sync2.Folder(), foldername)));
        }

        // TODO
        // sync while text file open for edition
        // sync while .doc file open for edition by MS Word
    }
}
