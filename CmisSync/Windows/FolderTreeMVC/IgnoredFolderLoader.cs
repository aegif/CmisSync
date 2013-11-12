using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CmisSync.CmisTree
{
    /// <summary>
    /// Creates Nodes from given ignored folder list
    /// </summary>
    public static class IgnoredFolderLoader
    {
        /// <summary>
        /// Creates a Node with its children for a given ignored folder
        /// </summary>
        /// <param name="ignoredPath"></param>
        /// <returns></returns>
        public static Node CreateNodesFromIgnoredFolder(string ignoredPath)
        {
            if (ignoredPath.StartsWith("/"))
                ignoredPath = ignoredPath.Substring(1, ignoredPath.Length - 1);
            string[] parts = ignoredPath.Split('/');
            if (parts.Length == 0)
                throw new ArgumentException("The ignoredPath contains no folder: " + ignoredPath);
            Node[] nodes = new Node[parts.Length];
            for ( int i = 0; i < nodes.Length; i++ )
            {
                Folder f = new Folder()
                {
                    Name = parts[i],
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
                {
                    nodes[i].Selected = false;
                }
            }
            return nodes[0];
        }

        /// <summary>
        /// Takes a list of ignored folders and returns the created Nodes as list
        /// </summary>
        /// <param name="ignoredFolder"></param>
        /// <returns></returns>
        public static List<Node> CreateNodesFormIgnoredFolders(List<string> ignoredFolder)
        {
            List<Node> results = new List<Node>();
            foreach (string ignored in ignoredFolder)
                results.Add(CreateNodesFromIgnoredFolder(ignored));
            return results;
        }

        /// <summary>
        /// Merges the given ignored folders as children to the given root folder node
        /// </summary>
        /// <param name="root"></param>
        /// <param name="ignoredFolder"></param>
        public static void AddIgnoredFolderToRootNode(RootFolder root, List<string> ignoredFolder)
        {
            AsyncNodeLoader.MergeFolderTrees(root, CreateNodesFormIgnoredFolders(ignoredFolder));
        }
    }
}
