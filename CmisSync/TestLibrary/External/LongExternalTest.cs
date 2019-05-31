using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Data.Impl;
using NUnit.Framework;
using System.Threading;
using DotCMIS.Exceptions;
using TestLibrary.External;

/**
 * Long test that performs various operations over a few minutes.
 */
namespace TestLibrary
{
    [TestFixture]
    public class LongExternalTest : AbstractExternalTests
    {
        // Referenced here because NUnit does not manage to find TestServers in parent class.
        public static IEnumerable<object[]> TestServers = AbstractExternalTests.TestServers;

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void LongScenario(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
            string url, string user, string password, string repositoryId)
        {
            // Clear the remote folder.
            ClearRemoteCMISFolder(url, user, password, repositoryId, remoteFolderPath);

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
            Assert.AreEqual(sizeKb * 1024, doc.ContentStreamLength);
            Assert.AreEqual("application/msword", doc.ContentStreamMimeType);
            // TODO compare date Assert.AreEqual(, doc.);
            string docId = doc.Id;

            ////////////////////////////////////////////// Local file append

            // Append to file
            string dataToAppend = "let's add a bit more data to that file";
            byte[] bytes = Encoding.UTF8.GetBytes(dataToAppend);
            using (var fileStream = new FileStream(Path.Combine(sync.Folder(), filename), FileMode.Append, FileAccess.Write, FileShare.None))
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
            Assert.True(1 - (1024 * sizeKb + dataToAppend.Length) / doc.ContentStreamLength < 0.1); // Text file size can vary a bit

            ////////////////////////////////////////////// Local folder creation

            // Create local folder.
            string foldername = "folder 1";
            Directory.CreateDirectory(Path.Combine(sync.Folder(), foldername));

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

            ////////////////////////////////////////////// Local file delete

            // Delete the file.
            File.Delete(Path.Combine(sync.Folder(), filename));

            // Wait so that sync gets a chance to sync things.
            Thread.Sleep(15 * 1000);

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

            ////////////////////////////////////////////// Remote file creation

            string[] remoteFilenames = { filename, "いろんなカタチが、見えてくる。", "مشاور", "コンサルティングपरामर्शदाता컨설턴트" };
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

            ////////////////////////////////////////////// Remote file modification

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
    }
}
