using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestLibrary;

namespace TestLibraryRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO new CmisSyncTests().ClientSideChanges();

#if DEBUG
            Console.WriteLine("Press any key to close...");
            Console.ReadLine();
#endif
        }
    }
}
