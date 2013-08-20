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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DotCMIS.Enums;

namespace DotCMIS.Client.Impl
{
    /// <summary>
    /// Operation context implementation.
    /// </summary>
    public class OperationContext : IOperationContext
    {
        public const string PropertiesStar = "*";
        public const string RenditionNone = "cmis:none";

        private HashSet<string> filter;
        private bool includeAllowableActions;
        private bool includeAcls;
        private IncludeRelationshipsFlag? includeRelationships;
        private bool includePolicies;
        private HashSet<string> renditionFilter;
        private bool includePathSegments;
        private string orderBy;
        private bool cacheEnabled;
        private string cacheKey;
        private int maxItemsPerPage;

        public OperationContext()
        {
            filter = null;
            includeAcls = false;
            includeAllowableActions = true;
            includePolicies = false;
            includeRelationships = IncludeRelationshipsFlag.None;
            renditionFilter = null;
            includePathSegments = true;
            orderBy = null;
            cacheEnabled = false;
            maxItemsPerPage = 100;

            GenerateCacheKey();
        }

        public OperationContext(IOperationContext source)
        {
            filter = (source.Filter == null ? null : new HashSet<string>(source.Filter));
            includeAcls = source.IncludeAcls;
            includeAllowableActions = source.IncludeAllowableActions;
            includePolicies = source.IncludePolicies;
            includeRelationships = source.IncludeRelationships;
            renditionFilter = (source.RenditionFilter == null ? null : new HashSet<string>(source.RenditionFilter));
            includePathSegments = source.IncludePathSegments;
            orderBy = source.OrderBy;
            cacheEnabled = source.CacheEnabled;
            maxItemsPerPage = source.MaxItemsPerPage;

            GenerateCacheKey();
        }

        public OperationContext(HashSet<string> filter, bool includeAcls, bool includeAllowableActions,
            bool includePolicies, IncludeRelationshipsFlag includeRelationships, HashSet<string> renditionFilter,
            bool includePathSegments, String orderBy, bool cacheEnabled, int maxItemsPerPage)
        {
            this.filter = filter;
            this.includeAcls = includeAcls;
            this.includeAllowableActions = includeAllowableActions;
            this.includePolicies = includePolicies;
            this.includeRelationships = includeRelationships;
            this.renditionFilter = renditionFilter;
            this.includePathSegments = includePathSegments;
            this.orderBy = orderBy;
            this.cacheEnabled = cacheEnabled;
            this.maxItemsPerPage = maxItemsPerPage;

            GenerateCacheKey();
        }

        public HashSet<string> Filter
        {
            get { return filter == null ? null : new HashSet<string>(filter); }
            set
            {
                if (value != null)
                {
                    HashSet<string> tempSet = new HashSet<string>();
                    foreach (string oid in value)
                    {
                        if (oid == null) { continue; }

                        string toid = oid.Trim();
                        if (toid.Length == 0) { continue; }
                        if (toid == PropertiesStar)
                        {
                            tempSet = new HashSet<string>();
                            tempSet.Add(PropertiesStar);
                            break;
                        }
                        if (toid.IndexOf(',') > -1)
                        {
                            throw new ArgumentException("Query id must not contain a comma!");
                        }

                        tempSet.Add(toid);
                    }

                    if (tempSet.Count == 0) { filter = null; }
                    else { filter = tempSet; }
                }
                else
                {
                    filter = null;
                }

                GenerateCacheKey();
            }
        }

        public string FilterString
        {
            get
            {
                if (filter == null) { return null; }

                if (filter.Contains(PropertiesStar))
                {
                    return PropertiesStar;
                }

                this.filter.Add(PropertyIds.ObjectId);
                this.filter.Add(PropertyIds.BaseTypeId);
                this.filter.Add(PropertyIds.ObjectTypeId);

                StringBuilder sb = new StringBuilder();

                foreach (String oid in filter)
                {
                    if (sb.Length > 0) { sb.Append(','); }
                    sb.Append(oid);
                }

                return sb.ToString();
            }

            set
            {
                if (value == null || value.Trim().Length == 0)
                {
                    Filter = null;
                    return;
                }

                string[] ids = value.Split(',');
                HashSet<string> tempSet = new HashSet<string>();
                foreach (string qid in ids)
                {
                    tempSet.Add(qid);
                }

                Filter = tempSet;
            }
        }

        public bool IncludeAllowableActions
        {
            get { return includeAllowableActions; }
            set { includeAllowableActions = value; GenerateCacheKey(); }
        }

        public bool IncludeAcls
        {
            get { return includeAcls; }
            set { includeAcls = value; GenerateCacheKey(); }
        }

        public IncludeRelationshipsFlag? IncludeRelationships
        {
            get { return includeRelationships; }
            set { includeRelationships = value; GenerateCacheKey(); }
        }

        public bool IncludePolicies
        {
            get { return includePolicies; }
            set { includePolicies = value; GenerateCacheKey(); }
        }

        public HashSet<string> RenditionFilter
        {
            get { return renditionFilter == null ? null : new HashSet<string>(renditionFilter); }
            set
            {
                HashSet<string> tempSet = new HashSet<string>();
                if (value != null)
                {
                    foreach (String rf in value)
                    {
                        if (rf == null) { continue; }

                        String trf = rf.Trim();
                        if (trf.Length == 0) { continue; }
                        if (trf.IndexOf(',') > -1)
                        {
                            throw new ArgumentException("Rendition must not contain a comma!");
                        }

                        tempSet.Add(trf);
                    }

                    if (tempSet.Count == 0)
                    {
                        tempSet.Add(RenditionNone);
                    }
                }
                else
                {
                    tempSet.Add(RenditionNone);
                }

                renditionFilter = tempSet;

                GenerateCacheKey();
            }
        }

        public string RenditionFilterString
        {
            get
            {
                if (renditionFilter == null) { return null; }

                StringBuilder sb = new StringBuilder();
                foreach (string rf in renditionFilter)
                {
                    if (sb.Length > 0) { sb.Append(','); }
                    sb.Append(rf);
                }

                return sb.ToString();
            }

            set
            {
                if (value == null || value.Trim().Length == 0)
                {
                    RenditionFilter = null;
                    return;
                }

                string[] renditions = value.Split(',');
                HashSet<string> tempSet = new HashSet<string>();
                foreach (string rend in renditions)
                {
                    tempSet.Add(rend);
                }

                RenditionFilter = tempSet;
            }
        }

        public bool IncludePathSegments
        {
            get { return includePathSegments; }
            set { includePathSegments = value; GenerateCacheKey(); }
        }

        public string OrderBy
        {
            get { return orderBy; }
            set { orderBy = value; GenerateCacheKey(); }
        }

        public bool CacheEnabled
        {
            get { return cacheEnabled; }
            set { cacheEnabled = value; GenerateCacheKey(); }
        }

        public string CacheKey
        {
            get { return cacheKey; }
        }

        public int MaxItemsPerPage
        {
            get { return maxItemsPerPage; }
            set { maxItemsPerPage = value; }
        }

        protected void GenerateCacheKey()
        {
            if (!cacheEnabled)
            {
                cacheKey = null;
            }

            StringBuilder sb = new StringBuilder();

            sb.Append(includeAcls ? "1" : "0");
            sb.Append(includeAllowableActions ? "1" : "0");
            sb.Append(includePolicies ? "1" : "0");
            sb.Append("|");
            sb.Append(filter == null ? "" : FilterString);
            sb.Append("|");
            sb.Append(includeRelationships == null ? "" : includeRelationships.GetCmisValue());

            sb.Append("|");
            sb.Append(renditionFilter == null ? "" : RenditionFilterString);

            cacheKey = sb.ToString();
        }
    }

    /// <summary>
    /// Object id implementation.
    /// </summary>
    public class ObjectId : IObjectId
    {
        private string id;
        public string Id
        {
            get { return id; }
            set
            {
                if (value == null || value.Length == 0)
                {
                    throw new ArgumentException("Id must be set!");
                }

                id = value;
            }
        }

        public ObjectId(string id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// Tree implementation.
    /// </summary>
    public class Tree<T> : ITree<T>
    {
        public T Item { get; set; }
        public IList<ITree<T>> Children { get; set; }
    }

    /// <summary>
    /// Base class for IItemEnumerable's.
    /// </summary>
    public abstract class AbstractEnumerable<T> : IItemEnumerable<T>
    {
        private AbstractEnumerator<T> enumerator;
        protected AbstractEnumerator<T> Enumerator
        {
            get
            {
                if (enumerator == null) { enumerator = CreateEnumerator(); }
                return enumerator;
            }
        }

        protected PageFetcher<T> PageFetcher { get; set; }
        protected long SkipCount { get; private set; }

        public AbstractEnumerable(PageFetcher<T> pageFetcher) :
            this(0, pageFetcher) { }

        protected AbstractEnumerable(long position, PageFetcher<T> pageFetcher)
        {
            this.PageFetcher = pageFetcher;
            this.SkipCount = position;
        }

        protected abstract AbstractEnumerator<T> CreateEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return Enumerator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Enumerator;
        }

        public IItemEnumerable<T> SkipTo(long position)
        {
            return new CollectionEnumerable<T>(position, PageFetcher);
        }

        public IItemEnumerable<T> GetPage()
        {
            return new CollectionPageEnumerable<T>(SkipCount, PageFetcher);
        }

        public IItemEnumerable<T> GetPage(int maxNumItems)
        {
            PageFetcher.MaxNumItems = maxNumItems;
            return new CollectionPageEnumerable<T>(SkipCount, PageFetcher);
        }

        public long PageNumItems { get { return Enumerator.PageNumItems; } }

        public bool HasMoreItems { get { return Enumerator.HasMoreItems; } }

        public long TotalNumItems { get { return Enumerator.TotalNumItems; } }
    }

    /// <summary>
    /// Abstract Enumerator implementation.
    /// </summary>
    public abstract class AbstractEnumerator<T> : IEnumerator<T>
    {
        private PageFetcher<T> pageFetcher;
        private PageFetcher<T>.Page<T> page = null;
        private long? totalNumItems = null;
        private bool? hasMoreItems = null;

        protected T Current;

        public AbstractEnumerator(long skipCount, PageFetcher<T> pageFetcher)
        {
            this.SkipCount = skipCount;
            this.pageFetcher = pageFetcher;
        }

        T IEnumerator<T>.Current { get { return Current; } }
        object IEnumerator.Current { get { return Current; } }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public abstract bool MoveNext();

        public void Dispose() { }

        public long SkipCount { get; protected set; }

        public int SkipOffset { get; protected set; }

        public long Position { get { return SkipCount + SkipOffset; } }

        public long PageNumItems
        {
            get
            {
                PageFetcher<T>.Page<T> page = GetCurrentPage();
                if (page != null)
                {
                    IList<T> items = page.Items;
                    if (items != null)
                    {
                        return items.Count;
                    }
                }
                return 0;
            }
        }

        public long TotalNumItems
        {
            get
            {
                if (totalNumItems == null)
                {
                    totalNumItems = -1;
                    PageFetcher<T>.Page<T> page = GetCurrentPage();
                    if (page != null)
                    {
                        totalNumItems = page.TotalNumItems;
                    }
                }
                return (long)totalNumItems;
            }
        }

        public bool HasMoreItems
        {
            get
            {
                if (hasMoreItems == null)
                {
                    hasMoreItems = false;
                    PageFetcher<T>.Page<T> page = GetCurrentPage();
                    if (page != null)
                    {
                        if (page.HasMoreItems.HasValue)
                        {
                            hasMoreItems = page.HasMoreItems;
                        }
                    }
                }
                return (bool)hasMoreItems;
            }
        }

        protected int IncrementSkipOffset()
        {
            return SkipOffset++;
        }

        protected PageFetcher<T>.Page<T> GetCurrentPage()
        {
            if (page == null)
            {
                page = pageFetcher.FetchNextPage(SkipCount);
            }
            return page;
        }

        protected PageFetcher<T>.Page<T> IncrementPage()
        {
            SkipCount += SkipOffset;
            SkipOffset = 0;
            totalNumItems = null;
            hasMoreItems = null;
            page = pageFetcher.FetchNextPage(SkipCount);
            return page;
        }
    }

    /// <summary>
    /// Page fetcher.
    /// </summary>
    public class PageFetcher<T>
    {
        public delegate Page<T> FetchPage(long maxNumItems, long skipCount);

        private FetchPage fetchPageDelegate;

        public PageFetcher(long maxNumItems, FetchPage fetchPageDelegate)
        {
            MaxNumItems = maxNumItems;
            this.fetchPageDelegate = fetchPageDelegate;
        }

        public long MaxNumItems { get; set; }

        public Page<T> FetchNextPage(long skipCount)
        {
            return fetchPageDelegate(MaxNumItems, skipCount);
        }

        public class Page<P>
        {
            public Page(IList<P> items, long? totalNumItems, bool? hasMoreItems)
            {
                Items = items;
                TotalNumItems = totalNumItems;
                HasMoreItems = hasMoreItems;
            }

            public IList<P> Items { get; private set; }
            public long? TotalNumItems { get; private set; }
            public bool? HasMoreItems { get; private set; }
        }
    }

    /// <summary>
    /// CMIS Collection Enumerable.
    /// </summary>
    public class CollectionEnumerable<T> : AbstractEnumerable<T>
    {
        public CollectionEnumerable(PageFetcher<T> pageFetcher) :
            this(0, pageFetcher) { }

        public CollectionEnumerable(long position, PageFetcher<T> pageFetcher) :
            base(position, pageFetcher) { }

        protected override AbstractEnumerator<T> CreateEnumerator()
        {
            return new CollectionEnumerator<T>(SkipCount, PageFetcher);
        }
    }

    /// <summary>
    /// Enumerator for iterating over all items in a CMIS Collection.
    /// </summary>
    public class CollectionEnumerator<T> : AbstractEnumerator<T>
    {
        public CollectionEnumerator(long skipCount, PageFetcher<T> pageFetcher) :
            base(skipCount, pageFetcher) { }

        public override bool MoveNext()
        {
            PageFetcher<T>.Page<T> page = GetCurrentPage();
            if (page == null)
            {
                return false;
            }

            IList<T> items = page.Items;
            if (items == null || items.Count == 0)
            {
                return false;
            }

            if (SkipOffset == items.Count)
            {
                if (!HasMoreItems)
                {
                    return false;
                } 

                page = IncrementPage();
                items = page == null ? null : page.Items;
            }

            if (items == null || items.Count == 0 || SkipOffset == items.Count)
            {
                return false;
            }

            Current = items[IncrementSkipOffset()];

            return true;
        }
    }

    /// <summary>
    /// Enumerable for a CMIS Collection Page.
    /// </summary>
    public class CollectionPageEnumerable<T> : AbstractEnumerable<T>
    {
        public CollectionPageEnumerable(PageFetcher<T> pageFetcher) :
            this(0, pageFetcher) { }

        public CollectionPageEnumerable(long position, PageFetcher<T> pageFetcher) :
            base(position, pageFetcher) { }

        protected override AbstractEnumerator<T> CreateEnumerator()
        {
            return new CollectionPageEnumerator<T>(SkipCount, PageFetcher);
        }
    }

    /// <summary>
    /// Enumerator for iterating over a page of items in a CMIS Collection.
    /// </summary>
    public class CollectionPageEnumerator<T> : AbstractEnumerator<T>
    {
        public CollectionPageEnumerator(long skipCount, PageFetcher<T> pageFetcher) :
            base(skipCount, pageFetcher) { }

        public override bool MoveNext()
        {
            PageFetcher<T>.Page<T> page = GetCurrentPage();
            if (page == null)
            {
                return false;
            }

            IList<T> items = page.Items;
            if (items == null || items.Count == 0 || SkipOffset == items.Count)
            {
                return false;
            }

            Current = items[IncrementSkipOffset()];

            return true;
        }
    }
}
