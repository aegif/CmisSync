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
    using TestLibrary.External;
    using DotCMIS.Enums;

    [TestFixture]
    public abstract class AbstractExternalTests : AbstractSyncTests
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

        /// <summary>
        /// Sync processes referenced by these variables get killed during teardown.
        /// Please use them to make sure that no sync process subsist after a test.
        /// </summary>
        protected CmisSyncProcess sync, sync2;

        [TearDown]
        public void TearDown()
        {
            // Exit the sync process to avoid any possible interference with subsequent tests.
            if (sync != null)
            {
                sync.Kill();
                sync = null;
            }
            if (sync2 != null)
            {
                sync2.Kill();
                sync2 = null;
            }

            // Run normal teardown too.
            base.TearDown();
        }

        /// <summary>
        /// Delete all objects contained in a remote CMIS folder.
        /// </summary>
        /// <returns>
        /// The cleared folder, ready to use for testing.
        /// </returns>
        protected IFolder ClearRemoteCMISFolder(string url, string user, string password, string repositoryId, string remoteFolderPath)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = url;
            parameters[SessionParameter.User] = user;
            parameters[SessionParameter.Password] = password;
            parameters[SessionParameter.RepositoryId] = repositoryId;

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.CreateSession(parameters);

            return ClearRemoteCMISFolderAndGetFolder(session, remoteFolderPath);
        }

        /// <summary>
        /// Delete all objects contained in a remote CMIS folder.
        /// </summary>
        /// <returns>
        /// The CMIS session, usable for testing.
        /// </returns>
        protected ISession ClearRemoteCMISFolderAndGetSession(string url, string user, string password, string repositoryId, string remoteFolderPath)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = url;
            parameters[SessionParameter.User] = user;
            parameters[SessionParameter.Password] = password;
            parameters[SessionParameter.RepositoryId] = repositoryId;

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.CreateSession(parameters);

            ClearRemoteCMISFolderAndGetFolder(session, remoteFolderPath);

            return session;
        }

        private IFolder ClearRemoteCMISFolderAndGetFolder(ISession session, string remoteFolderPath)
        {
            var folder = (IFolder)session.GetObjectByPath(remoteFolderPath, true);
            foreach (var child in folder.GetChildren())
            {
                if (child is IFolder)
                {
                    IFolder folderChild = (IFolder)child;
                    folderChild.DeleteTree(true, UnfileObject.Delete, true);
                }
                else
                {
                    child.Delete(true);
                }
            }

            return folder;
        }

        /*public string CreateTemporaryDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }*/

    }
}
