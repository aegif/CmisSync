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

        public static void CreateDirectoryAndRandomFiles(string path)
        {
            CreateRandomFile(path, 3);
            CreateRandomFile(path, 3);
            CreateRandomFile(path, 3);
            CreateRandomFile(path, 3);
            CreateRandomFile(path, 3);
            string path1 = Path.Combine(path, "dir1");
            Directory.CreateDirectory(path1);
            CreateRandomFile(path1, 3);
            CreateRandomFile(path1, 3);
            CreateRandomFile(path1, 3);
            CreateRandomFile(path1, 3);
            CreateRandomFile(path1, 3);
        }

        public static void CreateRandomFile(string path, int maxSizeInKb)
        {
            Random rng = new Random();
            int sizeInKb = 1 + rng.Next(maxSizeInKb);
            string filename = "file_" + id++ + ".bin";
            byte[] data = new byte[1024];

            using (FileStream stream = File.OpenWrite(Path.Combine(path, filename)))
            {
                // Write random data
                for (int i = 0; i < sizeInKb; i++)
                {
                    rng.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }
        }
    }
}
