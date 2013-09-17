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
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using NUnit.Framework;

namespace DotCMISUnitTest
{
    [TestFixture]
    class EnumeratorTest
    {
        private const int PageSize = 12;

        private IList<int> source;
        private IItemEnumerable<int> testEnumerable;

        [SetUp]
        public void Init()
        {
            source = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                source.Add(i);
            }

            PageFetcher<int>.FetchPage fetchPageDelegate = delegate(long maxNumItems, long skipCount)
            {
                IList<int> page = new List<int>();

                for (int i = (int)skipCount; i < skipCount + maxNumItems; i++)
                {
                    if (source.Count <= i) { break; }
                    page.Add(source[i]);
                }

                return new PageFetcher<int>.Page<int>(page, source.Count, skipCount + maxNumItems < source.Count);
            };

            testEnumerable = new CollectionEnumerable<int>(new PageFetcher<int>(PageSize, fetchPageDelegate));
        }

        [Test]
        public void TestIteration()
        {
            Assert.AreEqual(source.Count, testEnumerable.TotalNumItems);
            Assert.AreEqual(PageSize, testEnumerable.PageNumItems);

            int i = 0;
            foreach (int x in testEnumerable)
            {
                Assert.AreEqual(i, x);
                i++;
            }
        }

        [Test]
        public void TestSkip()
        {
            int i = 42;
            foreach (int x in testEnumerable.SkipTo(42))
            {
                Assert.AreEqual(i, x);
                i++;
            }

            Assert.AreEqual(source.Count, i);
        }

        [Test]
        public void TestOverSkip()
        {
            foreach (int x in testEnumerable.SkipTo(source.Count + 1))
            {
                Assert.Fail();
            }
        }

        [Test]
        public void TestPage()
        {
            int i = 0;
            foreach (int x in testEnumerable.GetPage(8))
            {
                Assert.AreEqual(i, x);
                i++;
            }

            Assert.AreEqual(8, i);
        }

        [Test]
        public void TestBigPage()
        {
            int i = 0;
            foreach (int x in testEnumerable.GetPage(source.Count * 2))
            {
                Assert.AreEqual(i, x);
                i++;
            }

            Assert.AreEqual(source.Count, i);
        }

        [Test]
        public void TestSkipAndPage()
        {
            int i = 42;
            foreach (int x in testEnumerable.SkipTo(42).GetPage(20))
            {
                Assert.AreEqual(i, x);
                i++;
            }

            Assert.AreEqual(62, i);
        }
    }
}
