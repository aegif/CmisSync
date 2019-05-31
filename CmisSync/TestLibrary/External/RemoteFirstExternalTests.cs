using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Data.Impl;
using NUnit.Framework;
using System.Threading;
using TestLibrary.External;

/**
 * Tests for the CmisSync.exe program that do something on the remote server and then check the result locally.
 */
namespace TestLibrary
{
    [TestFixture]
    public class RemoteFirstExternalTests : AbstractExternalTests
    {
        // Referenced here because NUnit does not manage to find TestServers in parent class.
        public static IEnumerable<object[]> TestServers = AbstractExternalTests.TestServers;

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteDocumentCreation(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            ////////////////////////////////////////////// Remote file creation

            string[] remoteFilenames = { "document.txt", "いろんなカタチが、見えてくる。", "مشاور", "コンサルティングपरामर्शदाता컨설턴트" };
            foreach (string remoteFilename in remoteFilenames)
            {
                // Create remote file
                IFolder remoteBaseFolder = (IFolder)CreateSession(url, user, password, repositoryId).GetObjectByPath(remoteFolderPath, true);
                IDictionary<string, object> properties = new Dictionary<string, object>();
                properties[PropertyIds.Name] = remoteFilename;
                properties[PropertyIds.ObjectTypeId] = "cmis:document";

                byte[] creationContent = UTF8Encoding.UTF8.GetBytes("Hello World!");
                ContentStream creationContentStream = new ContentStream();
                creationContentStream.FileName = remoteFilename;
                creationContentStream.MimeType = "text/plain";
                creationContentStream.Length = creationContent.Length;
                creationContentStream.Stream = new MemoryStream(creationContent);

                remoteBaseFolder.CreateDocument(properties, creationContentStream, null);

                // Wait for 10 seconds so that sync gets a chance to sync things.
                Thread.Sleep(10 * 1000);

                // Check locally
                Assert.True(File.Exists(Path.Combine(sync.Folder(), (String)properties[PropertyIds.Name])));
            }
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteDocumentDeletion(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            ////////////////////////////////////////////// Remote file creation

            string remoteFilename = "document.txt";
            // Create remote file
            IFolder remoteBaseFolder = (IFolder)CreateSession(url, user, password, repositoryId).GetObjectByPath(remoteFolderPath, true);
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = remoteFilename;
            properties[PropertyIds.ObjectTypeId] = "cmis:document";

            byte[] creationContent = UTF8Encoding.UTF8.GetBytes("Hello World!");
            ContentStream creationContentStream = new ContentStream();
            creationContentStream.FileName = remoteFilename;
            creationContentStream.MimeType = "text/plain";
            creationContentStream.Length = creationContent.Length;
            creationContentStream.Stream = new MemoryStream(creationContent);

            IDocument document = remoteBaseFolder.CreateDocument(properties, creationContentStream, null);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.True(File.Exists(Path.Combine(sync.Folder(), (String)properties[PropertyIds.Name])));

            document.DeleteAllVersions();

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.False(File.Exists(Path.Combine(sync.Folder(), (String)properties[PropertyIds.Name])));
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteDocumentModification(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create remote file
            string filename = "document.txt";
            IFolder remoteBaseFolder = (IFolder)CreateSession(url, user, password, repositoryId).GetObjectByPath(remoteFolderPath, true);
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = filename;
            properties[PropertyIds.ObjectTypeId] = "cmis:document";

            byte[] creationContent = UTF8Encoding.UTF8.GetBytes("Hello World!");
            ContentStream creationContentStream = new ContentStream();
            creationContentStream.FileName = filename;
            creationContentStream.MimeType = "text/plain";
            creationContentStream.Length = creationContent.Length;
            creationContentStream.Stream = new MemoryStream(creationContent);

            IDocument doc = remoteBaseFolder.CreateDocument(properties, creationContentStream, null);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.True(File.Exists(Path.Combine(sync.Folder(), (String)properties[PropertyIds.Name])));

            // Modify remote document.
            doc = (IDocument)CreateSession(url, user, password, repositoryId)
                .GetObjectByPath(remoteFolderPath + "/" + filename, true);

            string contentString = "Hello World, edited.";
            byte[] content = UTF8Encoding.UTF8.GetBytes(contentString);
            ContentStream contentStream = new ContentStream();
            contentStream.FileName = filename;
            contentStream.MimeType = "text/plain";
            contentStream.Length = content.Length;
            contentStream.Stream = new MemoryStream(content);

            doc.SetContentStream(contentStream, true);

            // Wait so that sync gets a chance to sync things.
            Thread.Sleep(30 * 1000);

            // Check local file
            string remoteContent = File.ReadAllText(Path.Combine(sync.Folder(), filename));
            Assert.AreEqual(contentString, remoteContent);
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteDocumentRename(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Remote file creation

            string filename = "document.txt";
            // Create remote file
            IFolder remoteBaseFolder = (IFolder)CreateSession(url, user, password, repositoryId).GetObjectByPath(remoteFolderPath, true);
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = filename;
            properties[PropertyIds.ObjectTypeId] = "cmis:document";

            byte[] creationContent = UTF8Encoding.UTF8.GetBytes("Hello World!");
            ContentStream creationContentStream = new ContentStream();
            creationContentStream.FileName = filename;
            creationContentStream.MimeType = "text/plain";
            creationContentStream.Length = creationContent.Length;
            creationContentStream.Stream = new MemoryStream(creationContent);

            IDocument document = remoteBaseFolder.CreateDocument(properties, creationContentStream, null);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.True(File.Exists(Path.Combine(sync.Folder(), (String)properties[PropertyIds.Name])));

            string newFilename = "renamed.txt";
            //document.Move(remoteBaseFolder, folder);
            document.Rename(newFilename);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.False(File.Exists(Path.Combine(sync.Folder(), filename))); // Currently fails, local document is not renamed, even though document is correctly renamed on server.
            Assert.True(File.Exists(Path.Combine(sync.Folder(), newFilename)));
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteFolderCreation(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create remote folder
            string foldername = "folder1";
            IFolder remoteBaseFolder = (IFolder)CreateSession(url, user, password, repositoryId).GetObjectByPath(remoteFolderPath, true);
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = foldername;
            properties[PropertyIds.ObjectTypeId] = "cmis:folder";

            IFolder folder = remoteBaseFolder.CreateFolder(properties);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.True(Directory.Exists(Path.Combine(sync.Folder(), (String)properties[PropertyIds.Name])));
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteFolderDeletion(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create remote folder
            string foldername = "folder1";
            IFolder remoteBaseFolder = (IFolder)CreateSession(url, user, password, repositoryId).GetObjectByPath(remoteFolderPath, true);
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = foldername;
            properties[PropertyIds.ObjectTypeId] = "cmis:folder";

            IFolder folder = remoteBaseFolder.CreateFolder(properties);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.True(Directory.Exists(Path.Combine(sync.Folder(), (String)properties[PropertyIds.Name])));

            // Delete the folder.
            folder.DeleteTree(true, null, true);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.False(Directory.Exists(Path.Combine(sync.Folder(), (String)properties[PropertyIds.Name])));
        }
    }
}