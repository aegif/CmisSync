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
using DotCMIS.Exceptions;

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

        // Path of the root in the remote repository.
        string remoteFolderPath;

        // State. true if syncing is being performed right now.
        // TODO use is_syncing variable in parent
        bool syncing = false;

        // Parameters for CMIS requests.
        Dictionary<string, string> cmisParameters;

        public SparkleRepo (string path, SparkleConfig config) : base (path, config)
        {
            // Set local root folder.
            localRootFolder = Path.Combine(SparkleFolder.ROOT_FOLDER,
                 config.GetFolderOptionalAttribute(Path.GetFileName(path), "name"));

            // Get path on remote repository.
            remoteFolderPath = config.GetFolderOptionalAttribute(Path.GetFileName(path), "remoteFolder");

            cmisParameters = new Dictionary<string, string>();
            cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
            cmisParameters[SessionParameter.AtomPubUrl] = config.GetUrlForFolder(Path.GetFileName(path));
            cmisParameters[SessionParameter.User] = config.GetFolderOptionalAttribute(Path.GetFileName(path), "user");
            cmisParameters[SessionParameter.Password] = config.GetFolderOptionalAttribute(Path.GetFileName(path), "password");
            cmisParameters[SessionParameter.RepositoryId] = config.GetFolderOptionalAttribute(Path.GetFileName(path), "repository");
        }

        public void Connect()
        {
            // Create session factory.
            SessionFactory factory = SessionFactory.NewInstance();
            try
            {
                // Get the list of repositories. There should be only one, because we specified RepositoryId.
                IList<IRepository> repositories = factory.GetRepositories(cmisParameters);
                if(repositories.Count != 1)
                    SparkleLogger.LogInfo("Sync", "Unexpected number of matching repositories: " + repositories.Count);
                // Get the repository.
                IRepository repository = factory.GetRepositories(cmisParameters)[0];
                // Detect whether the repository has the ChangeLog capability.
                ChangeLogCapability = repository.Capabilities.ChangesCapability == CapabilityChanges.All
                    || repository.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
                session = repository.CreateSession();
                SparkleLogger.LogInfo("Sync", "Created CMIS session: " + session.ToString());
            }
            catch (CmisRuntimeException e)
            {
                SparkleLogger.LogInfo("Sync", "Exception: " + e.Message + ", error content: " + e.ErrorContent);
            }
        }

        private void SyncInBackground()
        {
            if (syncing)
                return;
            syncing = true;

            SparkleLogger.LogInfo("Sync", "Syncing " + RemoteUrl + " " + local_config.GetFolderOptionalAttribute("repository", LocalPath));

            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(
                delegate(Object o, DoWorkEventArgs args)
                {
                    SparkleLogger.LogInfo("Sync", "Launching sync in background, so that the UI stays available.");
                    try
                    {
                        Sync();
                    }
                    catch (CmisBaseException e)
                    {
                        SparkleLogger.LogInfo("Sync", "CMIS exception while syncing:" + e.Message);
                        SparkleLogger.LogInfo("Sync", e.ErrorContent);
                    }
                    catch (Exception e)
                    {
                        SparkleLogger.LogInfo("Sync", "Exception while syncing:" + e.Message);
                    }
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

        private void Sync()
        {
            // If not connected, connect.
            if (session == null)
                Connect();

            // Get the root folder.
            //IFolder remoteRootFolder = session.GetRootFolder();
            IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath);

            if (ChangeLogCapability)
            {
                // Get last change log token from server.
                // TODO
                if (true /* TODO if no locally saved CMIS change log token */)
                {
                    RecursiveFolderCopy(remoteFolder, localRootFolder);
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
                CrawlSync(remoteFolder, localRootFolder);
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

                    string remoteDocumentFileName = remoteDocument.ContentStreamFileName;
                    // If this file does not have a filename, ignore it.
                    // It sometimes happen on IBM P8 CMIS server, not sure why.
                    if (remoteDocumentFileName == null)
                    {
                        SparkleLogger.LogInfo("Sync", "Skipping download of file with null content stream in " + localFolder);
                        continue;
                    }

                    string filePath = localFolder + Path.DirectorySeparatorChar + remoteDocumentFileName;


                    if (File.Exists(filePath))
                    {
                        // Check modification date stored in SQLite and download if remote modification date if different.
                        // TODO
                    }
                    else
                    {
                        SparkleLogger.LogInfo("Sync", "Downloading " + remoteDocumentFileName);
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
                SparkleLogger.LogInfo("Sync", "Skipping download of file with null content stream: " + remoteDocument.ContentStreamFileName);
                return;
            }

            // Download.
            string filePath = localFolder + Path.DirectorySeparatorChar + contentStream.FileName;

            // If there was previously a directory with this name, delete it.
            // TODO warn if local changes inside the folder.
            if (Directory.Exists(filePath))
            {
                Directory.Delete(filePath);
            }

            SparkleLogger.LogInfo("Sync", /*id +*/ "Downloading " + filePath);
            Stream file = File.OpenWrite(filePath);
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                file.Write(buffer, 0, len);
            }
            file.Close();
            contentStream.Stream.Close();
            SparkleLogger.LogInfo("Sync", "Downloaded");
        }


        public override List<string> ExcludePaths {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo ExcludePaths get");
                List<string> rules = new List<string> ();
                rules.Add (".CmisSync"); // Contains the configuration for this checkout.
                return rules;
            }
        }


        public override double Size {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo Size get");
                return 1234567; // TODO
            }
        }


        public override double HistorySize {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HistorySize get");
                return 1234567; // TODO
            }
        }


        private void UpdateSizes ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo UpdateSizes");
			// TODO
        }
        

        public override string [] UnsyncedFilePaths {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo UnsyncedFilePaths get");
                List<string> file_paths = new List<string> ();
                //file_paths.Add (path); TODO
                return file_paths.ToArray ();
            }
        }


        public override string CurrentRevision {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo CurrentRevision get");
                return null; // TODO
            }
        }


        public override bool HasRemoteChanges {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HasRemoteChanges get");
                return false; // TODO
            }
        }


        public override bool SyncUp ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo SyncUp");
			return true; // TODO
        }


        public override bool SyncDown ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo SyncDown");
			return true; // TODO
        }


        public override bool HasLocalChanges {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HasLocalChanges get");
				return false; // TODO
            }
        }


        public override bool HasUnsyncedChanges {
            get {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HasUnsyncedChanges get");
                SyncInBackground();
				return false; // TODO
            }

            set {
                SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo HasUnsyncedChanges set");
				// TODO
            }
        }


        // Stages the made changes
        private void Add ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo Add");
			// TODO
        }


        // Commits the made changes
        private void Commit (string message)
		{
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo Commit");
			// TODO
        }


        // Merges the fetched changes
        private void Rebase ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo Rebase");
			// TODO
        }


        private void ResolveConflict ()
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo ResolveConflict");
			// TODO
        }


        public override void RevertFile (string path, string revision)
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo RevertFile");
			// TODO
        }


        public override List<SparkleChangeSet> GetChangeSets (string path, int count)
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo GetChangeSets");
            return new List <SparkleChangeSet> (); // TODO
        }   


        public override List<SparkleChangeSet> GetChangeSets (int count)
        {
            SparkleLogger.LogInfo("Sync", "Cmis SparkleRepo GetChangeSets");
            return new List <SparkleChangeSet> (); // TODO
        }
    }
}
