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
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using DotCMIS;
using DotCMIS.Binding;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;
using NUnit.Framework;

namespace DotCMISUnitTest
{
    public class TestFramework
    {
        private IRepositoryInfo repositoryInfo;

        public ISession Session { get; set; }
        public ICmisBinding Binding { get { return Session.Binding; } }
        public IRepositoryInfo RepositoryInfo
        {
            get
            {
                if (repositoryInfo == null)
                {
                    repositoryInfo = Binding.GetRepositoryService().GetRepositoryInfos(null)[0];
                    Assert.NotNull(repositoryInfo);
                }

                return repositoryInfo;
            }
        }

        public string DefaultDocumentType { get; set; }
        public string DefaultFolderType { get; set; }
        public IFolder TestFolder { get; set; }

        [SetUp]
        public void Init()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            DefaultDocumentType = "cmis:document";
            DefaultFolderType = "cmis:folder";

            Session = ConnectFromConfig();
        }

        public ISession ConnectFromConfig()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            foreach (string key in ConfigurationManager.AppSettings.AllKeys)
            {
                parameters[key] = ConfigurationManager.AppSettings.Get(key);
            }

            string documentType = ConfigurationManager.AppSettings.Get("test.documenttype");
            if (documentType != null)
            {
                DefaultDocumentType = documentType;
            }

            string folderType = ConfigurationManager.AppSettings.Get("test.foldertype");
            if (folderType != null)
            {
                DefaultFolderType = folderType;
            }

            SessionFactory factory = SessionFactory.NewInstance();

            ISession session = null;
            if (parameters.ContainsKey(SessionParameter.RepositoryId))
            {
                session = factory.CreateSession(parameters);
            }
            else
            {
                session = factory.GetRepositories(parameters)[0].CreateSession();
            }

            Assert.NotNull(session);
            Assert.NotNull(session.Binding);
            Assert.NotNull(session.RepositoryInfo);
            Assert.NotNull(session.RepositoryInfo.Id);

            string testRootFolderPath = ConfigurationManager.AppSettings.Get("test.rootfolder");
            if (testRootFolderPath == null)
            {
                TestFolder = session.GetRootFolder();
            }
            else
            {
                TestFolder = session.GetObjectByPath(testRootFolderPath) as IFolder;
            }

            Assert.NotNull(TestFolder);
            Assert.NotNull(TestFolder.Id);

            return session;
        }

        public ISession ConnectAtomPub()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            string baseUrlAtom = "http://localhost:8080/alfresco/cmisatom";

            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = baseUrlAtom;
            parameters[SessionParameter.User] = "admin";
            parameters[SessionParameter.Password] = "admin";

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.GetRepositories(parameters)[0].CreateSession();

            Assert.NotNull(session);
            Assert.NotNull(session.Binding);
            Assert.NotNull(session.RepositoryInfo);
            Assert.NotNull(session.RepositoryInfo.Id);

            return session;
        }

        public ISession ConnectWebServices()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            string baseUrlWS = "https://localhost:8443/alfresco/cmisws";

            parameters[SessionParameter.BindingType] = BindingType.WebServices;
            parameters[SessionParameter.WebServicesRepositoryService] = baseUrlWS + "/RepositoryService?wsdl";
            parameters[SessionParameter.WebServicesAclService] = baseUrlWS + "/AclService?wsdl";
            parameters[SessionParameter.WebServicesDiscoveryService] = baseUrlWS + "/DiscoveryService?wsdl";
            parameters[SessionParameter.WebServicesMultifilingService] = baseUrlWS + "/MultifilingService?wsdl";
            parameters[SessionParameter.WebServicesNavigationService] = baseUrlWS + "/NavigationService?wsdl";
            parameters[SessionParameter.WebServicesObjectService] = baseUrlWS + "/ObjectService?wsdl";
            parameters[SessionParameter.WebServicesPolicyService] = baseUrlWS + "/PolicyService?wsdl";
            parameters[SessionParameter.WebServicesRelationshipService] = baseUrlWS + "/RelationshipService?wsdl";
            parameters[SessionParameter.WebServicesVersioningService] = baseUrlWS + "/VersioningService?wsdl";
            parameters[SessionParameter.User] = "admin";
            parameters[SessionParameter.Password] = "admin";

            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.GetRepositories(parameters)[0].CreateSession();

            Assert.NotNull(session);
            Assert.NotNull(session.Binding);
            Assert.NotNull(session.RepositoryInfo);
            Assert.NotNull(session.RepositoryInfo.Id);

            return session;
        }

        public IObjectData GetFullObject(string objectId)
        {
            IObjectData result = Binding.GetObjectService().GetObject(RepositoryInfo.Id, objectId, "*", true, IncludeRelationshipsFlag.Both, "*", true, true, null);

            Assert.NotNull(result);
            Assert.NotNull(result.Id);
            Assert.NotNull(result.BaseTypeId);
            Assert.NotNull(result.Properties);

            return result;
        }

        public IObjectData CreateDocument(string folderId, string name, string content)
        {
            DotCMIS.Data.Impl.Properties properties = new DotCMIS.Data.Impl.Properties();

            PropertyData objectTypeIdProperty = new PropertyData(PropertyType.Id);
            objectTypeIdProperty.Id = PropertyIds.ObjectTypeId;
            objectTypeIdProperty.Values = new List<object>();
            objectTypeIdProperty.Values.Add(DefaultDocumentType);
            properties.AddProperty(objectTypeIdProperty);

            PropertyData nameProperty = new PropertyData(PropertyType.String);
            nameProperty.Id = PropertyIds.Name;
            nameProperty.Values = new List<object>();
            nameProperty.Values.Add(name);
            properties.AddProperty(nameProperty);

            ContentStream contentStream = null;
            if (content != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);

                contentStream = new ContentStream();
                contentStream.FileName = name;
                contentStream.MimeType = "text/plain";
                contentStream.Stream = new MemoryStream(bytes);
                contentStream.Length = bytes.Length;
            }

            string newDocId = Binding.GetObjectService().CreateDocument(RepositoryInfo.Id, properties, folderId, contentStream, null, null, null, null, null);

            Assert.NotNull(newDocId);

            IObjectData doc = GetFullObject(newDocId);

            Assert.NotNull(doc);
            Assert.NotNull(doc.Id);
            Assert.AreEqual(BaseTypeId.CmisDocument, doc.BaseTypeId);
            Assert.NotNull(doc.Properties);
            Assert.NotNull(doc.Properties[PropertyIds.ObjectTypeId]);
            Assert.AreEqual(PropertyType.Id, doc.Properties[PropertyIds.ObjectTypeId].PropertyType);
            Assert.AreEqual(DefaultDocumentType, doc.Properties[PropertyIds.ObjectTypeId].FirstValue as string);
            Assert.NotNull(doc.Properties[PropertyIds.Name]);
            Assert.AreEqual(PropertyType.String, doc.Properties[PropertyIds.Name].PropertyType);
            Assert.AreEqual(name, doc.Properties[PropertyIds.Name].FirstValue as string);

            if (folderId != null)
            {
                CheckObjectInFolder(newDocId, folderId);
            }

            return doc;
        }

        public IObjectData CreateFolder(string folderId, string name)
        {
            DotCMIS.Data.Impl.Properties properties = new DotCMIS.Data.Impl.Properties();

            PropertyData objectTypeIdProperty = new PropertyData(PropertyType.Id);
            objectTypeIdProperty.Id = PropertyIds.ObjectTypeId;
            objectTypeIdProperty.Values = new List<object>();
            objectTypeIdProperty.Values.Add(DefaultFolderType);
            properties.AddProperty(objectTypeIdProperty);

            PropertyData nameProperty = new PropertyData(PropertyType.String);
            nameProperty.Id = PropertyIds.Name;
            nameProperty.Values = new List<object>();
            nameProperty.Values.Add(name);
            properties.AddProperty(nameProperty);

            string newFolderId = Binding.GetObjectService().CreateFolder(RepositoryInfo.Id, properties, folderId, null, null, null, null);

            Assert.NotNull(newFolderId);

            IObjectData folder = GetFullObject(newFolderId);

            Assert.NotNull(folder);
            Assert.NotNull(folder.Id);
            Assert.AreEqual(BaseTypeId.CmisFolder, folder.BaseTypeId);
            Assert.NotNull(folder.Properties);
            Assert.NotNull(folder.Properties[PropertyIds.ObjectTypeId]);
            Assert.AreEqual(PropertyType.Id, folder.Properties[PropertyIds.ObjectTypeId].PropertyType);
            Assert.AreEqual(DefaultFolderType, folder.Properties[PropertyIds.ObjectTypeId].FirstValue as string);
            Assert.NotNull(folder.Properties[PropertyIds.Name]);
            Assert.AreEqual(PropertyType.String, folder.Properties[PropertyIds.Name].PropertyType);
            Assert.AreEqual(name, folder.Properties[PropertyIds.Name].FirstValue as string);

            if (folderId != null)
            {
                CheckObjectInFolder(newFolderId, folderId);
            }

            return folder;
        }

        public void CheckObjectInFolder(string objectId, string folderId)
        {
            // check parents
            IList<IObjectParentData> parents = Binding.GetNavigationService().GetObjectParents(RepositoryInfo.Id, objectId, null, null, null, null, null, null);

            Assert.NotNull(parents);
            Assert.True(parents.Count > 0);

            bool found = false;
            foreach (IObjectParentData parent in parents)
            {
                Assert.NotNull(parent);
                Assert.NotNull(parent.Object);
                Assert.NotNull(parent.Object.Id);
                if (parent.Object.Id == folderId)
                {
                    found = true;
                }
            }
            Assert.True(found);

            // check children
            found = false;
            bool hasMore = true;
            long maxItems = 100;
            long skipCount = 0;

            while (hasMore)
            {
                IObjectInFolderList children = Binding.GetNavigationService().GetChildren(RepositoryInfo.Id, folderId, null, null, null, null, null, null, maxItems, skipCount, null);

                Assert.NotNull(children);
                if (children.NumItems != null)
                {
                    Assert.True(children.NumItems > 0);
                }

                foreach (ObjectInFolderData obj in children.Objects)
                {
                    Assert.NotNull(obj);
                    Assert.NotNull(obj.Object);
                    Assert.NotNull(obj.Object.Id);
                    if (obj.Object.Id == objectId)
                    {
                        found = true;
                    }
                }

                skipCount = skipCount + maxItems;

                if (children.HasMoreItems.HasValue)
                {
                    hasMore = children.HasMoreItems.Value;
                }
                else
                {
                    hasMore = children.Objects.Count == maxItems;
                }
            }

            Assert.True(found);

            // check descendants
            if (RepositoryInfo.Capabilities == null ||
                RepositoryInfo.Capabilities.IsGetDescendantsSupported == null ||
                !(bool)RepositoryInfo.Capabilities.IsGetDescendantsSupported)
            {
                return;
            }

            found = false;
            IList<IObjectInFolderContainer> descendants = Binding.GetNavigationService().GetDescendants(RepositoryInfo.Id, folderId, 1, null, null, null, null, null, null);

            Assert.NotNull(descendants);

            foreach (IObjectInFolderContainer obj in descendants)
            {
                Assert.NotNull(obj);
                Assert.NotNull(obj.Object);
                Assert.NotNull(obj.Object.Object);
                Assert.NotNull(obj.Object.Object.Id);
                if (obj.Object.Object.Id == objectId)
                {
                    found = true;
                }
            }
            Assert.True(found);
        }

        public void DeleteObject(string objectId)
        {
            Binding.GetObjectService().DeleteObject(RepositoryInfo.Id, objectId, true, null);

            try
            {
                Binding.GetObjectService().GetObject(RepositoryInfo.Id, objectId, null, null, null, null, null, null, null);
                Assert.Fail("CmisObjectNotFoundException excepted!");
            }
            catch (CmisObjectNotFoundException)
            {
            }
        }

        public string GetTextContent(string objectId)
        {
            IContentStream contentStream = Binding.GetObjectService().GetContentStream(RepositoryInfo.Id, objectId, null, null, null, null);

            Assert.NotNull(contentStream);
            Assert.NotNull(contentStream.Stream);

            MemoryStream memStream = new MemoryStream();
            byte[] buffer = new byte[4096];
            int b;
            while ((b = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                memStream.Write(buffer, 0, b);
            }

            string result = Encoding.UTF8.GetString(memStream.GetBuffer(), 0, (int)memStream.Length);

            return result;
        }

        // ---- asserts ----

        public void AssertAreEqual(IObjectType expected, IObjectType actual)
        {
            if (expected == null && actual == null)
            {
                return;
            }

            Assert.NotNull(expected);
            Assert.NotNull(actual);

            Assert.NotNull(actual.Id);

            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.IsBaseType, actual.IsBaseType);
            Assert.AreEqual(expected.BaseTypeId, actual.BaseTypeId);
            Assert.AreEqual(expected.DisplayName, actual.DisplayName);
            Assert.AreEqual(expected.Description, actual.Description);
            Assert.AreEqual(expected.LocalName, actual.LocalName);
            Assert.AreEqual(expected.LocalNamespace, actual.LocalNamespace);
            Assert.AreEqual(expected.QueryName, actual.QueryName);
            Assert.AreEqual(expected.PropertyDefinitions.Count, actual.PropertyDefinitions.Count);

            foreach (IPropertyDefinition propDef in expected.PropertyDefinitions)
            {
                Assert.NotNull(propDef);
                Assert.NotNull(propDef.Id);

                IPropertyDefinition propDef2 = actual[propDef.Id];

                AssertAreEqual(propDef, propDef2);
            }
        }

        public void AssertAreEqual(IPropertyDefinition expected, IPropertyDefinition actual)
        {
            if (expected == null && actual == null)
            {
                return;
            }

            Assert.NotNull(expected);
            Assert.NotNull(actual);

            Assert.NotNull(actual.Id);

            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.LocalName, actual.LocalName);
            Assert.AreEqual(expected.LocalNamespace, actual.LocalNamespace);
            Assert.AreEqual(expected.DisplayName, actual.DisplayName);
            Assert.AreEqual(expected.Description, actual.Description);
            Assert.AreEqual(expected.QueryName, actual.QueryName);
            Assert.AreEqual(expected.PropertyType, actual.PropertyType);
            Assert.AreEqual(expected.Cardinality, actual.Cardinality);
            Assert.AreEqual(expected.Updatability, actual.Updatability);
        }
    }
}
