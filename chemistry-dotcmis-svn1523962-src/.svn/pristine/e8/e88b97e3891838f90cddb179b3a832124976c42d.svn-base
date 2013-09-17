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
using DotCMIS.Data;
using DotCMIS.Enums;
using NUnit.Framework;

namespace DotCMISUnitTest
{
    [TestFixture]
    public class CRUDTest : TestFramework
    {
        [Test]
        public void TestRepositoryInfo()
        {
            Assert.NotNull(RepositoryInfo.Id);
            Assert.NotNull(RepositoryInfo.Name);
            Assert.NotNull(RepositoryInfo.RootFolderId);
        }

        [Test]
        public void TestRootFolder()
        {
            IObjectData rootFolder = GetFullObject(RepositoryInfo.RootFolderId);
            Assert.AreEqual(BaseTypeId.CmisFolder, rootFolder.BaseTypeId);

            IObjectData rootFolder2 = Binding.GetObjectService().GetObjectByPath(RepositoryInfo.Id, "/", null, true, IncludeRelationshipsFlag.Both, null, true, true, null);
            Assert.AreEqual(BaseTypeId.CmisFolder, rootFolder2.BaseTypeId);

            Assert.AreEqual(rootFolder.Id, rootFolder2.Id);
        }

        [Test]
        public void TestCreateDocument()
        {
            string content1 = "my content";

            IObjectData doc = CreateDocument(TestFolder.Id, "dottest", content1);

            string content2 = GetTextContent(doc.Id);
            Assert.AreEqual(content1, content2);

            DeleteObject(doc.Id);
        }

        [Test]
        public void TestCreateFolder()
        {
            IObjectData folder0 = CreateFolder(TestFolder.Id, "folder0");
            IObjectData folder1 = CreateFolder(folder0.Id, "folder1");
            IObjectData folder2 = CreateFolder(folder1.Id, "folder2");
            IObjectData folder3 = CreateFolder(folder2.Id, "folder3");

            DeleteObject(folder3.Id);
            DeleteObject(folder2.Id);
            DeleteObject(folder1.Id);
            DeleteObject(folder0.Id);
        }
    }
}
