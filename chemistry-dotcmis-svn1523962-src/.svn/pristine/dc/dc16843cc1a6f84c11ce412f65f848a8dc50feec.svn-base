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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using DotCMIS.Data;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace DotCMISUnitTest
{
    [TestFixture]
    public class TypeTest : TestFramework
    {
        [Test]
        public void TestBaseTypes()
        {
            ITypeDefinition type;

            // cmis:document
            type = Binding.GetRepositoryService().GetTypeDefinition(RepositoryInfo.Id, "cmis:document", null);

            Assert.NotNull(type);
            Assert.AreEqual(BaseTypeId.CmisDocument, type.BaseTypeId);
            Assert.AreEqual("cmis:document", type.Id);

            // cmis:folder
            type = Binding.GetRepositoryService().GetTypeDefinition(RepositoryInfo.Id, "cmis:folder", null);

            Assert.NotNull(type);
            Assert.AreEqual(BaseTypeId.CmisFolder, type.BaseTypeId);
            Assert.AreEqual("cmis:folder", type.Id);

            // cmis:relationship
            try
            {
                type = Binding.GetRepositoryService().GetTypeDefinition(RepositoryInfo.Id, "cmis:relationship", null);

                Assert.NotNull(type);
                Assert.AreEqual(BaseTypeId.CmisRelationship, type.BaseTypeId);
                Assert.AreEqual("cmis:relationship", type.Id);
            }
            catch (CmisObjectNotFoundException)
            {
                // not supported by the repository
            }

            // cmis:policy
            try
            {
                type = Binding.GetRepositoryService().GetTypeDefinition(RepositoryInfo.Id, "cmis:policy", null);

                Assert.NotNull(type);
                Assert.AreEqual(BaseTypeId.CmisPolicy, type.BaseTypeId);
                Assert.AreEqual("cmis:policy", type.Id);
            }
            catch (CmisObjectNotFoundException)
            {
                // not supported by the repository
            }
        }

        [Test]
        public void TestTypeChildren()
        {
            ITypeDefinitionList typeList = Binding.GetRepositoryService().GetTypeChildren(RepositoryInfo.Id, null, null, null, null, null);

            Assert.NotNull(typeList);
            Assert.NotNull(typeList.List);
            Assert.NotNull(typeList.NumItems);
            Assert.True(typeList.NumItems >= 2);
            Assert.True(typeList.NumItems <= 4);

            bool foundDocument = false;
            bool foundFolder = false;
            foreach (ITypeDefinition type in typeList.List)
            {
                Assert.NotNull(type);
                Assert.NotNull(type.Id);

                if (type.Id == "cmis:document")
                {
                    foundDocument = true;
                }
                if (type.Id == "cmis:folder")
                {
                    foundFolder = true;
                }
            }

            Assert.True(foundDocument);
            Assert.True(foundFolder);
        }
    }
}
