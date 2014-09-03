using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using log4net;

namespace CmisSync.Lib.Database
{

    /// <summary>
    /// Interface for Database
    /// </summary>
    public interface IDatabase
    {


        /// <summary>
        /// Begins a Database transaction
        /// </summary>
        DbTransaction BeginTransaction();


        /// <summary>
        /// Add a file to the database.
        /// If checksum is not null, it will be used for the database entry
        /// </summary>
        // void AddFile(string path, string objectId, DateTime? serverSideModificationDate,
        //     Dictionary<string, string[]> metadata, byte[] filehash);
        void AddFile(SyncItem item, string objectId, DateTime? serverSideModificationDate,
            Dictionary<string, string[]> metadata, byte[] filehash);

        /// <summary>
        /// Add a folder to the database.
        /// </summary>
        void AddFolder(SyncItem item, string objectId, DateTime? serverSideModificationDate);


        /// <summary>
        /// Remove a file from the database.
        /// </summary>
        void RemoveFile(string path);


        /// <summary>
        /// move a file from the database.
        /// </summary>
        void MoveFile(string oldPath, string newPath);


        /// <summary>
        /// Remove a folder from the database.
        /// </summary>
        void RemoveFolder(string path);


        /// <summary>
        /// move a folder from the database.
        /// </summary>
        void MoveFolder(string oldPath, string newPath);


        /// <summary>
        /// Get the time at which the file was last modified.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        DateTime? GetServerSideModificationDate(string path);


        /// <summary>
        /// Set the last modification date of a file.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        void SetFileServerSideModificationDate(string path, DateTime? serverSideModificationDate);


        /// <summary>
        /// Get the date at which the file was last download.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        DateTime? GetDownloadServerSideModificationDate(string path);


        /// <summary>
        /// Set the last download date of a file.
        /// This is the time on the CMIS server side, in UTC. Client-side time does not matter.
        /// </summary>
        void SetDownloadServerSideModificationDate(string path, DateTime? serverSideModificationDate);

        /// <summary>
        /// Deletes the upload retry counter.
        /// </summary>
        /// <param name='path'>
        /// Path of the local file.
        /// </param>
        void DeleteAllFailedOperations(string path);

        /// <summary>
        /// Deletes all failed upload counter.
        /// </summary>
        void DeleteAllFailedOperations();

        /// <summary>
        /// Recalculate the checksum of a file and save it to database.
        /// </summary>
        void RecalculateChecksum(string path);

        /// <summary>
        /// <returns>path field in files table for <paramref name="id"/></returns>
        /// </summary>
        string GetFilePath(string id);

        /// <summary>
        /// Checks whether the database contains a given folder.
        /// </summary>
        bool ContainsFolder(string path);

        /// <summary>
        /// <returns>path field in folders table for <paramref name="id"/></returns>
        /// </summary>
        string GetFolderPath(string id);

        /// <summary>
        /// Check whether a file's content has changed locally since it was last synchronized.
        /// This happens when the user edits a file on the local computer.
        /// This method does not communicate with the CMIS server, it just checks whether the checksum has changed.
        /// </summary>
        bool LocalFileHasChanged(string path);

        /// <summary>
        /// Get the ChangeLog token that was stored at the end of the last successful CmisSync synchronization.
        /// </summary>
        string GetChangeLogToken();


        /// <summary>
        /// Set the stored ChangeLog token.
        /// This should be called after each successful CmisSync synchronization.
        /// </summary>
        void SetChangeLogToken(string token);

    }
}
