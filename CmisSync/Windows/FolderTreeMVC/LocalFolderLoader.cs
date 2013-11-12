using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CmisSync.CmisTree
{
    /// <summary>
    /// Loads a local folder hierarchie as Nodes
    /// </summary>
    public static class LocalFolderLoader
    {
        /// <summary>
        /// Loads all sub folder from the given path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="parent">Parent Node for the new list of Nodes</param>
        /// <returns></returns>
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

        /// <summary>
        /// Merges the sub folder of the given path to the given Repo Node
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="localPath"></param>
        public static void AddLocalFolderToRootNode(RootFolder repo, string localPath)
        {
            List<Node> children = CreateNodesFromLocalFolder(localPath, null);
            AsyncNodeLoader.MergeFolderTrees(repo, children);
        }
    }
}
