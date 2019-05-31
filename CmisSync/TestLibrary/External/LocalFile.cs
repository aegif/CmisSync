using DotCMIS.Client;
using DotCMIS.Exceptions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestLibrary.External
{
    class LocalFile
    {
        public string Filename { get; }

        public string FullPath { get; }

        public IDocument document { get; }

        public LocalFile(string url, string user, string password,
            string repositoryId, string remoteFolderPath,
            CmisSyncProcess sync, AbstractExternalTests test)
        {
            // Create random small file.
            int sizeKb = 3;
            Filename = "file.doc";
            FullPath = Path.Combine(sync.Folder(), Filename);
            LocalFilesystemActivityGenerator.CreateFile(FullPath, sizeKb);

            // Wait a few seconds so that sync gets a chance to sync things.
            Thread.Sleep(20 * 1000); // TODO make shorter, but wait then check again if assert fails

            // Check that file is present server-side.
            document = null;
            try
            {
                document = (IDocument)(
                    test.CreateSession(url, user, password, repositoryId)
                        .GetObjectByPath(remoteFolderPath + "/" + Filename, true)
                );
            }
            catch (CmisObjectNotFoundException e)
            {
                Assert.Fail("Document failed to get synced to the server.", e);
            }
            Assert.NotNull(document);
            Assert.AreEqual(Filename, document.ContentStreamFileName);
            Assert.AreEqual(Filename, document.Name);
            Assert.AreEqual(sizeKb * 1024, document.ContentStreamLength);
            Assert.AreEqual("application/msword", document.ContentStreamMimeType);
            // TODO compare date Assert.AreEqual(, doc.);
            string docId = document.Id;
        }
    }
}
