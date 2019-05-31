using System;
using NUnit.Framework;
using System.Diagnostics;
using System.Threading;
using TestLibrary.External;
// using System.Windows.Automation; Requires .NET 4.5 it seems.

/**
 * Small-sized tests for the CmisSync.exe program.
 */
namespace TestLibrary
{
    [TestFixture]
    public class BasicExternalTests : AbstractExternalTests
    {
        [Test, Category("Fast")]
        public void Placebo()
        {
            Assert.AreEqual(4, 2 + 2);
        }

        [Test, Category("Fast")]
        public void StartStopGUI()
        {
            // TODO Change this to your CmisSync
            Process process = Process.Start(@"C:\Users\nico\src\CmisSync\CmisSync\Windows\bin\Debug\CmisSync.exe");
            if (null == process)
                Assert.Fail("Could not start process, maybe an existing process has been reused?");

            //Wait for CmisSync's GUI to start properly.
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
        public void StartStopConsolePerpetual()
        {
            // TODO Change this to your installed CmisSync
            Process process = Process.Start(CmisSyncProcess.CONSOLE_EXE, "-p");
            if (null == process)
                Assert.Fail("Could not start process, maybe an existing process has been reused?");

            process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => Console.WriteLine(e.Data);

            // The process should continue running perpetually.
            Thread.Sleep(1 * 1000); // Wait for 30 seconds.
            Assert.IsFalse(process.HasExited);

            // Exit the process to avoid any possible interference with subsequent tests.
            process.Kill();
        }


        [Test, Category("Fast")]
        public void StartStopConsoleNonPerpetual()
        {
            // TODO Change this to your installed CmisSync
            Process process = Process.Start(CmisSyncProcess.CONSOLE_EXE);
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
