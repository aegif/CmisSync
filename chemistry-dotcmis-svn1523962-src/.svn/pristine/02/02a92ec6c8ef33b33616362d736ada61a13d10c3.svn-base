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
using System.IO;
using System.Net;
using DotCMIS.Binding.Impl;
using DotCMIS.Binding.Services;
using DotCMIS.CMISWebServicesReference;
using DotCMIS.Data;
using DotCMIS.Data.Extensions;
using DotCMIS.Data.Impl;
using DotCMIS.Enums;
using DotCMIS.Exceptions;

namespace DotCMIS.Binding.AtomPub
{
    /// <summary>
    /// AtomPub binding SPI.
    /// </summary>
    internal class CmisAtomPubSpi : ICmisSpi
    {
        private RepositoryService repositoryService;
        private NavigationService navigationService;
        private ObjectService objectService;
        private VersioningService versioningService;
        private DiscoveryService discoveryService;
        private MultiFilingService multiFilingService;
        private RelationshipService relationshipService;
        private PolicyService policyService;
        private AclService aclService;

        public void initialize(BindingSession session)
        {
            repositoryService = new RepositoryService(session);
            navigationService = new NavigationService(session);
            objectService = new ObjectService(session);
            versioningService = new VersioningService(session);
            discoveryService = new DiscoveryService(session);
            multiFilingService = new MultiFilingService(session);
            relationshipService = new RelationshipService(session);
            policyService = new PolicyService(session);
            aclService = new AclService(session);
        }

        public IRepositoryService GetRepositoryService()
        {
            return repositoryService;
        }

        public INavigationService GetNavigationService()
        {
            return navigationService;
        }

        public IObjectService GetObjectService()
        {
            return objectService;
        }

        public IVersioningService GetVersioningService()
        {
            return versioningService;
        }

        public IRelationshipService GetRelationshipService()
        {
            return relationshipService;
        }

        public IDiscoveryService GetDiscoveryService()
        {
            return discoveryService;
        }

        public IMultiFilingService GetMultiFilingService()
        {
            return multiFilingService;
        }

        public IAclService GetAclService()
        {
            return aclService;
        }

        public IPolicyService GetPolicyService()
        {
            return policyService;
        }

        public void ClearAllCaches()
        {
            // nothing to do
        }

        public void ClearRepositoryCache(string repositoryId)
        {
            // nothing to do
        }

        public void Dispose()
        {
            // nothing to do
        }
    }

    internal abstract class AbstractAtomPubService
    {
        protected const string NameCollection = "collection";
        protected const string NameURITemplate = "uritemplate";
        protected const string NamePathSegment = "pathSegment";
        protected const string NameRelativePathSegment = "relativePathSegment";
        protected const string NameNumItems = "numItems";

        private const string SessionLinkCache = "org.apache.chemistry.dotcmis.binding.atompub.linkcache";

        protected enum IdentifierType
        {
            Id, Path
        };

        protected BindingSession Session { get; set; }


        // ---- link cache ----

        protected LinkCache GetLinkCache()
        {
            LinkCache linkCache = (LinkCache)Session.GetValue(SessionLinkCache);
            if (linkCache == null)
            {
                linkCache = new LinkCache(Session);
                Session.PutValue(SessionLinkCache, linkCache);
            }

            return linkCache;
        }

        protected string GetLink(string repositoryId, string id, string rel, string type)
        {
            if (repositoryId == null)
            {
                throw new CmisInvalidArgumentException("Repository id must be set!");
            }

            if (id == null)
            {
                throw new CmisInvalidArgumentException("Object id must be set!");
            }

            return GetLinkCache().GetLink(repositoryId, id, rel, type);
        }

        protected string GetLink(string repositoryId, string id, string rel)
        {
            return GetLink(repositoryId, id, rel, null);
        }

        protected string LoadLink(string repositoryId, string id, string rel, string type)
        {
            string link = GetLink(repositoryId, id, rel, type);
            if (link == null)
            {
                GetObjectInternal(repositoryId, IdentifierType.Id, id, ReturnVersion.This, null, null, null, null, null, null, null);
                link = GetLink(repositoryId, id, rel, type);
            }

            return link;
        }

        protected void AddLink(string repositoryId, string id, string rel, string type, string link)
        {
            GetLinkCache().AddLink(repositoryId, id, rel, type, link);
        }

        protected void AddLink(string repositoryId, string id, AtomLink link)
        {
            GetLinkCache().AddLink(repositoryId, id, link.Rel, link.Type, link.Href);
        }

        protected void RemoveLinks(string repositoryId, string id)
        {
            GetLinkCache().RemoveLinks(repositoryId, id);
        }

        protected void LockLinks()
        {
            GetLinkCache().LockLinks();
        }

        protected void UnlockLinks()
        {
            GetLinkCache().UnlockLinks();
        }

        protected string GetTypeLink(string repositoryId, string typeId, string rel, string type)
        {
            if (repositoryId == null)
            {
                throw new CmisInvalidArgumentException("Repository id must be set!");
            }

            if (typeId == null)
            {
                throw new CmisInvalidArgumentException("Type id must be set!");
            }

            return GetLinkCache().GetTypeLink(repositoryId, typeId, rel, type);
        }

        protected string GetTypeLink(string repositoryId, string typeId, string rel)
        {
            return GetTypeLink(repositoryId, typeId, rel, null);
        }

        protected string LoadTypeLink(string repositoryId, string typeId, string rel, string type)
        {
            string link = GetTypeLink(repositoryId, typeId, rel, type);
            if (link == null)
            {
                GetTypeDefinitionInternal(repositoryId, typeId);
                link = GetTypeLink(repositoryId, typeId, rel, type);
            }

            return link;
        }

        protected void AddTypeLink(string repositoryId, string typeId, string rel, string type, string link)
        {
            GetLinkCache().AddTypeLink(repositoryId, typeId, rel, type, link);
        }

        protected void AddTypeLink(string repositoryId, string typeId, AtomLink link)
        {
            GetLinkCache().AddTypeLink(repositoryId, typeId, link.Rel, link.Type, link.Href);
        }

        protected void RemoveTypeLinks(string repositoryId, string id)
        {
            GetLinkCache().RemoveTypeLinks(repositoryId, id);
        }

        protected void LockTypeLinks()
        {
            GetLinkCache().LockTypeLinks();
        }

        protected void UnlockTypeLinks()
        {
            GetLinkCache().UnlockTypeLinks();
        }

        protected string GetCollection(string repositoryId, string collection)
        {
            return GetLinkCache().GetCollection(repositoryId, collection);
        }

        protected string LoadCollection(string repositoryId, string collection)
        {
            string link = GetCollection(repositoryId, collection);
            if (link == null)
            {
                GetRepositoriesInternal(repositoryId);
                link = GetCollection(repositoryId, collection);
            }

            return link;
        }

        protected void AddCollection(string repositoryId, string collection, string link)
        {
            GetLinkCache().AddCollection(repositoryId, collection, link);
        }

        protected void AddCollection(string repositoryId, IDictionary<string, string> colDict)
        {
            string collection = null;
            colDict.TryGetValue("collectionType", out collection);

            string link = null;
            colDict.TryGetValue("href", out link);

            AddCollection(repositoryId, collection, link);
        }

        protected string GetRepositoryLink(string repositoryId, string rel)
        {
            return GetLinkCache().GetRepositoryLink(repositoryId, rel);
        }

        protected string LoadRepositoryLink(string repositoryId, string rel)
        {
            string link = GetRepositoryLink(repositoryId, rel);
            if (link == null)
            {
                GetRepositoriesInternal(repositoryId);
                link = GetRepositoryLink(repositoryId, rel);
            }

            return link;
        }

        protected void AddRepositoryLink(string repositoryId, string rel, string link)
        {
            GetLinkCache().AddRepositoryLink(repositoryId, rel, link);
        }

        protected void AddRepositoryLink(string repositoryId, AtomLink link)
        {
            AddRepositoryLink(repositoryId, link.Rel, link.Href);
        }

        protected string GetTemplateLink(string repositoryId, string type, IDictionary<string, object> parameters)
        {
            return GetLinkCache().GetTemplateLink(repositoryId, type, parameters);
        }

        protected string LoadTemplateLink(string repositoryId, string type, IDictionary<string, object> parameters)
        {
            string link = GetTemplateLink(repositoryId, type, parameters);
            if (link == null)
            {
                GetRepositoriesInternal(repositoryId);
                link = GetTemplateLink(repositoryId, type, parameters);
            }

            return link;
        }

        protected void AddTemplate(string repositoryId, string type, string link)
        {
            GetLinkCache().AddTemplate(repositoryId, type, link);
        }

        protected void AddTemplate(string repositoryId, IDictionary<string, string> tempDict)
        {
            string type = null;
            tempDict.TryGetValue("type", out type);

            string template = null;
            tempDict.TryGetValue("template", out template);

            AddTemplate(repositoryId, type, template);
        }

        // ---- exceptions ----

        protected CmisBaseException ConvertStatusCode(HttpStatusCode code, string message, string errorContent, Exception e)
        {
            switch (code)
            {
                case HttpStatusCode.BadRequest:
                    return new CmisInvalidArgumentException(message, errorContent, e);
                case HttpStatusCode.NotFound:
                    return new CmisObjectNotFoundException(message, errorContent, e);
                case HttpStatusCode.Forbidden:
                    return new CmisPermissionDeniedException(message, errorContent, e);
                case HttpStatusCode.MethodNotAllowed:
                    return new CmisNotSupportedException(message, errorContent, e);
                case HttpStatusCode.Conflict:
                    return new CmisConstraintException(message, errorContent, e);
                default:
                    return new CmisRuntimeException(message, errorContent, e);
            }
        }

        protected void ThrowLinkException(String repositoryId, String id, String rel, String type)
        {
            int index = GetLinkCache().CheckLink(repositoryId, id, rel, type);

            switch (index)
            {
                case 0:
                    throw new CmisObjectNotFoundException("Unknown repository!");
                case 1:
                    throw new CmisObjectNotFoundException("Unknown object!");
                case 2:
                    throw new CmisNotSupportedException("Operation not supported by the repository for this object!");
                case 3:
                    throw new CmisNotSupportedException("No link with matching media type!");
                case 4:
                    throw new CmisRuntimeException("Nothing wrong! Either this is a bug or threading issue.");
                default:
                    throw new CmisRuntimeException("Unknown error!");
            }
        }

        // ---- helpers ----

        protected T Parse<T>(Stream stream) where T : AtomBase
        {
            AtomPubParser parser = new AtomPubParser(stream);

            try
            {
                parser.Parse();
            }
            catch (Exception e)
            {
                throw new CmisConnectionException("Parsing exception!", e);
            }

            AtomBase parseResult = parser.GetParseResults();

            if (!typeof(T).IsInstanceOfType(parseResult))
            {
                throw new CmisConnectionException("Unexpected document! Received "
                        + (parseResult == null ? "something unknown" : parseResult.GetAtomType()) + "!");
            }

            return (T)parseResult;
        }

        protected HttpUtils.Response Read(UrlBuilder url)
        {
            HttpUtils.Response resp = HttpUtils.InvokeGET(url, Session);

            if (resp.StatusCode != HttpStatusCode.OK)
            {
                throw ConvertStatusCode(resp.StatusCode, resp.Message, resp.ErrorContent, null);
            }

            return resp;
        }

        protected HttpUtils.Response Post(UrlBuilder url, string contentType, HttpUtils.Output writer)
        {
            HttpUtils.Response resp = HttpUtils.InvokePOST(url, contentType, writer, Session);

            if (resp.StatusCode != HttpStatusCode.Created)
            {
                throw ConvertStatusCode(resp.StatusCode, resp.Message, resp.ErrorContent, null);
            }

            return resp;
        }

        protected HttpUtils.Response Put(UrlBuilder url, string contentType, HttpUtils.Output writer)
        {
            HttpUtils.Response resp = HttpUtils.InvokePUT(url, contentType, null, writer, Session);

            if ((int)resp.StatusCode < 200 || (int)resp.StatusCode > 299)
            {
                throw ConvertStatusCode(resp.StatusCode, resp.Message, resp.ErrorContent, null);
            }

            return resp;
        }

        protected void Delete(UrlBuilder url)
        {
            HttpUtils.Response resp = HttpUtils.InvokeDELETE(url, Session);

            if (resp.StatusCode != HttpStatusCode.NoContent)
            {
                throw ConvertStatusCode(resp.StatusCode, resp.Message, resp.ErrorContent, null);
            }
        }

        protected string GetServiceDocURL()
        {
            return Session.GetValue(SessionParameter.AtomPubUrl) as string;
        }

        protected bool IsNextLink(AtomElement element)
        {
            return AtomPubConstants.RelNext == ((AtomLink)element.Object).Rel;
        }

        protected bool IsStr(string name, AtomElement element)
        {
            return name == element.LocalName && element.Object is string;
        }

        protected bool IsInt(string name, AtomElement element)
        {
            return name == element.LocalName && element.Object is Int64;
        }

        // ---- common methods ----

        protected cmisObjectType CreateIdObject(string objectId)
        {
            cmisObjectType cmisObject = new cmisObjectType();

            cmisPropertiesType properties = new cmisPropertiesType();
            cmisObject.properties = properties;

            cmisPropertyId idProperty = new cmisPropertyId();
            properties.Items = new cmisProperty[] { idProperty };
            idProperty.propertyDefinitionId = PropertyIds.ObjectId;
            idProperty.value = new string[] { objectId };

            return cmisObject;
        }

        protected bool IsAclMergeRequired(IAcl addAces, IAcl removeAces)
        {
            return (addAces != null && addAces.Aces != null && addAces.Aces.Count > 0)
                    || (removeAces != null && removeAces.Aces != null && removeAces.Aces.Count > 0);
        }

        protected IAcl MergeAcls(IAcl originalAces, IAcl addAces, IAcl removeAces)
        {
            IDictionary<string, HashSet<string>> originals = ConvertAclToDict(originalAces);
            IDictionary<string, HashSet<string>> adds = ConvertAclToDict(addAces);
            IDictionary<string, HashSet<string>> removes = ConvertAclToDict(removeAces);
            IList<IAce> newACEs = new List<IAce>();

            // iterate through the original ACEs
            foreach (KeyValuePair<string, HashSet<string>> ace in originals)
            {
                HashSet<string> permSet;
                if (ace.Value == null)
                {
                    permSet = new HashSet<string>();
                }
                else
                {
                    permSet = new HashSet<string>(ace.Value);
                }

                // add permissions
                HashSet<string> addPermissions;
                if (adds.TryGetValue(ace.Key, out addPermissions))
                {
                    foreach (string perm in addPermissions)
                    {
                        permSet.Add(perm);
                    }
                }

                // remove permissions
                HashSet<string> removePermissions;
                if (removes.TryGetValue(ace.Key, out removePermissions))
                {
                    foreach (string perm in removePermissions)
                    {
                        permSet.Remove(perm);
                    }
                }

                // create new ACE
                Ace resultAce = new Ace();
                resultAce.IsDirect = true;
                Principal resultPrincipal = new Principal();
                resultPrincipal.Id = ace.Key;
                resultAce.Principal = resultPrincipal;
                resultAce.Permissions = new List<string>(permSet);

                newACEs.Add(resultAce);
            }

            // find all ACEs that should be added but are not in the original ACE list
            foreach (KeyValuePair<string, HashSet<string>> ace in adds)
            {
                if (!originals.ContainsKey(ace.Key) && ace.Value != null && ace.Value.Count > 0)
                {
                    Ace resultAce = new Ace();
                    resultAce.IsDirect = true;
                    Principal resultPrincipal = new Principal();
                    resultPrincipal.Id = ace.Key;
                    resultAce.Principal = resultPrincipal;
                    resultAce.Permissions = new List<string>(ace.Value);

                    newACEs.Add(resultAce);
                }
            }

            Acl result = new Acl();
            result.Aces = newACEs;

            return result;
        }

        private IDictionary<string, HashSet<string>> ConvertAclToDict(IAcl acl)
        {
            IDictionary<string, HashSet<string>> result = new Dictionary<string, HashSet<string>>();

            if (acl == null || acl.Aces == null)
            {
                return result;
            }

            foreach (Ace ace in acl.Aces)
            {
                // don't consider indirect ACEs - we can't change them
                if (!ace.IsDirect)
                {
                    // ignore
                    continue;
                }

                // although a principal must not be null, check it
                if ((ace.Principal == null) || (ace.Principal.Id == null))
                {
                    // ignore
                    continue;
                }

                if (ace.Permissions == null)
                {
                    continue;
                }

                HashSet<string> permissions;
                if (!result.TryGetValue(ace.Principal.Id, out permissions))
                {
                    permissions = new HashSet<string>();
                    result[ace.Principal.Id] = permissions;
                }

                foreach (string perm in ace.Permissions)
                {
                    permissions.Add(perm);
                }
            }

            return result;
        }

        protected IAcl GetAclInternal(string repositoryId, string objectId, bool? onlyBasicPermissions, IExtensionsData extension)
        {
            // find the link
            String link = LoadLink(repositoryId, objectId, AtomPubConstants.RelACL, AtomPubConstants.MediatypeACL);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelACL, AtomPubConstants.MediatypeACL);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamOnlyBasicPermissions, onlyBasicPermissions);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomAcl acl = Parse<AtomAcl>(resp.Stream);

            return Converter.Convert(acl.ACL, null);
        }

        protected AtomAcl UpdateAcl(string repositoryId, string objectId, IAcl acl, AclPropagation? aclPropagation)
        {
            // find the link
            String link = LoadLink(repositoryId, objectId, AtomPubConstants.RelACL, AtomPubConstants.MediatypeACL);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelACL, AtomPubConstants.MediatypeACL);
            }

            UrlBuilder aclUrl = new UrlBuilder(link);
            aclUrl.AddParameter(AtomPubConstants.ParamACLPropagation, aclPropagation);

            // set up object and writer
            cmisAccessControlListType cmisAcl = Converter.Convert(acl);
            HttpUtils.Output output = delegate(Stream stream)
            {
                AtomWriter.AclSerializer.Serialize(stream, cmisAcl);
            };

            // update
            HttpUtils.Response resp = Put(aclUrl, AtomPubConstants.MediatypeACL, output);

            // parse new acl
            return Parse<AtomAcl>(resp.Stream);
        }

        protected IList<IRepositoryInfo> GetRepositoriesInternal(string repositoryId)
        {
            IList<IRepositoryInfo> repInfos = new List<IRepositoryInfo>();

            // retrieve service doc
            UrlBuilder url = new UrlBuilder(GetServiceDocURL());
            url.AddParameter(AtomPubConstants.ParamRepositoryId, repositoryId);

            // read and parse
            HttpUtils.Response resp = Read(url);
            ServiceDoc serviceDoc = Parse<ServiceDoc>(resp.Stream);

            // walk through the workspaces
            foreach (RepositoryWorkspace ws in serviceDoc.GetWorkspaces())
            {
                if (ws.Id == null)
                {
                    // found a non-CMIS workspace
                    continue;
                }

                foreach (AtomElement element in ws.GetElements())
                {
                    if (element.LocalName == NameCollection)
                    {
                        AddCollection(ws.Id, (IDictionary<string, string>)element.Object);
                    }
                    else if (element.Object is AtomLink)
                    {
                        AddRepositoryLink(ws.Id, (AtomLink)element.Object);
                    }
                    else if (element.LocalName == NameURITemplate)
                    {
                        AddTemplate(ws.Id, (IDictionary<string, string>)element.Object);
                    }
                    else if (element.Object is cmisRepositoryInfoType)
                    {
                        repInfos.Add(Converter.Convert((cmisRepositoryInfoType)element.Object));
                    }
                }
            }

            return repInfos;
        }

        protected IObjectData GetObjectInternal(string repositoryId, IdentifierType idOrPath, string objectIdOrPath,
            ReturnVersion? returnVersion, string filter, bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships,
            string renditionFilter, bool? includePolicyIds, bool? includeAcl, IExtensionsData extension)
        {
            IObjectData result = null;

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters[AtomPubConstants.ParamId] = objectIdOrPath;
            parameters[AtomPubConstants.ParamPath] = objectIdOrPath;
            parameters[AtomPubConstants.ParamReturnVersion] = returnVersion;
            parameters[AtomPubConstants.ParamFilter] = filter;
            parameters[AtomPubConstants.ParamAllowableActions] = includeAllowableActions;
            parameters[AtomPubConstants.ParamACL] = includeAcl;
            parameters[AtomPubConstants.ParamPolicyIds] = includePolicyIds;
            parameters[AtomPubConstants.ParamRelationships] = includeRelationships;
            parameters[AtomPubConstants.ParamRenditionFilter] = renditionFilter;

            string link = LoadTemplateLink(repositoryId, (idOrPath == IdentifierType.Id ? AtomPubConstants.TemplateObjectById
                    : AtomPubConstants.TemplateObjectByPath), parameters);
            if (link == null)
            {
                throw new CmisObjectNotFoundException("Unknown repository!");
            }

            UrlBuilder url = new UrlBuilder(link);
            // workaround for missing template parameter in the CMIS spec
            if ((returnVersion != null) && (returnVersion != ReturnVersion.This))
            {
                url.AddParameter(AtomPubConstants.ParamReturnVersion, returnVersion);
            }

            HttpUtils.Response resp = Read(url);
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            if (entry.Id == null)
            {
                throw new CmisConnectionException("Received Atom entry is not a CMIS entry!");
            }

            LockLinks();
            try
            {
                RemoveLinks(repositoryId, entry.Id);

                foreach (AtomElement element in entry.GetElements())
                {
                    if (element.Object is AtomLink)
                    {
                        AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                    }
                    else if (element.Object is cmisObjectType)
                    {
                        result = Converter.Convert((cmisObjectType)element.Object);
                    }
                }
            }
            finally
            {
                UnlockLinks();
            }

            return result;
        }

        protected ITypeDefinition GetTypeDefinitionInternal(string repositoryId, string typeId)
        {
            ITypeDefinition result = null;

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters[AtomPubConstants.ParamId] = typeId;

            string link = LoadTemplateLink(repositoryId, AtomPubConstants.TemplateTypeById, parameters);
            if (link == null)
            {
                throw new CmisObjectNotFoundException("Unknown repository!");
            }

            // read and parse
            HttpUtils.Response resp = Read(new UrlBuilder(link));
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            // we expect a CMIS entry
            if (entry.Id == null)
            {
                throw new CmisConnectionException("Received Atom entry is not a CMIS entry!");
            }

            LockTypeLinks();
            try
            {
                // clean up cache
                RemoveTypeLinks(repositoryId, entry.Id);

                // walk through the entry
                foreach (AtomElement element in entry.GetElements())
                {
                    if (element.Object is AtomLink)
                    {
                        AddTypeLink(repositoryId, entry.Id, (AtomLink)element.Object);
                    }
                    else if (element.Object is cmisTypeDefinitionType)
                    {
                        result = Converter.Convert((cmisTypeDefinitionType)element.Object);
                    }
                }
            }
            finally
            {
                UnlockTypeLinks();
            }

            return result;
        }
    }

    internal class RepositoryService : AbstractAtomPubService, IRepositoryService
    {
        public RepositoryService(BindingSession session)
        {
            Session = session;
        }

        public IList<IRepositoryInfo> GetRepositoryInfos(IExtensionsData extension)
        {
            return GetRepositoriesInternal(null);
        }

        public IRepositoryInfo GetRepositoryInfo(string repositoryId, IExtensionsData extension)
        {
            IList<IRepositoryInfo> repositoryInfos = GetRepositoriesInternal(repositoryId);

            // find the repository
            foreach (IRepositoryInfo info in repositoryInfos)
            {
                if (info.Id == null) { continue; }
                if (info.Id == repositoryId) { return info; }
            }

            throw new CmisObjectNotFoundException("Repository not found!");
        }

        public ITypeDefinitionList GetTypeChildren(string repositoryId, string typeId, bool? includePropertyDefinitions,
            long? maxItems, long? skipCount, IExtensionsData extension)
        {
            TypeDefinitionList result = new TypeDefinitionList();

            // find the link
            string link = null;
            if (typeId == null)
            {
                link = LoadCollection(repositoryId, AtomPubConstants.CollectionTypes);
            }
            else
            {
                link = LoadTypeLink(repositoryId, typeId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);
            }

            if (link == null)
            {
                throw new CmisObjectNotFoundException("Unknown repository or type!");
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamPropertyDefinitions, includePropertyDefinitions);
            url.AddParameter(AtomPubConstants.ParamMaxItems, maxItems);
            url.AddParameter(AtomPubConstants.ParamSkipCount, skipCount);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // handle top level
            foreach (AtomElement element in feed.GetElements())
            {
                if (element.Object is AtomLink)
                {
                    if (IsNextLink(element)) { result.HasMoreItems = true; }
                }
                else if (IsInt(NameNumItems, element))
                {
                    result.NumItems = (long)element.Object;
                }
            }

            result.List = new List<ITypeDefinition>(feed.GetEntries().Count);

            // get the children
            foreach (AtomEntry entry in feed.GetEntries())
            {
                ITypeDefinition child = null;

                LockTypeLinks();
                try
                {
                    foreach (AtomElement element in entry.GetElements())
                    {
                        if (element.Object is AtomLink)
                        {
                            AddTypeLink(repositoryId, entry.Id, (AtomLink)element.Object);
                        }
                        else if (element.Object is cmisTypeDefinitionType)
                        {
                            child = Converter.Convert((cmisTypeDefinitionType)element.Object);
                        }
                    }
                }
                finally
                {
                    UnlockTypeLinks();
                }

                if (child != null)
                {
                    result.List.Add(child);
                }
            }

            return result;
        }

        public IList<ITypeDefinitionContainer> GetTypeDescendants(string repositoryId, string typeId, long? depth,
            bool? includePropertyDefinitions, IExtensionsData extension)
        {
            List<ITypeDefinitionContainer> result = new List<ITypeDefinitionContainer>();

            // find the link
            string link = null;
            if (typeId == null)
            {
                link = LoadRepositoryLink(repositoryId, AtomPubConstants.RepRelTypeDesc);
            }
            else
            {
                link = LoadTypeLink(repositoryId, typeId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeDescendants);
            }

            if (link == null)
            {
                throw new CmisObjectNotFoundException("Unknown repository or type!");
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamDepth, depth);
            url.AddParameter(AtomPubConstants.ParamPropertyDefinitions, includePropertyDefinitions);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // process tree
            AddTypeDescendantsLevel(repositoryId, feed, result);

            return result;
        }

        private void AddTypeDescendantsLevel(string repositoryId, AtomFeed feed, List<ITypeDefinitionContainer> containerList)
        {
            if (feed == null || feed.GetEntries().Count == 0)
            {
                return;
            }

            foreach (AtomEntry entry in feed.GetEntries())
            {
                TypeDefinitionContainer childContainer = null;
                List<ITypeDefinitionContainer> childContainerList = new List<ITypeDefinitionContainer>();

                // walk through the entry
                LockTypeLinks();
                try
                {
                    foreach (AtomElement element in entry.GetElements())
                    {
                        if (element.Object is AtomLink)
                        {
                            AddTypeLink(repositoryId, entry.Id, (AtomLink)element.Object);
                        }
                        else if (element.Object is cmisTypeDefinitionType)
                        {
                            childContainer = new TypeDefinitionContainer();
                            childContainer.TypeDefinition = Converter.Convert((cmisTypeDefinitionType)element.Object);
                        }
                        else if (element.Object is AtomFeed)
                        {
                            AddTypeDescendantsLevel(repositoryId, (AtomFeed)element.Object, childContainerList);
                        }
                    }
                }
                finally
                {
                    UnlockTypeLinks();
                }

                if (childContainer != null)
                {
                    childContainer.Children = childContainerList;
                    containerList.Add(childContainer);
                }
            }
        }

        public ITypeDefinition GetTypeDefinition(string repositoryId, string typeId, IExtensionsData extension)
        {
            return GetTypeDefinitionInternal(repositoryId, typeId);
        }
    }

    internal class NavigationService : AbstractAtomPubService, INavigationService
    {
        public NavigationService(BindingSession session)
        {
            Session = session;
        }

        public IObjectInFolderList GetChildren(string repositoryId, string folderId, string filter, string orderBy,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includePathSegment, long? maxItems, long? skipCount, IExtensionsData extension)
        {
            ObjectInFolderList result = new ObjectInFolderList();

            // find the link
            String link = LoadLink(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);

            if (link == null)
            {
                ThrowLinkException(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);
            url.AddParameter(AtomPubConstants.ParamOrderBy, orderBy);
            url.AddParameter(AtomPubConstants.ParamAllowableActions, includeAllowableActions);
            url.AddParameter(AtomPubConstants.ParamRelationships, includeRelationships);
            url.AddParameter(AtomPubConstants.ParamRenditionFilter, renditionFilter);
            url.AddParameter(AtomPubConstants.ParamPathSegment, includePathSegment);
            url.AddParameter(AtomPubConstants.ParamMaxItems, maxItems);
            url.AddParameter(AtomPubConstants.ParamSkipCount, skipCount);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // handle top level
            foreach (AtomElement element in feed.GetElements())
            {
                if (element.Object is AtomLink)
                {
                    if (IsNextLink(element)) { result.HasMoreItems = true; }
                }
                else if (IsInt(NameNumItems, element))
                {
                    result.NumItems = (long)element.Object;
                }
            }

            // get the children
            if (feed.GetEntries().Count > 0)
            {
                result.Objects = new List<IObjectInFolderData>(feed.GetEntries().Count);

                foreach (AtomEntry entry in feed.GetEntries())
                {
                    ObjectInFolderData child = null;
                    String pathSegment = null;

                    LockLinks();
                    try
                    {
                        // clean up cache
                        RemoveLinks(repositoryId, entry.Id);

                        // walk through the entry
                        foreach (AtomElement element in entry.GetElements())
                        {
                            if (element.Object is AtomLink)
                            {
                                AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                            }
                            else if (IsStr(NamePathSegment, element))
                            {
                                pathSegment = (string)element.Object;
                            }
                            else if (element.Object is cmisObjectType)
                            {
                                child = new ObjectInFolderData();
                                child.Object = Converter.Convert((cmisObjectType)element.Object);
                            }
                        }
                    }
                    finally
                    {
                        UnlockLinks();
                    }

                    if (child != null)
                    {
                        child.PathSegment = pathSegment;
                        result.Objects.Add(child);
                    }
                }
            }

            return result;
        }

        public IList<IObjectInFolderContainer> GetDescendants(string repositoryId, string folderId, long? depth, string filter,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includePathSegment, IExtensionsData extension)
        {
            IList<IObjectInFolderContainer> result = new List<IObjectInFolderContainer>();

            // find the link
            String link = LoadLink(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeDescendants);

            if (link == null)
            {
                ThrowLinkException(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeDescendants);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamDepth, depth);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);
            url.AddParameter(AtomPubConstants.ParamAllowableActions, includeAllowableActions);
            url.AddParameter(AtomPubConstants.ParamRelationships, includeRelationships);
            url.AddParameter(AtomPubConstants.ParamRenditionFilter, renditionFilter);
            url.AddParameter(AtomPubConstants.ParamPathSegment, includePathSegment);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // process tree
            AddDescendantsLevel(repositoryId, feed, result);

            return result;
        }

        public IList<IObjectInFolderContainer> GetFolderTree(string repositoryId, string folderId, long? depth, string filter,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includePathSegment, IExtensionsData extension)
        {
            IList<IObjectInFolderContainer> result = new List<IObjectInFolderContainer>();

            // find the link
            string link = LoadLink(repositoryId, folderId, AtomPubConstants.RelFolderTree, AtomPubConstants.MediatypeDescendants);

            if (link == null)
            {
                ThrowLinkException(repositoryId, folderId, AtomPubConstants.RelFolderTree, AtomPubConstants.MediatypeDescendants);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamDepth, depth);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);
            url.AddParameter(AtomPubConstants.ParamAllowableActions, includeAllowableActions);
            url.AddParameter(AtomPubConstants.ParamRelationships, includeRelationships);
            url.AddParameter(AtomPubConstants.ParamRenditionFilter, renditionFilter);
            url.AddParameter(AtomPubConstants.ParamPathSegment, includePathSegment);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // process tree
            AddDescendantsLevel(repositoryId, feed, result);

            return result;
        }

        public IList<IObjectParentData> GetObjectParents(string repositoryId, string objectId, string filter,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            bool? includeRelativePathSegment, IExtensionsData extension)
        {
            IList<IObjectParentData> result = new List<IObjectParentData>();

            // find the link
            String link = LoadLink(repositoryId, objectId, AtomPubConstants.RelUp, AtomPubConstants.MediatypeFeed);

            if (link == null)
            {
                // root and unfiled objects have no UP link
                return result;
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);
            url.AddParameter(AtomPubConstants.ParamAllowableActions, includeAllowableActions);
            url.AddParameter(AtomPubConstants.ParamRelationships, includeRelationships);
            url.AddParameter(AtomPubConstants.ParamRenditionFilter, renditionFilter);
            url.AddParameter(AtomPubConstants.ParamRelativePathSegment, includeRelativePathSegment);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomBase atomBase = Parse<AtomBase>(resp.Stream);

            if (atomBase is AtomFeed)
            {
                // it's a feed
                AtomFeed feed = (AtomFeed)atomBase;

                // walk through the feed
                foreach (AtomEntry entry in feed.GetEntries())
                {
                    IObjectParentData objectParent = ProcessParentEntry(entry, repositoryId);

                    if (objectParent != null)
                    {
                        result.Add(objectParent);
                    }
                }
            }
            else if (atomBase is AtomEntry)
            {
                // it's an entry
                AtomEntry entry = (AtomEntry)atomBase;

                IObjectParentData objectParent = ProcessParentEntry(entry, repositoryId);

                if (objectParent != null)
                {
                    result.Add(objectParent);
                }
            }

            return result;
        }

        private IObjectParentData ProcessParentEntry(AtomEntry entry, string repositoryId)
        {
            ObjectParentData result = null;
            String relativePathSegment = null;

            LockLinks();
            try
            {
                // clean up cache
                RemoveLinks(repositoryId, entry.Id);

                // walk through the entry
                foreach (AtomElement element in entry.GetElements())
                {
                    if (element.Object is AtomLink)
                    {
                        AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                    }
                    else if (element.Object is cmisObjectType)
                    {
                        result = new ObjectParentData();
                        result.Object = Converter.Convert((cmisObjectType)element.Object);
                    }
                    else if (IsStr(NameRelativePathSegment, element))
                    {
                        relativePathSegment = (string)element.Object;
                    }
                }
            }
            finally
            {
                UnlockLinks();
            }

            if (result != null)
            {
                result.RelativePathSegment = relativePathSegment;
            }

            return result;
        }

        public IObjectData GetFolderParent(string repositoryId, string folderId, string filter, ExtensionsData extension)
        {
            IObjectData result = null;

            // find the link
            String link = LoadLink(repositoryId, folderId, AtomPubConstants.RelUp, AtomPubConstants.MediatypeEntry);

            if (link == null)
            {
                ThrowLinkException(repositoryId, folderId, AtomPubConstants.RelUp, AtomPubConstants.MediatypeEntry);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);

            // read
            HttpUtils.Response resp = Read(url);
            AtomBase atomBase = Parse<AtomBase>(resp.Stream);

            // get the entry
            AtomEntry entry = null;
            if (atomBase is AtomFeed)
            {
                AtomFeed feed = (AtomFeed)atomBase;
                if (feed.GetEntries().Count == 0)
                {
                    throw new CmisRuntimeException("Parent feed is empty!");
                }
                entry = feed.GetEntries()[0];
            }
            else if (atomBase is AtomEntry)
            {
                entry = (AtomEntry)atomBase;
            }
            else
            {
                throw new CmisRuntimeException("Unexpected document!");
            }

            LockLinks();
            try
            {
                // clean up cache
                RemoveLinks(repositoryId, entry.Id);

                // walk through the entry
                foreach (AtomElement element in entry.GetElements())
                {
                    if (element.Object is AtomLink)
                    {
                        AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                    }
                    else if (element.Object is cmisObjectType)
                    {
                        result = Converter.Convert((cmisObjectType)element.Object);
                    }
                }
            }
            finally
            {
                UnlockLinks();
            }

            return result;
        }

        public IObjectList GetCheckedOutDocs(string repositoryId, string folderId, string filter, string orderBy,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            long? maxItems, long? skipCount, IExtensionsData extension)
        {
            ObjectList result = new ObjectList();

            // find the link
            String link = LoadCollection(repositoryId, AtomPubConstants.CollectionCheckedout);

            if (link == null)
            {
                throw new CmisObjectNotFoundException("Unknown repository or checkedout collection not supported!");
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamFolderId, folderId);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);
            url.AddParameter(AtomPubConstants.ParamOrderBy, orderBy);
            url.AddParameter(AtomPubConstants.ParamAllowableActions, includeAllowableActions);
            url.AddParameter(AtomPubConstants.ParamRelationships, includeRelationships);
            url.AddParameter(AtomPubConstants.ParamRenditionFilter, renditionFilter);
            url.AddParameter(AtomPubConstants.ParamMaxItems, maxItems);
            url.AddParameter(AtomPubConstants.ParamSkipCount, skipCount);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // handle top level
            foreach (AtomElement element in feed.GetElements())
            {
                if (element.Object is AtomLink)
                {
                    if (IsNextLink(element))
                    {
                        result.HasMoreItems = true;
                    }
                }
                else if (IsInt(NameNumItems, element))
                {
                    result.NumItems = (long)element.Object;
                }
            }

            // get the documents
            if (feed.GetEntries().Count > 0)
            {
                result.Objects = new List<IObjectData>(feed.GetEntries().Count);

                foreach (AtomEntry entry in feed.GetEntries())
                {
                    IObjectData child = null;

                    LockLinks();
                    try
                    {
                        // clean up cache
                        RemoveLinks(repositoryId, entry.Id);

                        // walk through the entry
                        foreach (AtomElement element in entry.GetElements())
                        {
                            if (element.Object is AtomLink)
                            {
                                AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                            }
                            else if (element.Object is cmisObjectType)
                            {
                                child = Converter.Convert((cmisObjectType)element.Object);
                            }
                        }
                    }
                    finally
                    {
                        UnlockLinks();
                    }

                    if (child != null)
                    {
                        result.Objects.Add(child);
                    }
                }
            }

            return result;
        }

        private void AddDescendantsLevel(String repositoryId, AtomFeed feed, IList<IObjectInFolderContainer> containerList)
        {
            if ((feed == null) || (feed.GetEntries().Count == 0))
            {
                return;
            }

            // walk through the feed
            foreach (AtomEntry entry in feed.GetEntries())
            {
                ObjectInFolderData objectInFolder = null;
                string pathSegment = null;
                IList<IObjectInFolderContainer> childContainerList = new List<IObjectInFolderContainer>();

                LockLinks();
                try
                {
                    // clean up cache
                    RemoveLinks(repositoryId, entry.Id);

                    // walk through the entry
                    foreach (AtomElement element in entry.GetElements())
                    {
                        if (element.Object is AtomLink)
                        {
                            AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                        }
                        else if (element.Object is cmisObjectType)
                        {
                            objectInFolder = new ObjectInFolderData();
                            objectInFolder.Object = Converter.Convert((cmisObjectType)element.Object);
                        }
                        else if (IsStr(NamePathSegment, element))
                        {
                            pathSegment = (string)element.Object;
                        }
                        else if (element.Object is AtomFeed)
                        {
                            AddDescendantsLevel(repositoryId, (AtomFeed)element.Object, childContainerList);
                        }
                    }
                }
                finally
                {
                    UnlockLinks();
                }

                if (objectInFolder != null)
                {
                    objectInFolder.PathSegment = pathSegment;
                    ObjectInFolderContainer childContainer = new ObjectInFolderContainer();
                    childContainer.Object = objectInFolder;
                    childContainer.Children = childContainerList;
                    containerList.Add(childContainer);
                }
            }
        }
    }

    internal class ObjectService : AbstractAtomPubService, IObjectService
    {
        public ObjectService(BindingSession session)
        {
            Session = session;
        }

        public string CreateDocument(string repositoryId, IProperties properties, string folderId, IContentStream contentStream,
            VersioningState? versioningState, IList<string> policies, IAcl addAces, IAcl removeAces, IExtensionsData extension)
        {
            CheckCreateProperties(properties);

            // find the link
            string link = null;

            if (folderId == null)
            {
                link = LoadCollection(repositoryId, AtomPubConstants.CollectionUnfiled);

                if (link == null)
                {
                    throw new CmisObjectNotFoundException("Unknown respository or unfiling not supported!");
                }
            }
            else
            {
                link = LoadLink(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);

                if (link == null)
                {
                    ThrowLinkException(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);
                }
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamVersioningState, versioningState);

            // set up object and writer
            cmisObjectType cmisObject = new cmisObjectType();
            cmisObject.properties = Converter.Convert(properties);
            cmisObject.policyIds = Converter.ConvertPolicies(policies);

            string mediaType = null;
            Stream stream = null;
            string filename = null;

            if (contentStream != null)
            {
                mediaType = contentStream.MimeType;
                stream = contentStream.Stream;
                filename = contentStream.FileName;
            }

            AtomEntryWriter entryWriter = new AtomEntryWriter(cmisObject, mediaType, filename, stream);

            // post the new folder object
            HttpUtils.Response resp = Post(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));

            // parse the response
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            // handle ACL modifications
            HandleAclModifications(repositoryId, entry, addAces, removeAces);

            return entry.Id;
        }

        public string CreateDocumentFromSource(string repositoryId, string sourceId, IProperties properties, string folderId,
            VersioningState? versioningState, IList<string> policies, IAcl addAces, IAcl removeAces, IExtensionsData extension)
        {
            throw new CmisNotSupportedException("createDocumentFromSource is not supported by the AtomPub binding!");
        }

        public string CreateFolder(string repositoryId, IProperties properties, string folderId, IList<string> policies,
            IAcl addAces, IAcl removeAces, IExtensionsData extension)
        {
            CheckCreateProperties(properties);

            // find the link
            string link = LoadLink(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);

            if (link == null)
            {
                ThrowLinkException(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);
            }

            UrlBuilder url = new UrlBuilder(link);


            // set up object and writer
            cmisObjectType cmisObject = new cmisObjectType();
            cmisObject.properties = Converter.Convert(properties);
            cmisObject.policyIds = Converter.ConvertPolicies(policies);

            AtomEntryWriter entryWriter = new AtomEntryWriter(cmisObject);

            // post the new folder object
            HttpUtils.Response resp = Post(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));

            // parse the response
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            // handle ACL modifications
            HandleAclModifications(repositoryId, entry, addAces, removeAces);

            return entry.Id;
        }

        public string CreateRelationship(string repositoryId, IProperties properties, IList<string> policies, IAcl addAces,
            IAcl removeAces, IExtensionsData extension)
        {
            CheckCreateProperties(properties);

            // find source id
            IPropertyData sourceIdProperty = properties[PropertyIds.SourceId];
            if (sourceIdProperty == null || sourceIdProperty.PropertyType != PropertyType.Id)
            {
                throw new CmisInvalidArgumentException("Source Id is not set!");
            }

            string sourceId = sourceIdProperty.FirstValue as string;
            if (sourceId == null)
            {
                throw new CmisInvalidArgumentException("Source Id is not set!");
            }

            // find the link
            string link = LoadLink(repositoryId, sourceId, AtomPubConstants.RelRelationships, AtomPubConstants.MediatypeFeed);

            if (link == null)
            {
                ThrowLinkException(repositoryId, sourceId, AtomPubConstants.RelRelationships, AtomPubConstants.MediatypeFeed);
            }

            UrlBuilder url = new UrlBuilder(link);

            // set up object and writer
            cmisObjectType cmisObject = new cmisObjectType();
            cmisObject.properties = Converter.Convert(properties);
            cmisObject.policyIds = Converter.ConvertPolicies(policies);

            AtomEntryWriter entryWriter = new AtomEntryWriter(cmisObject);

            // post the new folder object
            HttpUtils.Response resp = Post(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));

            // parse the response
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            // handle ACL modifications
            HandleAclModifications(repositoryId, entry, addAces, removeAces);

            return entry.Id;
        }

        public string CreatePolicy(string repositoryId, IProperties properties, string folderId, IList<string> policies,
            IAcl addAces, IAcl removeAces, IExtensionsData extension)
        {
            CheckCreateProperties(properties);

            // find the link
            string link = null;

            if (folderId == null)
            {
                link = LoadCollection(repositoryId, AtomPubConstants.CollectionUnfiled);

                if (link == null)
                {
                    throw new CmisObjectNotFoundException("Unknown respository or unfiling not supported!");
                }
            }
            else
            {
                link = LoadLink(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);

                if (link == null)
                {
                    ThrowLinkException(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);
                }
            }

            UrlBuilder url = new UrlBuilder(link);


            // set up object and writer
            cmisObjectType cmisObject = new cmisObjectType();
            cmisObject.properties = Converter.Convert(properties);
            cmisObject.policyIds = Converter.ConvertPolicies(policies);

            AtomEntryWriter entryWriter = new AtomEntryWriter(cmisObject);

            // post the new folder object
            HttpUtils.Response resp = Post(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));

            // parse the response
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            // handle ACL modifications
            HandleAclModifications(repositoryId, entry, addAces, removeAces);

            return entry.Id;
        }

        public IAllowableActions GetAllowableActions(string repositoryId, string objectId, IExtensionsData extension)
        {
            // find the link
            string link = LoadLink(repositoryId, objectId, AtomPubConstants.RelAllowableActions, AtomPubConstants.MediatypeAllowableAction);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelAllowableActions, AtomPubConstants.MediatypeAllowableAction);
            }

            UrlBuilder url = new UrlBuilder(link);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomAllowableActions allowableActions = Parse<AtomAllowableActions>(resp.Stream);

            return Converter.Convert(allowableActions.AllowableActions);
        }

        public IProperties GetProperties(string repositoryId, string objectId, string filter, IExtensionsData extension)
        {
            IObjectData obj = GetObjectInternal(repositoryId, IdentifierType.Id, objectId, ReturnVersion.This, filter,
                    false, IncludeRelationshipsFlag.None, "cmis:none", false, false, extension);

            return obj.Properties;
        }

        public IList<IRenditionData> GetRenditions(string repositoryId, string objectId, string renditionFilter,
            long? maxItems, long? skipCount, IExtensionsData extension)
        {
            IObjectData obj = GetObjectInternal(repositoryId, IdentifierType.Id, objectId, ReturnVersion.This,
                PropertyIds.ObjectId, false, IncludeRelationshipsFlag.None, renditionFilter, false, false, extension);

            IList<IRenditionData> result = obj.Renditions;
            if (result == null)
            {
                result = new List<IRenditionData>();
            }

            return result;
        }

        public IObjectData GetObject(string repositoryId, string objectId, string filter, bool? includeAllowableActions,
            IncludeRelationshipsFlag? includeRelationships, string renditionFilter, bool? includePolicyIds,
            bool? includeAcl, IExtensionsData extension)
        {
            return GetObjectInternal(repositoryId, IdentifierType.Id, objectId, ReturnVersion.This, filter, includeAllowableActions,
                includeRelationships, renditionFilter, includePolicyIds, includeAcl, extension);
        }

        public IObjectData GetObjectByPath(string repositoryId, string path, string filter, bool? includeAllowableActions,
            IncludeRelationshipsFlag? includeRelationships, string renditionFilter, bool? includePolicyIds, bool? includeAcl,
            IExtensionsData extension)
        {
            return GetObjectInternal(repositoryId, IdentifierType.Path, path, ReturnVersion.This, filter, includeAllowableActions,
                includeRelationships, renditionFilter, includePolicyIds, includeAcl, extension);
        }

        public IContentStream GetContentStream(string repositoryId, string objectId, string streamId, long? offset, long? length,
            IExtensionsData extension)
        {
            ContentStream result = new ContentStream();

            // find the link
            string link = LoadLink(repositoryId, objectId, AtomPubConstants.LinkRelContent, null);

            if (link == null)
            {
                throw new CmisConstraintException("No content stream");
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamStreamId, streamId);

            // get the content
            if (offset != null && offset > Int32.MaxValue)
            {
                throw new CmisInvalidArgumentException("Offset >" + Int32.MaxValue.ToString());
            }
            if (length != null && length > Int32.MaxValue)
            {
                throw new CmisInvalidArgumentException("Length >" + Int32.MaxValue.ToString());
            }
            HttpUtils.Response resp = HttpUtils.InvokeGET(url, Session, (int?)offset, (int?)length);

            // check response code
            if (resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.PartialContent)
            {
                throw ConvertStatusCode(resp.StatusCode, resp.Message, resp.ErrorContent, null);
            }

            result.FileName = null;
            result.Length = resp.ContentLength;
            result.MimeType = resp.ContentType;
            result.Stream = resp.Stream;

            return result;
        }

        public void UpdateProperties(string repositoryId, ref string objectId, ref string changeToken, IProperties properties,
            IExtensionsData extension)
        {
            // we need an object id
            if (objectId == null || objectId.Length == 0)
            {
                throw new CmisInvalidArgumentException("Object id must be set!");
            }

            // find the link
            string link = LoadLink(repositoryId, objectId, AtomPubConstants.RelSelf, AtomPubConstants.MediatypeEntry);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelSelf, AtomPubConstants.MediatypeEntry);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamChangeToken, changeToken);

            // set up object and writer
            cmisObjectType cmisObject = new cmisObjectType();
            cmisObject.properties = Converter.Convert(properties);

            AtomEntryWriter entryWriter = new AtomEntryWriter(cmisObject);

            // update
            HttpUtils.Response resp = Put(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));

            // parse new entry
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            // we expect a CMIS entry
            if (entry.Id == null)
            {
                throw new CmisConnectionException("Received Atom entry is not a CMIS entry!");
            }

            // set object id
            objectId = entry.Id;
            changeToken = null;

            LockLinks();
            try
            {
                // clean up cache
                RemoveLinks(repositoryId, entry.Id);

                // walk through the entry
                foreach (AtomElement element in entry.GetElements())
                {
                    if (element.Object is AtomLink)
                    {
                        AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                    }
                    else if (element.Object is cmisObjectType)
                    {
                        // extract new change token
                        cmisObject = (cmisObjectType)element.Object;
                        if (cmisObject.properties != null)
                        {
                            foreach (cmisProperty property in cmisObject.properties.Items)
                            {
                                if (PropertyIds.ChangeToken == property.propertyDefinitionId && property is cmisPropertyString)
                                {
                                    cmisPropertyString changeTokenProperty = (cmisPropertyString)property;
                                    if (changeTokenProperty.value != null && changeTokenProperty.value.Length > 0)
                                    {
                                        changeToken = changeTokenProperty.value[0];
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                UnlockLinks();
            }
        }

        public void MoveObject(string repositoryId, ref string objectId, string targetFolderId, string sourceFolderId,
            IExtensionsData extension)
        {
            if (objectId == null || objectId.Length == 0)
            {
                throw new CmisInvalidArgumentException("Object id must be set!");
            }

            if (targetFolderId == null || targetFolderId.Length == 0 || sourceFolderId == null || sourceFolderId.Length == 0)
            {
                throw new CmisInvalidArgumentException("Source and target folder must be set!");
            }

            // find the link
            String link = LoadLink(repositoryId, targetFolderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);

            if (link == null)
            {
                ThrowLinkException(repositoryId, targetFolderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamSourceFolderId, sourceFolderId);

            // set up object and writer
            AtomEntryWriter entryWriter = new AtomEntryWriter(CreateIdObject(objectId));

            // post move request
            HttpUtils.Response resp = Post(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));

            // parse the response
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            objectId = entry.Id;
        }

        public void DeleteObject(string repositoryId, string objectId, bool? allVersions, IExtensionsData extension)
        {
            // find the link
            string link = LoadLink(repositoryId, objectId, AtomPubConstants.RelSelf, AtomPubConstants.MediatypeEntry);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelSelf, AtomPubConstants.MediatypeEntry);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamAllVersions, allVersions);

            Delete(url);
        }

        public IFailedToDeleteData DeleteTree(string repositoryId, string folderId, bool? allVersions, UnfileObject? unfileObjects,
            bool? continueOnFailure, ExtensionsData extension)
        {
            // find the down link
            string link = LoadLink(repositoryId, folderId, AtomPubConstants.RelDown, null);
            string childrenLink = null;

            if (link != null)
            {
                // found only a children link, but no descendants link
                // -> try folder tree link
                childrenLink = link;
                link = null;
            }
            else
            {
                // found no or two down links
                // -> get only the descendants link
                link = LoadLink(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeDescendants);
            }

            if (link == null)
            {
                link = LoadLink(repositoryId, folderId, AtomPubConstants.RelFolderTree, AtomPubConstants.MediatypeDescendants);
            }

            if (link == null)
            {
                link = LoadLink(repositoryId, folderId, AtomPubConstants.RelFolderTree, AtomPubConstants.MediatypeFeed);
            }

            if (link == null)
            {
                link = childrenLink;
            }

            if (link == null)
            {
                ThrowLinkException(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeDescendants);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamAllVersions, allVersions);
            url.AddParameter(AtomPubConstants.ParamUnfildeObjects, unfileObjects);
            url.AddParameter(AtomPubConstants.ParamContinueOnFailure, continueOnFailure);

            // make the call
            HttpUtils.Response resp = HttpUtils.InvokeDELETE(url, Session);
            resp.CloseStream();

            // check response code
            if (resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.Accepted || resp.StatusCode == HttpStatusCode.NoContent)
            {
                return new FailedToDeleteData();
            }

            // If the server returned an internal server error, get the remaining
            // children of the folder. We only retrieve the first level, since
            // getDescendants() is not supported by all repositories.
            if (resp.StatusCode == HttpStatusCode.InternalServerError)
            {
                link = LoadLink(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);

                if (link != null)
                {
                    url = new UrlBuilder(link);
                    // we only want the object ids
                    url.AddParameter(AtomPubConstants.ParamFilter, "cmis:objectId");
                    url.AddParameter(AtomPubConstants.ParamAllowableActions, false);
                    url.AddParameter(AtomPubConstants.ParamRelationships, IncludeRelationshipsFlag.None);
                    url.AddParameter(AtomPubConstants.ParamRenditionFilter, "cmis:none");
                    url.AddParameter(AtomPubConstants.ParamPathSegment, false);
                    // 1000 children should be enough to indicate a problem
                    url.AddParameter(AtomPubConstants.ParamMaxItems, 1000);
                    url.AddParameter(AtomPubConstants.ParamSkipCount, 0);

                    // read and parse
                    resp = Read(url);
                    AtomFeed feed = Parse<AtomFeed>(resp.Stream);

                    // prepare result
                    FailedToDeleteData result = new FailedToDeleteData();
                    List<string> ids = new List<string>();
                    result.Ids = ids;

                    // get the children ids
                    foreach (AtomEntry entry in feed.GetEntries())
                    {
                        ids.Add(entry.Id);
                    }

                    return result;
                }
            }

            throw ConvertStatusCode(resp.StatusCode, resp.Message, resp.ErrorContent, null);
        }

        public void SetContentStream(string repositoryId, ref string objectId, bool? overwriteFlag, ref string changeToken,
            IContentStream contentStream, IExtensionsData extension)
        {
            if (objectId == null)
            {
                throw new CmisInvalidArgumentException("Object ID must be set!");
            }

            // we need content
            if (contentStream == null || contentStream.Stream == null || contentStream.MimeType == null)
            {
                throw new CmisInvalidArgumentException("Content must be set!");
            }

            // find the link
            String link = LoadLink(repositoryId, objectId, AtomPubConstants.RelEditMedia, null);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelEditMedia, null);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamChangeToken, changeToken);
            url.AddParameter(AtomPubConstants.ParamOverwriteFlag, overwriteFlag);

            HttpUtils.Output output = delegate(Stream stream)
            {
                int b;
                byte[] buffer = new byte[4096];
                while ((b = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    stream.Write(buffer, 0, b);
                }

                contentStream.Stream.Close();
            };

            IDictionary<string, string> headers = null;
            if (contentStream.FileName != null)
            {
                headers = new Dictionary<string, string>();
                headers.Add(MimeHelper.ContentDisposition,
                    MimeHelper.EncodeContentDisposition(MimeHelper.DispositionAttachment, contentStream.FileName));
            }

            // send content
            HttpUtils.Response resp = HttpUtils.InvokePUT(url, contentStream.MimeType, headers, output, Session);
            resp.CloseStream();

            // check response code
            if (resp.StatusCode != HttpStatusCode.OK && resp.StatusCode != HttpStatusCode.Created && resp.StatusCode != HttpStatusCode.NoContent)
            {
                throw ConvertStatusCode(resp.StatusCode, resp.Message, resp.ErrorContent, null);
            }

            objectId = null;
            changeToken = null;
        }

        public void DeleteContentStream(string repositoryId, ref string objectId, ref string changeToken, IExtensionsData extension)
        {
            if (objectId == null)
            {
                throw new CmisInvalidArgumentException("Object ID must be set!");
            }

            // find the link
            String link = LoadLink(repositoryId, objectId, AtomPubConstants.RelEditMedia, null);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelEditMedia, null);
            }

            UrlBuilder url = new UrlBuilder(link);
            if (changeToken != null)
            {
                url.AddParameter(AtomPubConstants.ParamChangeToken, changeToken);
            }

            Delete(url);

            objectId = null;
            changeToken = null;
        }

        // ---- internal ---

        private void CheckCreateProperties(IProperties properties)
        {
            if (properties == null || properties.PropertyList == null)
            {
                throw new CmisInvalidArgumentException("Properties must be set!");
            }

            if (properties[PropertyIds.ObjectTypeId] == null)
            {
                throw new CmisInvalidArgumentException("Property " + PropertyIds.ObjectTypeId + " must be set!");
            }

            if (properties[PropertyIds.ObjectId] != null)
            {
                throw new CmisInvalidArgumentException("Property " + PropertyIds.ObjectId + " must not be set!");
            }
        }

        private void HandleAclModifications(String repositoryId, AtomEntry entry, IAcl addAces, IAcl removeAces)
        {
            if (!IsAclMergeRequired(addAces, removeAces))
            {
                return;
            }

            IAcl originalAces = GetAclInternal(repositoryId, entry.Id, false, null);

            if (originalAces != null)
            {
                // merge and update ACL
                IAcl newACL = MergeAcls(originalAces, addAces, removeAces);
                if (newACL != null)
                {
                    UpdateAcl(repositoryId, entry.Id, newACL, null);
                }
            }
        }
    }

    internal class VersioningService : AbstractAtomPubService, IVersioningService
    {
        public VersioningService(BindingSession session)
        {
            Session = session;
        }

        public void CheckOut(string repositoryId, ref string objectId, IExtensionsData extension, out bool? contentCopied)
        {
            if (objectId == null || objectId.Length == 0)
            {
                throw new CmisInvalidArgumentException("Object id must be set!");
            }

            // find the link
            String link = LoadCollection(repositoryId, AtomPubConstants.CollectionCheckedout);

            if (link == null)
            {
                throw new CmisObjectNotFoundException("Unknown repository or checkedout collection not supported!");
            }

            UrlBuilder url = new UrlBuilder(link);

            // set up object and writer
            AtomEntryWriter entryWriter = new AtomEntryWriter(CreateIdObject(objectId));

            // post move request
            HttpUtils.Response resp = Post(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));

            // parse the response
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            objectId = entry.Id;

            LockLinks();
            try
            {
                // clean up cache
                RemoveLinks(repositoryId, entry.Id);

                // walk through the entry
                foreach (AtomElement element in entry.GetElements())
                {
                    if (element.Object is AtomLink)
                    {
                        AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                    }
                }
            }
            finally
            {
                UnlockLinks();
            }

            contentCopied = null;
        }

        public void CancelCheckOut(string repositoryId, string objectId, IExtensionsData extension)
        {
            // find the link
            String link = LoadLink(repositoryId, objectId, AtomPubConstants.RelSelf, AtomPubConstants.MediatypeEntry);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelSelf, AtomPubConstants.MediatypeEntry);
            }

            // prefer working copy link if available
            // (workaround for non-compliant repositories)
            string wcLink = GetLink(repositoryId, objectId, AtomPubConstants.RelWorkingCopy, AtomPubConstants.MediatypeEntry);
            if (wcLink != null)
            {
                link = wcLink;
            }

            Delete(new UrlBuilder(link));
        }

        public void CheckIn(string repositoryId, ref string objectId, bool? major, IProperties properties,
            IContentStream contentStream, string checkinComment, IList<string> policies, IAcl addAces, IAcl removeAces,
            IExtensionsData extension)
        {
            // we need an object id
            if (objectId == null || objectId.Length == 0)
            {
                throw new CmisInvalidArgumentException("Object id must be set!");
            }

            // find the link
            string link = LoadLink(repositoryId, objectId, AtomPubConstants.RelSelf, AtomPubConstants.MediatypeEntry);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelSelf, AtomPubConstants.MediatypeEntry);
            }

            // prefer working copy link if available
            // (workaround for non-compliant repositories)
            string wcLink = GetLink(repositoryId, objectId, AtomPubConstants.RelWorkingCopy, AtomPubConstants.MediatypeEntry);
            if (wcLink != null)
            {
                link = wcLink;
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamCheckinComment, checkinComment);
            url.AddParameter(AtomPubConstants.ParamMajor, major);
            url.AddParameter(AtomPubConstants.ParamCheckIn, "true");

            // set up object and writer
            cmisObjectType cmisObject = new cmisObjectType();
            cmisObject.properties = Converter.Convert(properties);
            cmisObject.policyIds = Converter.ConvertPolicies(policies);

            if (cmisObject.properties == null)
            {
                cmisObject.properties = new cmisPropertiesType();
            }

            string mediaType = null;
            Stream stream = null;
            string filename = null;

            if (contentStream != null)
            {
                mediaType = contentStream.MimeType;
                stream = contentStream.Stream;
                filename = contentStream.FileName;
            }

            AtomEntryWriter entryWriter = new AtomEntryWriter(cmisObject, mediaType, filename, stream);

            // update
            HttpUtils.Response resp = Put(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));

            // parse new entry
            AtomEntry entry = Parse<AtomEntry>(resp.Stream);

            // we expect a CMIS entry
            if (entry.Id == null)
            {
                throw new CmisConnectionException("Received Atom entry is not a CMIS entry!");
            }

            // set object id
            objectId = entry.Id;

            IAcl originalAces = null;

            LockLinks();
            try
            {
                // clean up cache
                RemoveLinks(repositoryId, entry.Id);

                // walk through the entry
                foreach (AtomElement element in entry.GetElements())
                {
                    if (element.Object is AtomLink)
                    {
                        AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                    }
                    else if (element.Object is cmisObjectType)
                    {
                        // extract current ACL
                        cmisObject = (cmisObjectType)element.Object;
                        originalAces = Converter.Convert(cmisObject.acl, cmisObject.exactACLSpecified ? (bool?)cmisObject.exactACL : null);
                    }
                }
            }
            finally
            {
                UnlockLinks();
            }

            // handle ACL modifications
            if ((originalAces != null) && (IsAclMergeRequired(addAces, removeAces)))
            {
                // merge and update ACL
                IAcl newACL = MergeAcls(originalAces, addAces, removeAces);
                if (newACL != null)
                {
                    UpdateAcl(repositoryId, entry.Id, newACL, null);
                }
            }
        }

        public IObjectData GetObjectOfLatestVersion(string repositoryId, string objectId, string versionSeriesId, bool major,
            string filter, bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships,
            string renditionFilter, bool? includePolicyIds, bool? includeAcl, IExtensionsData extension)
        {
            ReturnVersion returnVersion = ReturnVersion.Latest;
            if (major)
            {
                returnVersion = ReturnVersion.LatestMajor;
            }

            return GetObjectInternal(repositoryId, IdentifierType.Id, objectId, returnVersion, filter,
                    includeAllowableActions, includeRelationships, renditionFilter, includePolicyIds, includeAcl, extension);
        }

        public IProperties GetPropertiesOfLatestVersion(string repositoryId, string objectId, string versionSeriesId, bool major,
            string filter, IExtensionsData extension)
        {
            ReturnVersion returnVersion = ReturnVersion.Latest;
            if (major)
            {
                returnVersion = ReturnVersion.LatestMajor;
            }

            IObjectData objectData = GetObjectInternal(repositoryId, IdentifierType.Id, objectId, returnVersion, filter,
                    false, IncludeRelationshipsFlag.None, "cmis:none", false, false, extension);

            return objectData.Properties;
        }

        public IList<IObjectData> GetAllVersions(string repositoryId, string objectId, string versionSeriesId, string filter,
            bool? includeAllowableActions, IExtensionsData extension)
        {
            IList<IObjectData> result = new List<IObjectData>();

            // find the link
            string link = LoadLink(repositoryId, objectId, AtomPubConstants.RelVersionHistory, AtomPubConstants.MediatypeFeed);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelVersionHistory, AtomPubConstants.MediatypeFeed);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);
            url.AddParameter(AtomPubConstants.ParamAllowableActions, includeAllowableActions);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // get the versions
            if (feed.GetEntries().Count > 0)
            {
                foreach (AtomEntry entry in feed.GetEntries())
                {
                    IObjectData version = null;

                    LockLinks();
                    try
                    {
                        // clean up cache
                        RemoveLinks(repositoryId, entry.Id);

                        // walk through the entry
                        foreach (AtomElement element in entry.GetElements())
                        {
                            if (element.Object is AtomLink)
                            {
                                AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                            }
                            else if (element.Object is cmisObjectType)
                            {
                                version = Converter.Convert((cmisObjectType)element.Object);
                            }
                        }
                    }
                    finally
                    {
                        UnlockLinks();
                    }

                    if (version != null)
                    {
                        result.Add(version);
                    }
                }
            }

            return result;
        }
    }

    internal class RelationshipService : AbstractAtomPubService, IRelationshipService
    {
        public RelationshipService(BindingSession session)
        {
            Session = session;
        }

        public IObjectList GetObjectRelationships(string repositoryId, string objectId, bool? includeSubRelationshipTypes,
            RelationshipDirection? relationshipDirection, string typeId, string filter, bool? includeAllowableActions,
            long? maxItems, long? skipCount, IExtensionsData extension)
        {
            ObjectList result = new ObjectList();

            // find the link
            string link = LoadLink(repositoryId, objectId, AtomPubConstants.RelRelationships, AtomPubConstants.MediatypeFeed);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelRelationships, AtomPubConstants.MediatypeFeed);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamSubRelationshipTypes, includeSubRelationshipTypes);
            url.AddParameter(AtomPubConstants.ParamRelationshipDirection, relationshipDirection);
            url.AddParameter(AtomPubConstants.ParamTypeId, typeId);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);
            url.AddParameter(AtomPubConstants.ParamAllowableActions, includeAllowableActions);
            url.AddParameter(AtomPubConstants.ParamMaxItems, maxItems);
            url.AddParameter(AtomPubConstants.ParamSkipCount, skipCount);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // handle top level
            foreach (AtomElement element in feed.GetElements())
            {
                if (element.Object is AtomLink)
                {
                    if (IsNextLink(element))
                    {
                        result.HasMoreItems = true;
                    }
                }
                else if (IsInt(NameNumItems, element))
                {
                    result.NumItems = (long)element.Object;
                }
            }

            // get the children
            if (feed.GetEntries().Count > 0)
            {
                result.Objects = new List<IObjectData>(feed.GetEntries().Count);

                foreach (AtomEntry entry in feed.GetEntries())
                {
                    IObjectData relationship = null;

                    LockLinks();
                    try
                    {
                        // clean up cache
                        RemoveLinks(repositoryId, entry.Id);

                        // walk through the entry
                        foreach (AtomElement element in entry.GetElements())
                        {
                            if (element.Object is AtomLink)
                            {
                                AddLink(repositoryId, entry.Id, (AtomLink)element.Object);
                            }
                            else if (element.Object is cmisObjectType)
                            {
                                relationship = Converter.Convert((cmisObjectType)element.Object);
                            }
                        }
                    }
                    finally
                    {
                        UnlockLinks();
                    }

                    if (relationship != null)
                    {
                        result.Objects.Add(relationship);
                    }
                }
            }

            return result;
        }
    }

    internal class DiscoveryService : AbstractAtomPubService, IDiscoveryService
    {
        public DiscoveryService(BindingSession session)
        {
            Session = session;
        }

        public IObjectList Query(string repositoryId, string statement, bool? searchAllVersions,
            bool? includeAllowableActions, IncludeRelationshipsFlag? includeRelationships, string renditionFilter,
            long? maxItems, long? skipCount, IExtensionsData extension)
        {
            ObjectList result = new ObjectList();

            // find the link
            String link = LoadCollection(repositoryId, AtomPubConstants.CollectionQuery);

            if (link == null)
            {
                throw new CmisObjectNotFoundException("Unknown repository or query not supported!");
            }

            UrlBuilder url = new UrlBuilder(link);

            // compile query request
            AtomQueryWriter queryWriter = new AtomQueryWriter(statement, searchAllVersions, includeAllowableActions,
                includeRelationships, renditionFilter, maxItems, skipCount);

            // post the query and parse results
            HttpUtils.Response resp = Post(url, AtomPubConstants.MediatypeQuery, new HttpUtils.Output(queryWriter.Write));
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // handle top level
            foreach (AtomElement element in feed.GetElements())
            {
                if (element.Object is AtomLink)
                {
                    if (IsNextLink(element))
                    {
                        result.HasMoreItems = true;
                    }
                }
                else if (IsInt(NameNumItems, element))
                {
                    result.NumItems = (long)element.Object;
                }
            }

            // get the result set
            if (feed.GetEntries().Count > 0)
            {
                result.Objects = new List<IObjectData>(feed.GetEntries().Count);

                foreach (AtomEntry entry in feed.GetEntries())
                {
                    IObjectData hit = null;

                    // walk through the entry
                    foreach (AtomElement element in entry.GetElements())
                    {
                        if (element.Object is cmisObjectType)
                        {
                            hit = Converter.Convert((cmisObjectType)element.Object);
                        }
                    }

                    if (hit != null)
                    {
                        result.Objects.Add(hit);
                    }
                }
            }

            return result;
        }

        public IObjectList GetContentChanges(string repositoryId, ref string changeLogToken, bool? includeProperties,
           string filter, bool? includePolicyIds, bool? includeAcl, long? maxItems, IExtensionsData extension)
        {
            ObjectList result = new ObjectList();

            // find the link
            String link = LoadRepositoryLink(repositoryId, AtomPubConstants.RepRelChanges);

            if (link == null)
            {
                throw new CmisObjectNotFoundException("Unknown repository or content changes not supported!");
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamChangeLogToken, changeLogToken);
            url.AddParameter(AtomPubConstants.ParamProperties, includeProperties);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);
            url.AddParameter(AtomPubConstants.ParamPolicyIds, includePolicyIds);
            url.AddParameter(AtomPubConstants.ParamACL, includeAcl);
            url.AddParameter(AtomPubConstants.ParamMaxItems, maxItems);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // handle top level
            foreach (AtomElement element in feed.GetElements())
            {
                if (element.Object is AtomLink)
                {
                    if (IsNextLink(element))
                    {
                        result.HasMoreItems = true;
                    }
                }
                else if (IsInt(NameNumItems, element))
                {
                    result.NumItems = (long)element.Object;
                }
            }

            // get the changes
            if (feed.GetEntries().Count > 0)
            {
                result.Objects = new List<IObjectData>(feed.GetEntries().Count);
                foreach (AtomEntry entry in feed.GetEntries())
                {
                    IObjectData hit = null;

                    // walk through the entry
                    foreach (AtomElement element in entry.GetElements())
                    {
                        if (element.Object is cmisObjectType)
                        {
                            hit = Converter.Convert((cmisObjectType)element.Object);
                        }
                    }

                    if (hit != null)
                    {
                        result.Objects.Add(hit);
                    }
                }
            }

            return result;
        }
    }

    internal class MultiFilingService : AbstractAtomPubService, IMultiFilingService
    {
        public MultiFilingService(BindingSession session)
        {
            Session = session;
        }

        public void AddObjectToFolder(string repositoryId, string objectId, string folderId, bool? allVersions, IExtensionsData extension)
        {
            if (objectId == null)
            {
                throw new CmisInvalidArgumentException("Object id must be set!");
            }

            // find the link
            string link = LoadLink(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);

            if (link == null)
            {
                ThrowLinkException(repositoryId, folderId, AtomPubConstants.RelDown, AtomPubConstants.MediatypeChildren);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamAllVersions, allVersions);

            // set up object and writer
            AtomEntryWriter entryWriter = new AtomEntryWriter(CreateIdObject(objectId));

            // post addObjectToFolder request
            Post(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));
        }

        public void RemoveObjectFromFolder(string repositoryId, string objectId, string folderId, IExtensionsData extension)
        {
            if (objectId == null)
            {
                throw new CmisInvalidArgumentException("Object id must be set!");
            }

            // find the link
            string link = LoadCollection(repositoryId, AtomPubConstants.CollectionUnfiled);

            if (link == null)
            {
                throw new CmisObjectNotFoundException("Unknown repository or unfiling not supported!");
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamRemoveFrom, folderId);

            // set up object and writer
            AtomEntryWriter entryWriter = new AtomEntryWriter(CreateIdObject(objectId));

            // post removeObjectFromFolder request
            Post(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));
        }
    }

    internal class AclService : AbstractAtomPubService, IAclService
    {
        public AclService(BindingSession session)
        {
            Session = session;
        }

        public IAcl GetAcl(string repositoryId, string objectId, bool? onlyBasicPermissions, IExtensionsData extension)
        {
            return GetAclInternal(repositoryId, objectId, onlyBasicPermissions, extension);
        }

        public IAcl ApplyAcl(string repositoryId, string objectId, IAcl addAces, IAcl removeAces, AclPropagation? aclPropagation,
            IExtensionsData extension)
        {
            IAcl result = null;

            // fetch the current ACL
            IAcl originalAces = GetAcl(repositoryId, objectId, false, null);

            // if no changes required, just return the ACL
            if (!IsAclMergeRequired(addAces, removeAces))
            {
                return originalAces;
            }

            // merge ACLs
            IAcl newACL = MergeAcls(originalAces, addAces, removeAces);

            // update ACL
            AtomAcl acl = UpdateAcl(repositoryId, objectId, newACL, aclPropagation);
            result = Converter.Convert(acl.ACL, null);

            return result;
        }
    }

    internal class PolicyService : AbstractAtomPubService, IPolicyService
    {
        public PolicyService(BindingSession session)
        {
            Session = session;
        }

        public void ApplyPolicy(string repositoryId, string policyId, string objectId, IExtensionsData extension)
        {
            // find the link
            string link = LoadLink(repositoryId, objectId, AtomPubConstants.RelPolicies, AtomPubConstants.MediatypeFeed);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelPolicies, AtomPubConstants.MediatypeFeed);
            }

            UrlBuilder url = new UrlBuilder(link);

            // set up object and writer
            AtomEntryWriter entryWriter = new AtomEntryWriter(CreateIdObject(objectId));

            // post applyPolicy request
            Post(url, AtomPubConstants.MediatypeEntry, new HttpUtils.Output(entryWriter.Write));
        }

        public void RemovePolicy(string repositoryId, string policyId, string objectId, IExtensionsData extension)
        {
            // we need a policy id
            if (policyId == null || policyId.Length == 0)
            {
                throw new CmisInvalidArgumentException("Policy id must be set!");
            }

            // find the link
            String link = LoadLink(repositoryId, objectId, AtomPubConstants.RelPolicies, AtomPubConstants.MediatypeFeed);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelPolicies, AtomPubConstants.MediatypeFeed);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamFilter, PropertyIds.ObjectId);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // find the policy
            string policyLink = null;
            bool found = false;

            if (feed.GetEntries().Count > 0)
            {
                foreach (AtomEntry entry in feed.GetEntries())
                {
                    // walk through the entry
                    foreach (AtomElement element in entry.GetElements())
                    {
                        if (element.Object is AtomLink)
                        {
                            AtomLink atomLink = (AtomLink)element.Object;
                            if (AtomPubConstants.RelSelf == atomLink.Rel)
                            {
                                policyLink = atomLink.Href;
                            }
                        }
                        else if (element.Object is cmisObjectType)
                        {
                            string id = FindIdProperty((cmisObjectType)element.Object);
                            if (policyId == id)
                            {
                                found = true;
                            }
                        }
                    }

                    if (found)
                    {
                        break;
                    }
                }
            }

            // if found, delete it
            if (found && policyLink != null)
            {
                Delete(new UrlBuilder(policyLink));
            }
        }

        public IList<IObjectData> GetAppliedPolicies(string repositoryId, string objectId, string filter, IExtensionsData extension)
        {
            IList<IObjectData> result = new List<IObjectData>();

            // find the link
            string link = LoadLink(repositoryId, objectId, AtomPubConstants.RelPolicies, AtomPubConstants.MediatypeFeed);

            if (link == null)
            {
                ThrowLinkException(repositoryId, objectId, AtomPubConstants.RelPolicies, AtomPubConstants.MediatypeFeed);
            }

            UrlBuilder url = new UrlBuilder(link);
            url.AddParameter(AtomPubConstants.ParamFilter, filter);

            // read and parse
            HttpUtils.Response resp = Read(url);
            AtomFeed feed = Parse<AtomFeed>(resp.Stream);

            // get the policies
            if (feed.GetEntries().Count > 0)
            {
                foreach (AtomEntry entry in feed.GetEntries())
                {
                    IObjectData policy = null;

                    // walk through the entry
                    foreach (AtomElement element in entry.GetElements())
                    {
                        if (element.Object is cmisObjectType)
                        {
                            policy = Converter.Convert((cmisObjectType)element.Object);
                        }
                    }

                    if (policy != null)
                    {
                        result.Add(policy);
                    }
                }
            }

            return result;
        }

        private string FindIdProperty(cmisObjectType cmisObject)
        {
            if (cmisObject == null || cmisObject.properties == null)
            {
                return null;
            }

            foreach (cmisProperty property in cmisObject.properties.Items)
            {
                if (PropertyIds.ObjectId == property.propertyDefinitionId && property is cmisPropertyId)
                {
                    string[] values = ((cmisPropertyId)property).value;
                    if (values.Length == 1)
                    {
                        return values[0];
                    }
                }
            }

            return null;
        }
    }
}
