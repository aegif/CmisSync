using System;
using CmisSync.Lib;

namespace CmisSync.Console
{
    // Console UI does not actually need a controller, so we use this empty controller instead.
	public class ConsoleController : IActivityListener
	{
        /// <summary>
        /// Constructor
        /// </summary>
		public ConsoleController ()
		{
            // Intentionally empty.
		}

        /// <summary>
        /// Activity has started.
        /// </summary>
		public void ActivityStarted()
		{
			System.Console.WriteLine("ActivityStarted");
		}

        /// <summary>
        /// Activity has stopped.
        /// </summary>
 		public void ActivityStopped()
		{
			System.Console.WriteLine("ActivityStopped");
		}

        /// <summary>
        /// Activity error occured.
        /// </summary>
        public void ActivityError(Tuple<string, Exception> error)
        {
            System.Console.WriteLine(String.Format("Could not sync '{0}': {1}", error.Item1, error.Item2.Message));
        }
    }

}

