using System;
using CmisSync.Lib;

namespace CmisSync.Console
{
	public class ConsoleController :IActivityListener
	{
		public ConsoleController ()
		{

		}

		public void ActivityStarted()
		{
			System.Console.WriteLine("ActivityStarted");
		}
		
 		public void ActivityStopped()
		{
			System.Console.WriteLine("ActivityStoppted");
		}
	}

}

