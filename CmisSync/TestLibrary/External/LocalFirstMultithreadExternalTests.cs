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
    public class LocalFirstMultithreadExternalTests : AbstractExternalTests
    {
        // Referenced here because NUnit does not manage to find TestServers in parent class.
        public static IEnumerable<object[]> TestServers = AbstractExternalTests.TestServers;

        [Test, TestCaseSource("TestServers"), Category("Slow")]
        public void LocalFolderMoves(string ignoredCanonicalName, string ignoredLocalPath, string remoteFolderPath,
string url, string user, string password, string repositoryId)
        {
            // Prepare remote folder and CmisSync process.
            ISession session = ClearRemoteCMISFolderAndGetSession(url, user, password, repositoryId, remoteFolderPath);
            sync = new CmisSyncProcess(remoteFolderPath, url, user, password, repositoryId);

            // Create second local folder.
            string f1 = "folder 1";
            string f1a = "folder 1 a";
            string f2 = "folder 2";
            string f2a = "folder 2 a";

            Directory.CreateDirectory(Path.Combine(sync.Folder(), f1));
            Directory.CreateDirectory(Path.Combine(sync.Folder(), f1a));
            Directory.CreateDirectory(Path.Combine(sync.Folder(), f1, f2));
            Directory.CreateDirectory(Path.Combine(sync.Folder(), f1, f2a));

            // Loop for about 1 minute.

            // Wait for just 1 second
            Thread.Sleep(15 * 1000);

            // Move f2 outside of f1
            Directory.Move(Path.Combine(sync.Folder(), f2), sync.Folder());
            // Check
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
    }
}
