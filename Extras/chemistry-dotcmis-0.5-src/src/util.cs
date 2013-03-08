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
using System.Diagnostics;
using System.Linq;

namespace DotCMIS.Util
{
    internal class DotCMISDebug
    {
        public static TraceSwitch DotCMISSwitch = new TraceSwitch("DotCMIS", "DotCMIS");
    }

    /// <summary>
    /// LRU cache implementation. Not thread safe!
    /// </summary>
    internal class LRUCache<K, V>
    {
        private int capacity;
        private TimeSpan ttl;
        private Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>> cacheDict = new Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>>();
        private LinkedList<LRUCacheItem<K, V>> lruList = new LinkedList<LRUCacheItem<K, V>>();

        public LRUCache(int capacity, TimeSpan ttl)
        {
            this.capacity = capacity;
            this.ttl = ttl;
        }

        public V Get(K key)
        {
            LinkedListNode<LRUCacheItem<K, V>> node;
            if (cacheDict.TryGetValue(key, out node))
            {
                lruList.Remove(node);

                if (node.Value.IsExpired)
                {
                    cacheDict.Remove(node.Value.Key);
                    return default(V);
                }

                lruList.AddLast(node);

                return node.Value.Value;
            }

            return default(V);
        }

        public V GetLatest()
        {
            if (lruList.Count == 0)
            {
                return default(V);
            }

            return lruList.First().Value;
        }

        public void Add(K key, V val)
        {
            Remove(key);

            if (cacheDict.Count >= capacity)
            {
                RemoveFirst();
            }

            LRUCacheItem<K, V> cacheItem = new LRUCacheItem<K, V>(key, val, DateTime.UtcNow + ttl);
            LinkedListNode<LRUCacheItem<K, V>> node = new LinkedListNode<LRUCacheItem<K, V>>(cacheItem);

            lruList.AddLast(node);
            cacheDict.Add(key, node);
        }

        protected void RemoveFirst()
        {
            LinkedListNode<LRUCacheItem<K, V>> node = lruList.First;
            lruList.RemoveFirst();
            cacheDict.Remove(node.Value.Key);
        }

        public void Remove(K key)
        {
            LinkedListNode<LRUCacheItem<K, V>> node;
            if (cacheDict.TryGetValue(key, out node))
            {
                lruList.Remove(node);
                cacheDict.Remove(node.Value.Key);
            }
        }

        public int Count
        {
            get { return lruList.Count; }
        }
    }

    internal class LRUCacheItem<K, V>
    {
        public LRUCacheItem(K key, V value, DateTime expiration)
        {
            Key = key;
            Value = value;
            Expiration = expiration;
        }

        public K Key { get; private set; }

        public V Value { get; private set; }

        public DateTime Expiration { get; private set; }

        public bool IsExpired
        {
            get { return Value == null || DateTime.UtcNow > Expiration; }
        }
    }
}
