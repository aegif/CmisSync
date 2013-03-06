//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Threading;

using CmisSync.Lib;

namespace CmisSync {

    // This is CmisSync!
    public class Program {

        public static Controller Controller;
        public static UI UI;

        private static Mutex program_mutex = new Mutex (false, "CmisSync");
     
        #if !__MonoCS__
        [STAThread]
        #endif
        public static void Main (string [] args)
        {
			Console.WriteLine("start");
            if (args.Length != 0 && !args [0].Equals ("start") &&
                Backend.Platform != PlatformID.MacOSX &&
                Backend.Platform != PlatformID.Win32NT) {

                string n = Environment.NewLine;

                Console.WriteLine (n +
                    "CmisSync is a collaboration and sharing tool that is" + n +
                    "designed to keep things simple and to stay out of your way." + n +
                    n +
                    "Version: " + CmisSync.Lib.Backend.Version + n +
                    "Copyright (C) 2010 Hylke Bons" + n +
                    "This program comes with ABSOLUTELY NO WARRANTY." + n +
                    n +
                    "This is free software, and you are welcome to redistribute it" + n +
                    "under certain conditions. Please read the GNU GPLv3 for details." + n +
                    n +
                    "Usage: CmisSync [start|stop|restart]");
                Environment.Exit (-1);
            }

			// Only allow one instance of CmisSync (on Windows)
			if (!program_mutex.WaitOne (0, false)) {
				Console.WriteLine ("CmisSync is already running.");
				Environment.Exit (-1);
			}

//#if !DEBUG
            try {
//#endif
                Controller = new Controller ();
                Controller.Initialize ();

                UI = new UI ();
                UI.Run ();
            
//#if !DEBUG
            } catch (Exception e) {
				Console.WriteLine("Exception in Program.Main");
                Logger.WriteCrashReport (e);
                Environment.Exit (-1);
            }
//#endif

            #if !__MonoCS__
            // Suppress assertion messages in debug mode
            GC.Collect (GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers ();
            #endif
        }
    }
}
