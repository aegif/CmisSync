using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TestLibrary;
using Newtonsoft.Json;
using System.IO;

// Useful to debug unit tests.
namespace TestLibraryRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            int serverId = 2; // Which server in test-servers.json (first=0)

            IEnumerable<object[]> servers = JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText("../../../TestLibrary/test-servers.json"));
            object[] server = servers.ElementAt(serverId);
            //new CmisSyncTests().ClientSideSmallFileAddition((string)server[0], (string)server[1],
            //    (string)server[2], (string)server[3], (string)server[4], (string)server[5], (string)server[6]);

// Let the console open.
#if DEBUG
            Console.WriteLine("Press any key to close...");
            Console.ReadLine();
#endif
        }
    }
}
