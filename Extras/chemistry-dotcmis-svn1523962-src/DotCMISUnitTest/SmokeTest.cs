/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
using System.Collections.Generic;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Enums;
using NUnit.Framework;
using System;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using System.Text;
using System.IO;
using DotCMIS.Exceptions;

namespace DotCMISUnitTest
{
    [TestFixture]
    class SmokeTest : TestFramework
    {
        [Test]
        public void SmokeTestSession()
        {
            Assert.NotNull(Session);
            Assert.NotNull(Session.Binding);
            Assert.NotNull(Session.RepositoryInfo);
            Assert.NotNull(Session.RepositoryInfo.Id);
            Assert.NotNull(Session.RepositoryInfo.RootFolderId);
            Assert.NotNull(Session.DefaultContext);
            Assert.NotNull(Session.ObjectFactory);

            Assert.AreEqual("test", Session.CreateObjectId("test").Id);
        }

        [Test]
        public void SmokeTestTypes()
        {
            // getTypeDefinition
            IObjectType documentType = Session.GetTypeDefinition("cmis:document");
            Assert.NotNull(documentType);
            Assert.True(documentType is DocumentType);
            Assert.AreEqual("cmis:document", documentType.Id);
            Assert.AreEqual(BaseTypeId.CmisDocument, documentType.BaseTypeId);
            Assert.True(documentType.IsBaseType);
            Assert.IsNullOrEmpty(documentType.ParentTypeId);
            Assert.NotNull(documentType.PropertyDefinitions);
            Assert.True(documentType.PropertyDefinitions.Count >= 9);

            IObjectType folderType = Session.GetTypeDefinition("cmis:folder");
            Assert.NotNull(folderType);
            Assert.True(folderType is FolderType);
            Assert.AreEqual("cmis:folder", folderType.Id);
            Assert.AreEqual(BaseTypeId.CmisFolder, folderType.BaseTypeId);
            Assert.True(folderType.IsBaseType);
            Assert.IsNullOrEmpty(folderType.ParentTypeId);
            Assert.NotNull(folderType.PropertyDefinitions);
            Assert.True(folderType.PropertyDefinitions.Count >= 9);

            // getTypeChildren
            Session.Clear();

            IItemEnumerable<IObjectType> children = Session.GetTypeChildren(null, true);
            Assert.NotNull(children);

            int count;
            count = 0;
            foreach (IObjectType type in children)
            {
                Assert.NotNull(type);
                Assert.NotNull(type.Id);
                Assert.True(type.IsBaseType);
                Assert.IsNullOrEmpty(type.ParentTypeId);
                Assert.NotNull(type.PropertyDefinitions);

                Session.Clear();
                IObjectType type2 = Session.GetTypeDefinition(type.Id);
                AssertAreEqual(type, type2);

                Session.GetTypeChildren(type.Id, true);

                count++;
            }

            Assert.True(count >= 2);
            Assert.True(count <= 4);

            // getTypeDescendants
            Session.Clear();

            IList<ITree<IObjectType>> descendants = Session.GetTypeDescendants(null, -1, true);

            count = 0;
            foreach (ITree<IObjectType> tree in descendants)
            {
                Assert.NotNull(tree);
                Assert.NotNull(tree.Item);

                IObjectType type = tree.Item;
                Assert.NotNull(type);
                Assert.NotNull(type.Id);
                Assert.True(type.IsBaseType);
                Assert.IsNullOrEmpty(type.ParentTypeId);
                Assert.NotNull(type.PropertyDefinitions);

                Session.Clear();
                IObjectType type2 = Session.GetTypeDefinition(type.Id);
                AssertAreEqual(type, type2);

                Session.GetTypeDescendants(type.Id, 2, true);

                count++;
            }

            Assert.True(count >= 2);
            Assert.True(count <= 4);
        }

        [Test]
        public void SmokeTestRootFolder()
        {
            ICmisObject rootFolderObject = Session.GetRootFolder();

            Assert.NotNull(rootFolderObject);
            Assert.NotNull(rootFolderObject.Id);
            Assert.True(rootFolderObject is IFolder);

            IFolder rootFolder = (IFolder)rootFolderObject;

            Assert.AreEqual("/", rootFolder.Path);
            Assert.AreEqual(1, rootFolder.Paths.Count);

            Assert.NotNull(rootFolder.AllowableActions);
            Assert.True(rootFolder.AllowableActions.Actions.Contains(Actions.CanGetProperties));
            Assert.False(rootFolder.AllowableActions.Actions.Contains(Actions.CanGetFolderParent));

            IItemEnumerable<ICmisObject> children = rootFolder.GetChildren();
            Assert.NotNull(children);
            foreach (ICmisObject child in children)
            {
                Assert.NotNull(child);
                Assert.NotNull(child.Id);
                Assert.NotNull(child.Name);
                Console.WriteLine(child.Name + " (" + child.Id + ")");
            }
        }

        [Test]
        public void SmokeTestQuery()
        {
            IItemEnumerable<IQueryResult> qr = Session.Query("SELECT * FROM cmis:document", false);
            Assert.NotNull(qr);

            foreach (IQueryResult hit in qr)
            {
                Assert.NotNull(hit);
                Assert.NotNull(hit["cmis:objectId"]);
                Console.WriteLine(hit.GetPropertyValueById(PropertyIds.Name) + " (" + hit.GetPropertyValueById(PropertyIds.ObjectId) + ")");

                foreach (IPropertyData prop in hit.Properties)
                {
                    string name = prop.QueryName;
                    object value = prop.FirstValue;
                }
            }
        }

        [Test]
        public void SmokeTestCreateDocument()
        {
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = "test-smoke.txt";
            properties[PropertyIds.ObjectTypeId] = DefaultDocumentType;

            byte[] content = UTF8Encoding.UTF8.GetBytes("Hello World!");

            ContentStream contentStream = new ContentStream();
            contentStream.FileName = properties[PropertyIds.Name] as string;
            contentStream.MimeType = "text/plain";
            contentStream.Length = content.Length;
            contentStream.Stream = new MemoryStream(content);

            IDocument doc = TestFolder.CreateDocument(properties, contentStream, null);

            // check doc
            Assert.NotNull(doc);
            Assert.NotNull(doc.Id);
            Assert.AreEqual(properties[PropertyIds.Name], doc.Name);
            Assert.AreEqual(BaseTypeId.CmisDocument, doc.BaseTypeId);
            Assert.True(doc.AllowableActions.Actions.Contains(Actions.CanGetProperties));
            Assert.False(doc.AllowableActions.Actions.Contains(Actions.CanGetChildren));

            // check type
            IObjectType type = doc.ObjectType;
            Assert.NotNull(type);
            Assert.NotNull(type.Id);
            Assert.AreEqual(properties[PropertyIds.ObjectTypeId], type.Id);

            // check versions
            IList<IDocument> versions = doc.GetAllVersions();
            Assert.NotNull(versions);
            Assert.AreEqual(1, versions.Count);
            //Assert.AreEqual(doc.Id, versions[0].Id);

            // check content
            IContentStream retrievedContentStream = doc.GetContentStream();
            Assert.NotNull(retrievedContentStream);
            Assert.NotNull(retrievedContentStream.Stream);

            MemoryStream byteStream = new MemoryStream();
            byte[] buffer = new byte[4096];
            int b = 1;
            while (b > 0)
            {
                b = retrievedContentStream.Stream.Read(buffer, 0, buffer.Length);
                byteStream.Write(buffer, 0, b);
            }

            byte[] retrievedContent = byteStream.ToArray();
            Assert.NotNull(retrievedContent);
            Assert.AreEqual(content.Length, retrievedContent.Length);
            for (int i = 0; i < content.Length; i++)
            {
                Assert.AreEqual(content[i], retrievedContent[i]);
            }

            // update name
            properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = "test2-smoke.txt";

            IObjectId newId = doc.UpdateProperties(properties);
            IDocument doc2 = Session.GetObject(newId) as IDocument;

            Assert.NotNull(doc2);

            doc2.Refresh();
            Assert.AreEqual(properties[PropertyIds.Name], doc2.Name);
            Assert.AreEqual(properties[PropertyIds.Name], doc2.GetPropertyValue(PropertyIds.Name));

            IProperty nameProperty = doc2[PropertyIds.Name];
            Assert.NotNull(nameProperty.PropertyType);
            Assert.AreEqual(properties[PropertyIds.Name], nameProperty.Value);
            Assert.AreEqual(properties[PropertyIds.Name], nameProperty.FirstValue);


            byte[] content2 = UTF8Encoding.UTF8.GetBytes("Hello Universe!");

            ContentStream contentStream2 = new ContentStream();
            contentStream2.FileName = properties[PropertyIds.Name] as string;
            contentStream2.MimeType = "text/plain";
            contentStream2.Length = content2.Length;
            contentStream2.Stream = new MemoryStream(content2);

            // doc.SetContentStream(contentStream2, true);

            doc2.Delete(true);

            try
            {
                doc.Refresh();
                Assert.Fail("Document shouldn't exist anymore!");
            }
            catch (CmisObjectNotFoundException) { }
        }

        [Test]
        public void SmokeTestVersioning()
        {
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = "test-version-smoke.txt";
            properties[PropertyIds.ObjectTypeId] = DefaultDocumentType;

            IDocument doc = TestFolder.CreateDocument(properties, null, null);
            Assert.NotNull(doc);
            Assert.NotNull(doc.Id);
            Assert.AreEqual(properties[PropertyIds.Name], doc.Name);

            IList<IDocument> versions = doc.GetAllVersions();
            Assert.NotNull(versions);
            Assert.AreEqual(1, versions.Count);

            IObjectId pwcId = doc.CheckOut();
            Assert.NotNull(pwcId);

            IDocument pwc = Session.GetObject(pwcId) as IDocument;

            // check PWC
            Assert.NotNull(pwc);
            Assert.NotNull(pwc.Id);
            Assert.AreEqual(BaseTypeId.CmisDocument, doc.BaseTypeId);

            IDictionary<string, object> newProperties = new Dictionary<string, object>();
            newProperties[PropertyIds.Name] = "test-version2-smoke.txt";

            IObjectId doc2Id = pwc.CheckIn(true, newProperties, null, "new DotCMIS version");
            Assert.NotNull(doc2Id);

            IDocument doc2 = Session.GetObject(doc2Id) as IDocument;
            doc2.Refresh();

            // check new version
            Assert.NotNull(doc2);
            Assert.NotNull(doc2.Id);
            Assert.AreEqual(newProperties[PropertyIds.Name], doc2.Name);
            Assert.AreEqual(BaseTypeId.CmisDocument, doc2.BaseTypeId);

            versions = doc2.GetAllVersions();
            Assert.NotNull(versions);
            Assert.AreEqual(2, versions.Count);

            doc2.DeleteAllVersions();

            try
            {
                doc2.Refresh();
                Assert.Fail("Document shouldn't exist anymore!");
            }
            catch (CmisObjectNotFoundException) { }
        }

        [Test]
        public void SmokeTestCreateFolder()
        {
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties[PropertyIds.Name] = "test-smoke";
            properties[PropertyIds.ObjectTypeId] = DefaultFolderType;

            IFolder folder = TestFolder.CreateFolder(properties);

            // check folder
            Assert.NotNull(folder);
            Assert.NotNull(folder.Id);
            Assert.AreEqual(properties[PropertyIds.Name], folder.Name);
            Assert.AreEqual(BaseTypeId.CmisFolder, folder.BaseTypeId);
            Assert.AreEqual(TestFolder.Id, folder.FolderParent.Id);
            Assert.False(folder.IsRootFolder);
            Assert.True(folder.Path.StartsWith("/"));
            Assert.True(folder.AllowableActions.Actions.Contains(Actions.CanGetProperties));
            Assert.True(folder.AllowableActions.Actions.Contains(Actions.CanGetChildren));
            Assert.False(folder.AllowableActions.Actions.Contains(Actions.CanGetContentStream));

            // check children
            foreach (ICmisObject cmisObject in folder.GetChildren())
            {
                Assert.Fail("Folder shouldn't have children!");
            }

            // check descendants
            bool? descSupport = Session.RepositoryInfo.Capabilities.IsGetDescendantsSupported;
            if (descSupport.HasValue && descSupport.Value)
            {
                IList<ITree<IFileableCmisObject>> list = folder.GetDescendants(-1);

                if (list != null)
                {
                    foreach (ITree<IFileableCmisObject> desc in list)
                    {
                        Assert.Fail("Folder shouldn't have children!");
                    }
                }
            }
            else
            {
                Console.WriteLine("GetDescendants not supported!");
            }

            // check folder tree
            bool? folderTreeSupport = Session.RepositoryInfo.Capabilities.IsGetFolderTreeSupported;
            if (folderTreeSupport.HasValue && folderTreeSupport.Value)
            {
                IList<ITree<IFileableCmisObject>> list = folder.GetFolderTree(-1);

                if (list != null)
                {
                    foreach (ITree<IFileableCmisObject> desc in list)
                    {
                        Assert.Fail("Folder shouldn't have children!");
                    }
                }
            }
            else
            {
                Console.WriteLine("GetFolderTree not supported!");
            }

            // check parents
            IFolder parent = folder.FolderParent;
            Assert.NotNull(parent);
            Assert.AreEqual(TestFolder.Id, parent.Id);

            IList<IFolder> parents = folder.Parents;
            Assert.NotNull(parents);
            Assert.True(parents.Count > 0);

            bool found = false;
            foreach (IFolder p in parents)
            {
                if (TestFolder.Id == p.Id)
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found);

            folder.Delete(true);

            try
            {
                folder.Refresh();
                Assert.Fail("Folder shouldn't exist anymore!");
            }
            catch (CmisObjectNotFoundException) { }
        }

        [Test]
        public void SmokeTestContentChanges()
        {
            if (Session.RepositoryInfo.Capabilities.ChangesCapability != null)
            {
                if (Session.RepositoryInfo.Capabilities.ChangesCapability != CapabilityChanges.None)
                {
                    IChangeEvents changeEvents = Session.GetContentChanges(null, true, 1000);
                    Assert.NotNull(changeEvents);
                }
                else
                {
                    Console.WriteLine("Content changes not supported!");
                }
            }
            else
            {
                Console.WriteLine("ChangesCapability not set!");
            }
        }
    }
}
