using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TestLibrary
{
    class LocalFilesystemActivityGenerator
    {
        private static int id = 0;

        public static void GenerateActivity(string path)
        {
            CreateRandomFile(path);
            CreateRandomFile(path);
            CreateRandomFile(path);
            CreateRandomFile(path);
            CreateRandomFile(path);
            string path1 = Path.Combine(path, "dir1");
            Directory.CreateDirectory(path1);
            CreateRandomFile(path1);
            CreateRandomFile(path1);
            CreateRandomFile(path1);
            CreateRandomFile(path1);
            CreateRandomFile(path1);
            // TODO destroy directory while upload is going on
        }

        public static void CreateRandomFile(string path)
        {
            Random rng = new Random();
            int sizeInKb = rng.Next(3000);
            string filename = "file_" + id++ + ".bin";

            const int blockSize = 1024 * 8;
            const int blocksPerKb = 1024 / blockSize;
            byte[] data = new byte[blockSize];
            using (FileStream stream = File.OpenWrite(path))
            {
                // There 
                for (int i = 0; i < sizeInKb * blocksPerKb; i++)
                {
                    rng.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }
        }
    }
}
