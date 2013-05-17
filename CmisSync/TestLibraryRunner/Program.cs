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
        static int serverId = 0; // Which server in the JSON file (first=0)

        static void test()
        {

            IEnumerable<object[]> servers = JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText("../../../TestLibrary/test-servers.json"));
            object[] server = servers.ElementAt(serverId);

            new CmisSyncTests().Sync((string)server[0], (string)server[1],
                (string)server[2], (string)server[3], (string)server[4], (string)server[5], (string)server[6]);
        }

        static void testFuzzy()
        {
            IEnumerable<object[]> servers = JsonConvert.DeserializeObject<List<object[]>>(
                    File.ReadAllText("../../../TestLibrary/test-servers-fuzzy.json"));
            object[] server = servers.ElementAt(serverId);
            new CmisSyncTests().GetRepositoriesFuzzy((string)server[0], (string)server[1], (string)server[2]);
        }

        static void Main(string[] args)
        {
            test();
            //testFuzzy();

            // Let the console open.
            Console.WriteLine("Press Enter to close...");
            Console.ReadLine();
        }
    }
}
