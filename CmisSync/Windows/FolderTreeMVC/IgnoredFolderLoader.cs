using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CmisSync.CmisTree
{
    public static class IgnoredFolderLoader
    {
        public static Node CreateNodesFromIgnoredFolder(string ignoredPath)
        {
            if (ignoredPath.StartsWith("/"))
                ignoredPath = ignoredPath.Substring(1, ignoredPath.Length - 1);
            string[] parts = ignoredPath.Split('/');
            if (parts.Length == 0)
                throw new ArgumentException("The ignoredPath contains no folder: " + ignoredPath);
            Node[] nodes = new Node[parts.Length];
            string path = "";
            for ( int i = 0; i < nodes.Length; i++ )
            {
                path += parts[i] + Path.DirectorySeparatorChar;
                Folder f = new Folder()
                {
                    Name = parts[i],
                    Path = path,
                    LocationType = Node.NodeLocationType.NONE,
                    Status = LoadingStatus.DONE
                };
                nodes[i] = f;
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                if (i > 0)
                    nodes[i].Parent = nodes[i - 1];
                if (i < nodes.Length - 1)
                    nodes[i].Children.Add(nodes[i + 1]);
                if (i == nodes.Length - 1)
                    nodes[i].IsIgnored = true;
            }
            return nodes[0];
        }

        public static List<Node> CreateNodesFormIgnoredFolders(List<string> ignoredFolder)
        {
            List<Node> results = new List<Node>();
            foreach (string ignored in ignoredFolder)
                results.Add(CreateNodesFromIgnoredFolder(ignored));
            return results;
        }

        public static void AddIgnoredFolderToRootNode(RootFolder root, List<string> ignoredFolder)
        {
            AsyncNodeLoader.MergeFolderTrees(root, CreateNodesFormIgnoredFolders(ignoredFolder));
        }
    }
}
