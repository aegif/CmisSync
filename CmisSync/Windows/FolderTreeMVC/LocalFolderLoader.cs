using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CmisSync.CmisTree
{
    public static class LocalFolderLoader
    {
        public static List<Node> CreateNodesFromLocalFolder(string path, Node parent)
        {
            string[] subdirs = Directory.GetDirectories(path);
            List<Node> results = new List<Node>();
            foreach (string subdir in subdirs)
            {
                Folder f = new Folder()
                {
                    Name = new DirectoryInfo(subdir).Name,
                    Parent = parent,
                    LocationType = Node.NodeLocationType.LOCAL
                };
                f.IsIllegalFileNameInPath = CmisSync.Lib.Utils.IsInvalidFolderName(f.Name);
                List<Node> children = CreateNodesFromLocalFolder(subdir, f);
                foreach (Node child in children)
                    f.Children.Add(child);
                results.Add(f);
            }
            return results;
        }

        public static void AddLocalFolderToRootNode(RootFolder repo, string localPath)
        {
            List<Node> children = CreateNodesFromLocalFolder(localPath, null);
            AsyncNodeLoader.MergeFolderTrees(repo, children);
        }
    }
}
