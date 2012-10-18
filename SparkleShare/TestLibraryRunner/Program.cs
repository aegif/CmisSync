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
            new ConnectionTests().DotCmisToIBMConnections();

#if DEBUG
            Console.WriteLine("Press any key to close...");
            Console.ReadLine();
#endif
        }
    }
}
