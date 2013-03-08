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
using System.Text;
using System.Web;
using DotCMIS.Binding.Impl;

namespace DotCMIS.Binding.AtomPub
{
    internal class LinkCache
    {
        private static HashSet<string> KnownLinks = new HashSet<string>();

        static LinkCache()
        {
            KnownLinks.Add(AtomPubConstants.RelACL);
            KnownLinks.Add(AtomPubConstants.RelDown);
            KnownLinks.Add(AtomPubConstants.RelUp);
            KnownLinks.Add(AtomPubConstants.RelFolderTree);
            KnownLinks.Add(AtomPubConstants.RelRelationships);
            KnownLinks.Add(AtomPubConstants.RelSelf);
            KnownLinks.Add(AtomPubConstants.RelAllowableActions);
            KnownLinks.Add(AtomPubConstants.RelEditMedia);
            KnownLinks.Add(AtomPubConstants.RelPolicies);
            KnownLinks.Add(AtomPubConstants.RelVersionHistory);
            KnownLinks.Add(AtomPubConstants.RelWorkingCopy);
            KnownLinks.Add(AtomPubConstants.LinkRelContent);
        }

        private const int CacheSizeRepositories = 10;
        private const int CacheSizeTypes = 100;
        private const int CacheSizeLinks = 400;

        private IBindingCache linkCache;
        private IBindingCache typeLinkCache;
        private IBindingCache collectionLinkCache;
        private IBindingCache templateCache;
        private IBindingCache repositoryLinkCache;

        public LinkCache(BindingSession session)
        {
            int repCount = session.GetValue(SessionParameter.CacheSizeRepositories, CacheSizeRepositories);
            if (repCount < 1)
            {
                repCount = CacheSizeRepositories;
            }

            int typeCount = session.GetValue(SessionParameter.CacheSizeTypes, CacheSizeTypes);
            if (typeCount < 1)
            {
                typeCount = CacheSizeTypes;
            }

            int objCount = session.GetValue(SessionParameter.CacheSizeLinks, CacheSizeLinks);
            if (objCount < 1)
            {
                objCount = CacheSizeLinks;
            }

            linkCache = new Cache("Link Cache");
            linkCache.Initialize(new string[] {
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=" + repCount, // repository
                typeof(LruCacheLevel).FullName + " " + LruCacheLevel.MaxEntries + "=" + objCount, // id
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=16", // rel
                typeof(ContentTypeCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=3,"
                        + DictionaryCacheLevel.SingleValue + "=true" // type
        });

            typeLinkCache = new Cache("Type Link Cache");
            typeLinkCache.Initialize(new string[] {
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=" + repCount, // repository
                typeof(LruCacheLevel).FullName + " " + LruCacheLevel.MaxEntries + "=" + typeCount, // id
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=16", // rel
                typeof(ContentTypeCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=3,"
                        + DictionaryCacheLevel.SingleValue + "=true"// type
        });

            collectionLinkCache = new Cache("Collection Link Cache");
            collectionLinkCache.Initialize(new string[] {
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=" + repCount, // repository
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=8" // collection
        });

            templateCache = new Cache("URI Template Cache");
            templateCache.Initialize(new string[] {
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=" + repCount, // repository
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=6" // type
        });

            repositoryLinkCache = new Cache("Repository Link Cache");
            repositoryLinkCache.Initialize(new string[] {
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=" + repCount, // repository
                typeof(DictionaryCacheLevel).FullName + " " + DictionaryCacheLevel.Capacity + "=6" // rel
        });
        }

        // ---- links ---

        public void AddLink(string repositoryId, string id, string rel, string type, string link)
        {
            if (KnownLinks.Contains(rel))
            {
                linkCache.Put(new string[] { repositoryId, id, rel, type }, link);
            }
        }

        public void RemoveLinks(string repositoryId, string id)
        {
            linkCache.Remove(new string[] { repositoryId, id });
        }

        public string GetLink(string repositoryId, string id, string rel, string type)
        {
            return (string)linkCache.Get(new string[] { repositoryId, id, rel, type });
        }

        public string GetLink(string repositoryId, string id, string rel)
        {
            return GetLink(repositoryId, id, rel, null);
        }

        public int CheckLink(string repositoryId, string id, string rel, string type)
        {
            return linkCache.Check(new string[] { repositoryId, id, rel, type });
        }

        public void LockLinks()
        {
            linkCache.Lock();
        }

        public void UnlockLinks()
        {
            linkCache.Unlock();
        }

        // ---- type links ---

        public void AddTypeLink(string repositoryId, string id, string rel, string type, string link)
        {
            if (KnownLinks.Contains(rel))
            {
                typeLinkCache.Put(new string[] { repositoryId, id, rel, type }, link);
            }
        }

        public void RemoveTypeLinks(string repositoryId, string id)
        {
            typeLinkCache.Remove(new string[] { repositoryId, id });
        }

        public string GetTypeLink(string repositoryId, string id, string rel, string type)
        {
            return (string)typeLinkCache.Get(new string[] { repositoryId, id, rel, type });
        }

        public void LockTypeLinks()
        {
            typeLinkCache.Lock();
        }

        public void UnlockTypeLinks()
        {
            typeLinkCache.Unlock();
        }

        // ---- collections ----

        public void AddCollection(string repositoryId, string collection, string link)
        {
            collectionLinkCache.Put(new string[] { repositoryId, collection }, link);
        }

        public string GetCollection(string repositoryId, string collection)
        {
            return (string)collectionLinkCache.Get(new string[] { repositoryId, collection });
        }

        // ---- templates ----

        public void AddTemplate(string repositoryId, string type, string link)
        {
            templateCache.Put(new string[] { repositoryId, type }, link);
        }

        public string GetTemplateLink(string repositoryId, string type, IDictionary<string, object> parameters)
        {
            string template = (string)templateCache.Get(new string[] { repositoryId, type });
            if (template == null)
            {
                return null;
            }

            StringBuilder result = new StringBuilder();
            StringBuilder param = new StringBuilder();

            bool paramMode = false;
            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];

                if (paramMode)
                {
                    if (c == '}')
                    {
                        paramMode = false;

                        object paramValue;
                        if (parameters.TryGetValue(param.ToString(), out paramValue))
                        {
                            result.Append(paramValue == null ? "" : Uri.EscapeDataString(UrlBuilder.NormalizeParameter(paramValue)));
                        }

                        param = new StringBuilder();
                    }
                    else
                    {
                        param.Append(c);
                    }
                }
                else
                {
                    if (c == '{')
                    {
                        paramMode = true;
                    }
                    else
                    {
                        result.Append(c);
                    }
                }
            }

            return result.ToString();
        }

        // ---- repository links ----

        public void AddRepositoryLink(string repositoryId, string rel, string link)
        {
            repositoryLinkCache.Put(new string[] { repositoryId, rel }, link);
        }

        public string GetRepositoryLink(string repositoryId, string rel)
        {
            return (string)repositoryLinkCache.Get(new string[] { repositoryId, rel });
        }

        // ---- clear ----

        public void ClearRepository(string repositoryId)
        {
            linkCache.Remove(new string[] { repositoryId });
            typeLinkCache.Remove(new string[] { repositoryId });
            collectionLinkCache.Remove(new string[] { repositoryId });
            templateCache.Remove(new string[] { repositoryId });
            repositoryLinkCache.Remove(new string[] { repositoryId });
        }
    }

    internal class ContentTypeCacheLevel : DictionaryCacheLevel
    {
        public ContentTypeCacheLevel()
        {
            EnableKeyFallback(NullKey);
        }

        public override object this[string key]
        {
            get
            {
                return base[Normalize(key)];
            }
            set
            {
                base[Normalize(key)] = value;
            }
        }

        public override void Remove(string key)
        {
            base.Remove(Normalize(key));
        }

        private string Normalize(string key)
        {
            if (key == null)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();
            int parameterStart = 0;

            // first, get the MIME type
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];

                if (Char.IsWhiteSpace(c))
                {
                    continue;
                }
                else if (c == ';')
                {
                    parameterStart = i;
                    break;
                }

                sb.Append(Char.ToLower(c));
            }

            // if parameters have been found, gather them
            if (parameterStart > 0)
            {
                SortedList<string, string> parameter = new SortedList<string, string>();
                StringBuilder ksb = new StringBuilder();
                StringBuilder vsb = new StringBuilder();
                bool isKey = true;

                for (int i = parameterStart + 1; i < key.Length; i++)
                {
                    char c = key[i];
                    if (Char.IsWhiteSpace(c))
                    {
                        continue;
                    }

                    if (isKey)
                    {
                        if (c == '=')
                        {
                            // value start
                            isKey = false;
                            continue;
                        }

                        ksb.Append(Char.ToLower(c));
                    }
                    else
                    {
                        if (c == ';')
                        {
                            // next key
                            isKey = true;

                            parameter.Add(ksb.ToString(), vsb.ToString());

                            ksb = new StringBuilder();
                            vsb = new StringBuilder();

                            continue;
                        }
                        else if (c == '"')
                        {
                            // filter quotes
                            continue;
                        }

                        vsb.Append(Char.ToLower(c));
                    }
                }

                // add last parameter
                if (ksb.Length > 0)
                {
                    parameter.Add(ksb.ToString(), vsb.ToString());
                }

                // write parameters sorted by key
                for (int i = 0; i < parameter.Count; i++)
                {
                    sb.Append(";");
                    sb.Append(parameter.Keys[i]);
                    sb.Append("=");
                    sb.Append(parameter.Values[i]);
                }
            }

            return sb.ToString();
        }
    }
}
