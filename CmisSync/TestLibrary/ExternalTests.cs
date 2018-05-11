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

    [TestFixture]
    public class ExternalTests
    {
        public static IEnumerable<object[]> TestServers
        {
            get
            {
                string path = "../../test-servers.json";
                bool exists = File.Exists(path);

                if (!exists)
                {
                    path = "../CmisSync/TestLibrary/test-servers.json";
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
            Process process = Process.Start(@"C:\Users\win7pro32bit\Documents\GitHub\CmisSync\CmisSync\Windows\bin\Debug\CmisSync.exe");
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


        [Test, Category("Fast")]
        public void StartStopConsole()
        {
            // TODO Change this to your CmisSync
            Process process = Process.Start(@"C:\Users\win7pro32bit\Documents\GitHub\CmisSync\CmisSync.Console\bin\Debug\CmisSync.Console.exe");
            if (null == process)
                Assert.Fail("Could not start process, maybe an existing process has been reused?");

            process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);

            // Wait for CmisSync to finish what it is doing and exit normally. This might take a few minutes if a big sync was going on.
            process.WaitForExit();
            Console.Write("Exiting StartStopConsole");
        }
    }
}
