using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TestLibrary
{
    /// <summary>
    /// Generate some file system activity.
    /// </summary>
    class LocalFilesystemActivityGenerator
    {
        public static int id = 1;

        public static void CreateDirectoriesAndFiles(string path)
        {
            try
            {
                CreateRandomFile(path, 3);
                CreateRandomFile(path, 3);
                CreateRandomFile(path, 3);
                CreateRandomFile(path, 3);
                CreateRandomFile(path, 3);
                string path1 = Path.Combine(path, "dir1");
                if (!Directory.Exists(path1))
                {
                    Directory.CreateDirectory(path1);
                }
                CreateRandomFile(path1, 3);
                CreateRandomFile(path1, 3);
                CreateRandomFile(path1, 3);
                CreateRandomFile(path1, 3);
                CreateRandomFile(path1, 3);
                string path2 = Path.Combine(path1, "dir2");
                if (!Directory.Exists(path2))
                {
                    Directory.CreateDirectory(path2);
                }
                CreateRandomFile(path2, 3);
            }
            catch (IOException ex)
            {
                Console.WriteLine("Exception on testing side, ignoring " + ex);
            }
        }

        public static void CreateRandomFile(string path, int maxSizeInKb)
        {
            Random rng = new Random();
            int sizeInKb = 1 + rng.Next(maxSizeInKb);
            CreateFileInFolder(path, sizeInKb);
        }

        /// <summary>
        /// Gives the name of the file that will be created next.
        /// </summary>
        public static string GetNextFileName()
        {
            string filename = "file_" + id.ToString() + ".bin";
            return filename;
        }

        /// <summary>
        /// Create a random binary file in the given folder, with the given size.
        /// </summary>
        public static void CreateFileInFolder(string folderPath, int sizeInKb)
        {
            ++id;
            CreateFile(GetNextFileName(), sizeInKb);
        }

        /// <summary>
        /// Create a random binary file with the given path, with the given size.
        /// </summary>
        public static void CreateFile(string filePath, int sizeInKb)
        {
            Random random = new Random();
            byte[] data = new byte[1024];

            try
            {
                using (FileStream stream = File.OpenWrite(filePath))
                {
                    // Write random data
                    for (int i = 0; i < sizeInKb; i++)
                    {
                        random.NextBytes(data);
                        stream.Write(data, 0, data.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception on testing side, ignoring " + ex);
            }
        }
    }
}
