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
using DotCMIS.Client;
using DotCMIS.Client.Impl.Cache;
using DotCMIS.Data;
using DotCMIS.Data.Extensions;
using DotCMIS.Enums;
using NUnit.Framework;
using DotCMIS;
using System.Threading;

namespace DotCMISUnitTest
{
    [TestFixture]
    class CacheTest
    {
        [Test]
        public void TestCache()
        {
            IDictionary<string, string> parameters = new Dictionary<string, string>();

            CmisObjectCache cache = new CmisObjectCache();
            cache.Initialize(null, parameters);

            string cacheKey1 = "ck1";
            string cacheKey2 = "ck2";

            // add first object
            MockObject mock1 = new MockObject("1");
            cache.Put(mock1, cacheKey1);

            ICmisObject o1 = cache.GetById(mock1.Id, cacheKey1);
            Assert.NotNull(o1);
            Assert.AreEqual(mock1.Id, o1.Id);
            Assert.Null(cache.GetById(mock1.Id, cacheKey2));
            Assert.Null(cache.GetById("- some id -", cacheKey1));

            // add second object - same id
            MockObject mock2 = new MockObject("1");
            cache.Put(mock2, cacheKey2);

            o1 = cache.GetById(mock1.Id, cacheKey1);
            Assert.NotNull(o1);
            Assert.AreEqual(mock1.Id, o1.Id);

            ICmisObject o2 = cache.GetById(mock2.Id, cacheKey2);
            Assert.NotNull(o2);
            Assert.AreEqual(mock2.Id, o2.Id);

            // add third object - other id

            MockObject mock3 = new MockObject("3");
            cache.Put(mock3, cacheKey1);

            o1 = cache.GetById(mock1.Id, cacheKey1);
            Assert.NotNull(o1);
            Assert.AreEqual(mock1.Id, o1.Id);

            o2 = cache.GetById(mock2.Id, cacheKey2);
            Assert.NotNull(o2);
            Assert.AreEqual(mock2.Id, o2.Id);

            ICmisObject o3 = cache.GetById(mock3.Id, cacheKey1);
            Assert.NotNull(o3);
            Assert.AreEqual(mock3.Id, o3.Id);
        }

        [Test]
        public void TestLRU()
        {
            IDictionary<string, string> parameters = new Dictionary<string, string>();
            parameters[SessionParameter.CacheSizeObjects] = "10";

            CmisObjectCache cache = new CmisObjectCache();
            cache.Initialize(null, parameters);

            string cacheKey1 = "ck1";

            MockObject[] mocks = new MockObject[10];
            for (int i = 0; i < 10; i++)
            {
                mocks[i] = new MockObject("m" + i);
                cache.Put(mocks[i], cacheKey1);
            }

            for (int i = 0; i < 10; i++)
            {
                mocks[i] = new MockObject("m" + i);
                Assert.NotNull(cache.GetById("m" + i, cacheKey1));
            }

            MockObject newMock = new MockObject("new");
            cache.Put(newMock, cacheKey1);
            Assert.NotNull(cache.GetById(newMock.Id, cacheKey1));

            for (int i = 1; i < 10; i++)
            {
                mocks[i] = new MockObject("m" + i);
                Assert.NotNull(cache.GetById("m" + i, cacheKey1));
            }

            Assert.Null(cache.GetById("m0", cacheKey1));
        }

        [Test]
        public void TestExpiration()
        {
            IDictionary<string, string> parameters = new Dictionary<string, string>();
            parameters[SessionParameter.CacheTTLObjects] = "500";

            CmisObjectCache cache = new CmisObjectCache();
            cache.Initialize(null, parameters);

            string cacheKey1 = "ck1";

            MockObject mock1 = new MockObject("1");
            cache.Put(mock1, cacheKey1);

            Assert.NotNull(cache.GetById(mock1.Id, cacheKey1));

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.Null(cache.GetById(mock1.Id, cacheKey1));
        }

        [Test]
        public void TestPath()
        {
            IDictionary<string, string> parameters = new Dictionary<string, string>();

            CmisObjectCache cache = new CmisObjectCache();
            cache.Initialize(null, parameters);

            string cacheKey1 = "ck1";
            string path = "/test/path";

            MockObject mock1 = new MockObject("1");
            cache.PutPath(path, mock1, cacheKey1);

            ICmisObject o1 = cache.GetById(mock1.Id, cacheKey1);
            Assert.NotNull(o1);
            Assert.AreEqual(mock1.Id, o1.Id);

            ICmisObject o2 = cache.GetByPath(path, cacheKey1);
            Assert.NotNull(o2);
            Assert.AreEqual(mock1.Id, o2.Id);

            Assert.Null(cache.GetByPath("/some/other/path/", cacheKey1));
        }
    }

    class MockObject : ICmisObject
    {
        public MockObject(string id)
        {
            Id = id;
        }

        public string Id { get; private set; }
        public IList<IProperty> Properties { get { return null; } }
        public IProperty this[string propertyId] { get { return null; } }
        public object GetPropertyValue(string propertyId) { return null; }
        public string Name { get { return null; } }
        public string CreatedBy { get { return null; } }
        public DateTime? CreationDate { get { return null; } }
        public string LastModifiedBy { get { return null; } }
        public DateTime? LastModificationDate { get { return null; } }
        public BaseTypeId BaseTypeId { get { return BaseTypeId.CmisDocument; } }
        public IObjectType BaseType { get { return null; } }
        public IObjectType ObjectType { get { return null; } }
        public string ChangeToken { get { return null; } }
        public IAllowableActions AllowableActions { get { return null; } }
        public IList<IRelationship> Relationships { get { return null; } }
        public IAcl Acl { get { return null; } }
        public void Delete(bool allVersions) { }
        public ICmisObject UpdateProperties(IDictionary<string, object> properties) { return null; }
        public IObjectId UpdateProperties(IDictionary<string, object> properties, bool refresh) { return null; }
        public IList<IRendition> Renditions { get { return null; } }
        public void ApplyPolicy(params IObjectId[] policyId) { }
        public void RemovePolicy(params IObjectId[] policyId) { }
        public IList<IPolicy> Policies { get { return null; } }
        public IAcl ApplyAcl(IList<IAce> AddAces, IList<IAce> removeAces, AclPropagation? aclPropagation) { return null; }
        public IAcl AddAcl(IList<IAce> AddAces, AclPropagation? aclPropagation) { return null; }
        public IAcl RemoveAcl(IList<IAce> RemoveAces, AclPropagation? aclPropagation) { return null; }
        public IList<ICmisExtensionElement> GetExtensions(ExtensionLevel level) { return null; }
        public DateTime RefreshTimestamp { get { return DateTime.Now; } }
        public void Refresh() { }
        public void RefreshIfOld(long durationInMillis) { }
    }
}
