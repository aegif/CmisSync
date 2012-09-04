//   CmisSync, a CMIS synchronization tool.
//   Copyright (C) 2012  Nicolas Raoul <nicolas.raoul@aegif.jp>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;
using DotCMIS.Data.Impl;
using DotCMIS.Data.Extensions;

using SparkleLib;
using System.ComponentModel;
using DotCMIS.Enums;

namespace SparkleLib.Cmis {

    public class SparkleRepo : SparkleRepoBase {

        // At which degree the repository supports Change Logs.
        // See http://docs.oasis-open.org/cmis/CMIS/v1.0/os/cmis-spec-v1.0.html#_Toc243905424
        // Possible values: none, objectidsonly, properties, all
        bool ChangeLogCapability;

        // Session to the CMIS repository.
        ISession session;

        // Local folder where the changes are synchronized to.
        string localRootFolder;

        bool syncing = true; // State. true if syncing is being performed right now. // TODO use is_syncing variable in parent

        public SparkleRepo (string path, SparkleConfig config) : base (path, config)
        {
            // Set local root folder.
            localRootFolder = @"C:\localRoot" // TODO make this configurable, or create in user home.
                 + Path.DirectorySeparatorChar
                 + config.GetFolderOptionalAttribute(Path.GetFileName(path), "name");

            // Connect to repository.
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = config.GetUrlForFolder(Path.GetFileName(path));
            parameters[SessionParameter.User] = config.GetFolderOptionalAttribute(Path.GetFileName(path), "user");
            parameters[SessionParameter.Password] = config.GetFolderOptionalAttribute(Path.GetFileName(path), "password");
            parameters[SessionParameter.RepositoryId] = config.GetFolderOptionalAttribute(Path.GetFileName(path), "repository");
            SessionFactory factory = SessionFactory.NewInstance();
            IList<IRepository> repositories = factory.GetRepositories(parameters);
            Console.WriteLine("Matching repositories: " + repositories.Count);
            IRepository repository = factory.GetRepositories(parameters)[0];
            ChangeLogCapability = repository.Capabilities.ChangesCapability.GetCmisValue().Equals("all")
                || repository.Capabilities.ChangesCapability.GetCmisValue().Equals("objectidsonly");
            session = repository.CreateSession();
            Console.WriteLine("Created CMIS session: " + session.ToString());
            syncing = false;
        }

        private void Sync()
        {
            if (syncing)
                return;
            syncing = true;
            // TODO this.watcher.Disable ();

            Console.WriteLine("Syncing " + RemoteUrl + " " + local_config.GetFolderOptionalAttribute("repository", LocalPath));

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(
                delegate(Object o, DoWorkEventArgs args)
                {
                    Console.WriteLine("Launching sync in background, so that the UI stays available.");
                    SyncInBackground();
                }
            );
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                delegate(object o, RunWorkerCompletedEventArgs args)
                {
                    syncing = false;
                }
            );
            bw.RunWorkerAsync();
        }

        private void SyncInBackground()
        {
            // Get the root folder.
            IFolder remoteRootFolder = session.GetRootFolder();

            if (ChangeLogCapability)
            {
                // Get last change log token from server.
                // TODO
                if (true /* TODO if no locally saved CMIS change log token */)
                {
                    RecursiveFolderCopy(remoteRootFolder, localRootFolder);
                }
                else
                {
                    // Check which files/folders have changed.
                    // TODO session.GetContentChanges(changeLogToken, includeProperties, maxNumItems);

                    // Download/delete files/folders accordingly.
                    // TODO
                }
                // Save change log token locally.
                // TODO
            }
            else
            {
                // No ChangeLog capability, so we have to crawl remote and local folders.
                CrawlSync(remoteRootFolder, localRootFolder);
            }
        }

        private void RecursiveFolderCopy(IFolder remoteFolder, string localFolder)
		{
            String id = new Random().Next(0, 1000000).ToString();
            // List all children.
			foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
			{
                if (cmisObject is DotCMIS.Client.Impl.Folder)
                {
                    IFolder remoteSubFolder = (IFolder)cmisObject;
                    string localSubFolder = localFolder + Path.DirectorySeparatorChar + cmisObject.Name;
                    
                    // Create local folder.
                    Directory.CreateDirectory(localSubFolder);

                    // Recurse into folder.
                    RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                }
                else
                {
                    // It is a file, just download it.
                    DownloadFile((IDocument)cmisObject, localFolder);
                }
            }
		}


        private void CrawlSync(IFolder remoteFolder, string localFolder)
        {
            //String id = new Random().Next(0, 1000000).ToString();

            // Sync down.

            // List all children.
            foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
            {
                if (cmisObject is DotCMIS.Client.Impl.Folder)
                {
                    IFolder remoteSubFolder = (IFolder)cmisObject;
                    string localSubFolder = localFolder + Path.DirectorySeparatorChar + cmisObject.Name;

                    // Check whether local folder
                    if (Directory.Exists(localSubFolder))
                    {
                        // Recurse into folder.
                        CrawlSync(remoteSubFolder, localSubFolder);
                    }
                    else
                    {
                        // Create local folder.
                        Directory.CreateDirectory(localSubFolder);

                        // Recursive copy of the whole folder.
                        RecursiveFolderCopy(remoteSubFolder, localSubFolder);
                    }
                }
                else
                {
                    // It is a file, check whether it exists and has the same modifica download it.
                    IDocument remoteDocument = (IDocument)cmisObject;

                    string filePath = localFolder + Path.DirectorySeparatorChar + remoteDocument.ContentStreamFileName;
                    if (File.Exists(filePath))
                    {
                        // Check modification date stored in SQLite and download if remote modification date if different.
                        // TODO
                    }
                    else
                    {
                        DownloadFile(remoteDocument, localFolder);
                    }
                }
            }

            // Sync up.
            // TODO
            // for all local folders/files check SQLite
        }


        private void DownloadFile(IDocument remoteDocument, string localFolder)
        {
            DotCMIS.Data.IContentStream contentStream = remoteDocument.GetContentStream();

            // If this file does not have a content stream, ignore it.
            // Even 0 bytes files have a contentStream.
            // null contentStream sometimes happen on IBM P8 CMIS server, not sure why.
            if (contentStream == null)
            {
                return;
            }

            // Download.
            string filePath = localFolder + Path.DirectorySeparatorChar + contentStream.FileName;
            Console.Write(/*id +*/ "Downloading " + filePath);
            Stream file = File.OpenWrite(filePath);
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                file.Write(buffer, 0, len);
            }
            file.Close();
            contentStream.Stream.Close();
            Console.WriteLine(" OK");
        }


        public override List<string> ExcludePaths {
            get {
				Console.WriteLine("Cmis SparkleRepo ExcludePaths get");
                List<string> rules = new List<string> ();
                rules.Add (".CmisSync"); // Contains the configuration for this checkout.
                return rules;
            }
        }


        public override double Size {
            get {
				Console.WriteLine("Cmis SparkleRepo Size get");
                return 1234567; // TODO
            }
        }


        public override double HistorySize {
            get {
				Console.WriteLine("Cmis SparkleRepo HistorySize get");
                return 1234567; // TODO
            }
        }


        private void UpdateSizes ()
        {
			Console.WriteLine("Cmis SparkleRepo UpdateSizes");
			// TODO
        }
        

        public override string [] UnsyncedFilePaths {
            get {
				Console.WriteLine("Cmis SparkleRepo UnsyncedFilePaths get");
                List<string> file_paths = new List<string> ();
                //file_paths.Add (path); TODO
                return file_paths.ToArray ();
            }
        }


        public override string CurrentRevision {
            get {
				Console.WriteLine("Cmis SparkleRepo CurrentRevision get");
                return null; // TODO
            }
        }


        public override bool HasRemoteChanges {
            get {
				Console.WriteLine("Cmis SparkleRepo HasRemoteChanges get");
                return false; // TODO
            }
        }


        public override bool SyncUp ()
        {
			Console.WriteLine("Cmis SparkleRepo SyncUp");
			return true; // TODO
        }


        public override bool SyncDown ()
        {
			Console.WriteLine("Cmis SparkleRepo SyncDown");
			return true; // TODO
        }


        public override bool HasLocalChanges {
            get {
				Console.WriteLine("Cmis SparkleRepo HasLocalChanges get");
				return false; // TODO
            }
        }


        public override bool HasUnsyncedChanges {
            get {
				Console.WriteLine("Cmis SparkleRepo HasUnsyncedChanges get");
                Sync();
				return false; // TODO
            }

            set {
				Console.WriteLine("Cmis SparkleRepo HasUnsyncedChanges set");
				// TODO
            }
        }


        // Stages the made changes
        private void Add ()
        {
			Console.WriteLine("Cmis SparkleRepo Add");
			// TODO
        }


        // Commits the made changes
        private void Commit (string message)
		{
			Console.WriteLine("Cmis SparkleRepo Commit");
			// TODO
        }


        // Merges the fetched changes
        private void Rebase ()
        {
			Console.WriteLine("Cmis SparkleRepo Rebase");
			// TODO
        }


        private void ResolveConflict ()
        {
			Console.WriteLine("Cmis SparkleRepo ResolveConflict");
			// TODO
        }


        public override void RevertFile (string path, string revision)
        {
			Console.WriteLine("Cmis SparkleRepo RevertFile");
			// TODO
        }


        public override List<SparkleChangeSet> GetChangeSets (string path, int count)
        {
			Console.WriteLine("Cmis SparkleRepo GetChangeSets");
            return new List <SparkleChangeSet> (); // TODO
        }   


        public override List<SparkleChangeSet> GetChangeSets (int count)
        {
			Console.WriteLine("Cmis SparkleRepo GetChangeSets");
            return new List <SparkleChangeSet> (); // TODO
        }
    }
}
