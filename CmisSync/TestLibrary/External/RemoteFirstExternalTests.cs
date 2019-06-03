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
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Remote file creations
            string[] remoteFilenames = { "document.txt", "いろんなカタチが、見えてくる。", "مشاور", "コンサルティングपरामर्शदाता컨설턴트" };
            foreach (string remoteFilename in remoteFilenames)
            {
                // Create remote file
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
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Remote file creation
            string remoteFilename = "document.txt";
            // Create remote file
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
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create remote file
            string filename = "document.txt";
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
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Remote file creation
            string filename = "document.txt";
            // Create remote file
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
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create remote folder
            string foldername = "folder1";
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
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create remote folder
            string foldername = "folder1";
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

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteDocumentMove(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create remote file
            string filename = "document.txt";
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = filename;
            properties[PropertyIds.ObjectTypeId] = "cmis:document";
            string contentString = "Hello World!";
            byte[] creationContent = UTF8Encoding.UTF8.GetBytes(contentString);
            ContentStream creationContentStream = new ContentStream();
            creationContentStream.FileName = filename;
            creationContentStream.MimeType = "text/plain";
            creationContentStream.Length = creationContent.Length;
            creationContentStream.Stream = new MemoryStream(creationContent);
            IDocument doc = remoteBaseFolder.CreateDocument(properties, creationContentStream, null);

            // Create remote folder
            string foldername = "folder1";
            IDictionary<string, object> folderProperties = new Dictionary<string, object>();
            folderProperties[PropertyIds.Name] = foldername;
            folderProperties[PropertyIds.ObjectTypeId] = "cmis:folder";
            IFolder folder = remoteBaseFolder.CreateFolder(folderProperties);

            // Check locally
            Thread.Sleep(30 * 1000);
            string syncedContent = File.ReadAllText(Path.Combine(sync.Folder(), filename));
            Assert.AreEqual(contentString, syncedContent);
            Assert.True(Directory.Exists(Path.Combine(sync.Folder(), foldername)));

            // Move document into the folder
            doc.Move(remoteBaseFolder, folder);

            // Check locally
            Thread.Sleep(20 * 1000);
            Assert.True(File.Exists(Path.Combine(sync.Folder(), foldername, filename)), "File not present where it should be");
            syncedContent = File.ReadAllText(Path.Combine(sync.Folder(), foldername, filename));
            Assert.AreEqual(contentString, syncedContent);
            Assert.False(File.Exists(Path.Combine(sync.Folder(), filename)));
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteDocumentDeletionThenSameNameFolderCreation(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Remote file creation

            string remoteObjectName = "document.or.folder";
            // Create remote file
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = remoteObjectName;
            properties[PropertyIds.ObjectTypeId] = "cmis:document";

            byte[] creationContent = UTF8Encoding.UTF8.GetBytes("Hello World!");
            ContentStream creationContentStream = new ContentStream();
            creationContentStream.FileName = remoteObjectName;
            creationContentStream.MimeType = "text/plain";
            creationContentStream.Length = creationContent.Length;
            creationContentStream.Stream = new MemoryStream(creationContent);

            IDocument document = remoteBaseFolder.CreateDocument(properties, creationContentStream, null);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.True(File.Exists(Path.Combine(sync.Folder(), remoteObjectName)));

            // Delete remote document
            document.DeleteAllVersions();

            // Immediately create remote folder with exact same name
            IDictionary<string, object> folderProperties = new Dictionary<string, object>();
            folderProperties[PropertyIds.Name] = remoteObjectName;
            folderProperties[PropertyIds.ObjectTypeId] = "cmis:folder";
            IFolder folder = remoteBaseFolder.CreateFolder(folderProperties);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.True(Directory.Exists(Path.Combine(sync.Folder(), remoteObjectName)));
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteFolderCreationThenSameNameDocumentDeletion(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Remote file creation
            string remoteObjectName = "document.or.folder";
            IDictionary<string, object> folderProperties = new Dictionary<string, object>();
            folderProperties[PropertyIds.Name] = remoteObjectName;
            folderProperties[PropertyIds.ObjectTypeId] = "cmis:folder";
            IFolder folder = remoteBaseFolder.CreateFolder(folderProperties);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.True(File.Exists(Path.Combine(sync.Folder(), remoteObjectName)));

            // Delete remote folder.
            folder.Delete(true);

            // Immediately create remote file with exact same name
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = remoteObjectName;
            properties[PropertyIds.ObjectTypeId] = "cmis:document";
            byte[] creationContent = UTF8Encoding.UTF8.GetBytes("Hello World!");
            ContentStream creationContentStream = new ContentStream();
            creationContentStream.FileName = remoteObjectName;
            creationContentStream.MimeType = "text/plain";
            creationContentStream.Length = creationContent.Length;
            creationContentStream.Stream = new MemoryStream(creationContent);
            IDocument document = remoteBaseFolder.CreateDocument(properties, creationContentStream, null);

            // Wait for 10 seconds so that sync gets a chance to sync things.
            Thread.Sleep(10 * 1000);

            // Check locally
            Assert.True(Directory.Exists(Path.Combine(sync.Folder(), remoteObjectName)));
        }

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteDocumentMetadataModification(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create remote file
            string filename = "document.txt";
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

            // Modify remote document metadata.
            doc = (IDocument)CreateSession(url, user, password, repositoryId)
                .GetObjectByPath(remoteFolderPath + "/" + filename, true);

            // TODO create custom aspect on server
            //properties[PropertyIds.] = filename;

            // Wait so that sync gets a chance to sync things.
            Thread.Sleep(30 * 1000);

            // Check local file's metadata in database
            // TODO
        }


        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void RemoteFolderRename(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            IFolder remoteBaseFolder = ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create remote folder
            string foldername = "folder1";
            IDictionary<string, object> folderProperties = new Dictionary<string, object>();
            folderProperties[PropertyIds.Name] = foldername;
            folderProperties[PropertyIds.ObjectTypeId] = "cmis:folder";
            IFolder folder = remoteBaseFolder.CreateFolder(folderProperties);

            // Create remote file inside that folder.
            string filename = "document.txt";
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = filename;
            properties[PropertyIds.ObjectTypeId] = "cmis:document";
            string contentString = "Hello World!";
            byte[] creationContent = UTF8Encoding.UTF8.GetBytes(contentString);
            ContentStream creationContentStream = new ContentStream();
            creationContentStream.FileName = filename;
            creationContentStream.MimeType = "text/plain";
            creationContentStream.Length = creationContent.Length;
            creationContentStream.Stream = new MemoryStream(creationContent);
            IDocument doc = folder.CreateDocument(properties, creationContentStream, null);

            // Check locally
            Thread.Sleep(20 * 1000);
            string syncedContent = File.ReadAllText(Path.Combine(sync.Folder(), foldername , filename));
            Assert.AreEqual(contentString, syncedContent);
            Assert.True(Directory.Exists(Path.Combine(sync.Folder(), foldername)));

            // Rename folder
            string newFoldername = "renamed folder";
            folder.Rename(newFoldername);

            // Check locally
            Thread.Sleep(20 * 1000);
            Assert.True(File.Exists(Path.Combine(sync.Folder(), newFoldername, filename)), "File not present where it should be");
            syncedContent = File.ReadAllText(Path.Combine(sync.Folder(), foldername, filename));
            Assert.AreEqual(contentString, syncedContent);
            Assert.False(File.Exists(Path.Combine(sync.Folder(), foldername, filename)), "File present where it should not be");
        }


        // TODO

        // rename folder that contains doc (note: no expectation that file is same)

        // file bigger than 4GB
        // permission change: document becomes invisible
        // permission change: document becomes visible
        // permission change: folder becomes invisible
        // permission change: folder becomes visible
        // overflow changelog buffer and see whether everything gets synchronized
    }
}