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
using DotCMIS.Binding;
using DotCMIS.Data;
using DotCMIS.Data.Extensions;
using DotCMIS.Enums;

namespace DotCMIS.Client
{
    /// <summary>
    /// Session factory interface.
    /// </summary>
    public interface ISessionFactory
    {
        /// <summary>
        /// Creates a new session with the given parameters and connects to the repository.
        /// </summary>
        /// <param name="parameters">the session parameters</param>
        /// <returns>the newly created session</returns>
        /// <example>
        /// Connect to an AtomPub CMIS endpoint:
        /// <code>
        /// Dictionary&lt;string, string&gt; parameters = new Dictionary&lt;string, string&gt;();
        /// 
        /// parameters[SessionParameter.BindingType] = BindingType.AtomPub;
        /// parameters[SessionParameter.AtomPubUrl] = "http://localhost/cmis/atom";
        /// parameters[SessionParameter.Password] = "admin";
        /// parameters[SessionParameter.User] = "admin";
        /// parameters[SessionParameter.RepositoryId] = "1234-abcd-5678";
        ///
        /// SessionFactory factory = SessionFactory.NewInstance();
        /// ISession session = factory.CreateSession(parameters);
        /// </code>
        /// 
        /// Connect to a Web Services CMIS endpoint:
        /// <code>
        /// Dictionary&lt;string, string&gt; parameters = new Dictionary&lt;string, string&gt;();
        /// 
        /// string baseUrlWS = "https://localhost:443/cmis/ws";
        ///
        /// parameters[SessionParameter.BindingType] = BindingType.WebServices;
        /// parameters[SessionParameter.WebServicesRepositoryService] = baseUrlWS + "/RepositoryService?wsdl";
        /// parameters[SessionParameter.WebServicesAclService] = baseUrlWS + "/AclService?wsdl";
        /// parameters[SessionParameter.WebServicesDiscoveryService] = baseUrlWS + "/DiscoveryService?wsdl";
        /// parameters[SessionParameter.WebServicesMultifilingService] = baseUrlWS + "/MultifilingService?wsdl";
        /// parameters[SessionParameter.WebServicesNavigationService] = baseUrlWS + "/NavigationService?wsdl";
        /// parameters[SessionParameter.WebServicesObjectService] = baseUrlWS + "/ObjectService?wsdl";
        /// parameters[SessionParameter.WebServicesPolicyService] = baseUrlWS + "/PolicyService?wsdl";
        /// parameters[SessionParameter.WebServicesRelationshipService] = baseUrlWS + "/RelationshipService?wsdl";
        /// parameters[SessionParameter.WebServicesVersioningService] = baseUrlWS + "/VersioningService?wsdl";
        /// parameters[SessionParameter.RepositoryId] = "1234-abcd-5678"
        /// parameters[SessionParameter.User] = "admin";
        /// parameters[SessionParameter.Password] = "admin";
        ///
        /// SessionFactory factory = SessionFactory.NewInstance();
        /// ISession session = factory.CreateSession(parameters);
        /// </code>
        /// </example>
        /// <seealso cref="DotCMIS.SessionParameter"/>
        ISession CreateSession(IDictionary<string, string> parameters);

        /// <summary>
        /// Gets all repository available at the specified endpoint.
        /// </summary>
        /// <param name="parameters">the session parameters</param>
        /// <returns>a list of all available repositories</returns>
        /// <seealso cref="DotCMIS.SessionParameter"/>
        IList<IRepository> GetRepositories(IDictionary<string, string> parameters);
    }

    /// <summary>
    /// Repository interface.
    /// </summary>
    public interface IRepository : IRepositoryInfo
    {
        /// <summary>
        /// Creates a session for this repository.
        /// </summary>
        ISession CreateSession();
    }

    /// <summary>
    /// A session is a connection to a CMIS repository with a specific user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not all operations might be supported by the connected repository. Either DotCMIS or the repository will 
    /// throw an exception if an unsupported operation is called. 
    /// The capabilities of the repository can be discovered by evaluating the repository info 
    /// (see <see cref="RepositoryInfo"/>).
    /// </para>
    /// <para>
    /// Almost all methods might throw exceptions derived from <see cref="DotCMIS.Exceptions.CmisBaseException"/>!
    /// </para>
    /// <para>
    /// (Please refer to the <a href="http://docs.oasis-open.org/cmis/CMIS/v1.0/os/">CMIS specification</a>
    /// for details about the domain model, terms, concepts, base types, properties, ids and query names, 
    /// query language, etc.)
    /// </para>
    /// </remarks>
    public interface ISession
    {
        /// <summary>
        /// Clears all caches.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets the CMIS binding object.
        /// </summary>
        ICmisBinding Binding { get; }

        /// <summary>
        /// Gets the default operation context.
        /// </summary>
        IOperationContext DefaultContext { get; set; }

        /// <summary>
        /// Creates a new operation context object.
        /// </summary>
        IOperationContext CreateOperationContext();

        /// <summary>
        /// Creates a new operation context object with the given parameters.
        /// </summary>
        IOperationContext CreateOperationContext(HashSet<string> filter, bool includeAcls, bool includeAllowableActions, bool includePolicies,
            IncludeRelationshipsFlag includeRelationships, HashSet<string> renditionFilter, bool includePathSegments, string orderBy,
            bool cacheEnabled, int maxItemsPerPage);

        /// <summary>
        /// Creates a new <see cref="DotCMIS.Client.IObjectId"/> with the giveb id.
        /// </summary>
        IObjectId CreateObjectId(string id);

        /// <summary>
        /// Gets the CMIS repositoy info.
        /// </summary>
        IRepositoryInfo RepositoryInfo { get; }

        /// <summary>
        /// Gets the internal object factory. 
        /// </summary>
        IObjectFactory ObjectFactory { get; }

        // types

        IObjectType GetTypeDefinition(string typeId);
        IItemEnumerable<IObjectType> GetTypeChildren(string typeId, bool includePropertyDefinitions);
        IList<ITree<IObjectType>> GetTypeDescendants(string typeId, int depth, bool includePropertyDefinitions);

        // navigation

        IFolder GetRootFolder();
        IFolder GetRootFolder(IOperationContext context);
        IItemEnumerable<IDocument> GetCheckedOutDocs();
        IItemEnumerable<IDocument> GetCheckedOutDocs(IOperationContext context);

        /// <summary>
        /// Gets a CMIS object from the session cache. If the object is not in the cache or the cache is 
        /// turned off per default <see cref="DotCMIS.Client.IOperationContext"/>, it will load the object 
        /// from the repository and puts it into the cache.
        /// </summary>
        /// <param name="objectId">the object id</param>
        ICmisObject GetObject(IObjectId objectId);
        ICmisObject GetObject(IObjectId objectId, IOperationContext context);

        /// <summary>
        /// Gets a CMIS object from the session cache. If the object is not in the cache or the cache is 
        /// turned off per default <see cref="DotCMIS.Client.IOperationContext"/>, it will load the object 
        /// from the repository and puts it into the cache.
        /// </summary>
        /// <param name="objectId">the object id</param>
        ICmisObject GetObject(string objectId);
        ICmisObject GetObject(string objectId, IOperationContext context);

        /// <summary>
        /// Gets a CMIS object from the session cache. If the object is not in the cache or the cache is 
        /// turned off per default <see cref="DotCMIS.Client.IOperationContext"/>, it will load the object
        /// from the repository and puts it into the cache.
        /// </summary>
        /// <param name="path">the path to the object</param>
        ICmisObject GetObjectByPath(string path);
        ICmisObject GetObjectByPath(string path, IOperationContext context);

        /// <summary>
        ///  Removes the given object from the cache.
        /// </summary>
        /// <param name="objectId">the object id</param>
        void RemoveObjectFromCache(IObjectId objectId);

        /// <summary>
        ///  Removes the given object from the cache.
        /// </summary>
        /// <param name="objectId">the object id</param>
        void RemoveObjectFromCache(string objectId);

        // discovery

        /// <summary>
        /// Performs a query.
        /// </summary>
        /// <param name="statement">the CMIS QL statement</param>
        /// <param name="searchAllVersions">indicates if all versions or only latest version should be searched</param>
        /// <returns>query results</returns>
        IItemEnumerable<IQueryResult> Query(string statement, bool searchAllVersions);

        /// <summary>
        /// Performs a query using the given <see cref="DotCMIS.Client.IOperationContext"/>.
        /// </summary>
        /// <param name="statement">the CMIS QL statement</param>
        /// <param name="searchAllVersions">indicates if all versions or only latest version should be searched</param>
        /// <param name="context">the <see cref="DotCMIS.Client.IOperationContext"/></param>
        /// <returns>query results</returns>
        IItemEnumerable<IQueryResult> Query(string statement, bool searchAllVersions, IOperationContext context);

        IChangeEvents GetContentChanges(string changeLogToken, bool includeProperties, long maxNumItems);
        IChangeEvents GetContentChanges(string changeLogToken, bool includeProperties, long maxNumItems,
                IOperationContext context);

        // create

        IObjectId CreateDocument(IDictionary<string, object> properties, IObjectId folderId, IContentStream contentStream,
                VersioningState? versioningState, IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces);
        IObjectId CreateDocument(IDictionary<string, object> properties, IObjectId folderId, IContentStream contentStream,
                VersioningState? versioningState);
        IObjectId CreateDocumentFromSource(IObjectId source, IDictionary<string, object> properties, IObjectId folderId,
                VersioningState? versioningState, IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces);
        IObjectId CreateDocumentFromSource(IObjectId source, IDictionary<string, object> properties, IObjectId folderId,
                VersioningState? versioningState);
        IObjectId CreateFolder(IDictionary<string, object> properties, IObjectId folderId, IList<IPolicy> policies, IList<IAce> addAces,
                IList<IAce> removeAces);
        IObjectId CreateFolder(IDictionary<string, object> properties, IObjectId folderId);
        IObjectId CreatePolicy(IDictionary<string, object> properties, IObjectId folderId, IList<IPolicy> policies, IList<IAce> addAces,
                IList<IAce> removeAces);
        IObjectId CreatePolicy(IDictionary<string, object> properties, IObjectId folderId);
        IObjectId CreateRelationship(IDictionary<string, object> properties, IList<IPolicy> policies, IList<IAce> addAces,
                IList<IAce> removeAces);
        IObjectId CreateRelationship(IDictionary<string, object> properties);

        IItemEnumerable<IRelationship> GetRelationships(IObjectId objectId, bool includeSubRelationshipTypes,
                RelationshipDirection? relationshipDirection, IObjectType type, IOperationContext context);

        // delete
        void Delete(IObjectId objectId);
        void Delete(IObjectId objectId, bool allVersions);

        // content stream
        IContentStream GetContentStream(IObjectId docId);
        IContentStream GetContentStream(IObjectId docId, string streamId, long? offset, long? length);

        // permissions
        IAcl GetAcl(IObjectId objectId, bool onlyBasicPermissions);
        IAcl ApplyAcl(IObjectId objectId, IList<IAce> addAces, IList<IAce> removeAces, AclPropagation? aclPropagation);
        void ApplyPolicy(IObjectId objectId, params IObjectId[] policyIds);
        void RemovePolicy(IObjectId objectId, params IObjectId[] policyIds);
    }

    public interface IObjectFactory
    {
        void Initialize(ISession session, IDictionary<string, string> parameters);

        // ACL and ACE
        IAcl ConvertAces(IList<IAce> aces);
        IAcl CreateAcl(IList<IAce> aces);
        IAce CreateAce(string principal, IList<string> permissions);

        // policies
        IList<string> ConvertPolicies(IList<IPolicy> policies);

        // renditions
        IRendition ConvertRendition(string objectId, IRenditionData rendition);

        // content stream
        IContentStream CreateContentStream(string filename, long length, string mimetype, Stream stream);

        // types
        IObjectType ConvertTypeDefinition(ITypeDefinition typeDefinition);
        IObjectType GetTypeFromObjectData(IObjectData objectData);

        // properties
        IProperty CreateProperty<T>(IPropertyDefinition type, IList<T> values);
        IDictionary<string, IProperty> ConvertProperties(IObjectType objectType, IProperties properties);
        IProperties ConvertProperties(IDictionary<string, object> properties, IObjectType type, HashSet<Updatability> updatabilityFilter);
        IList<IPropertyData> ConvertQueryProperties(IProperties properties);

        // objects
        ICmisObject ConvertObject(IObjectData objectData, IOperationContext context);
        IQueryResult ConvertQueryResult(IObjectData objectData);
        IChangeEvent ConvertChangeEvent(IObjectData objectData);
        IChangeEvents ConvertChangeEvents(string changeLogToken, IObjectList objectList);
    }

    /// <summary>
    /// Operation context interface.
    /// </summary>
    public interface IOperationContext
    {
        /// <summary>
        /// Gets and sets the property filter.
        /// </summary>
        /// <remarks>
        /// This is a set of query names.
        /// </remarks>
        HashSet<string> Filter { get; set; }

        /// <summary>
        /// Gets and sets the property filter.
        /// </summary>
        /// <remarks>
        /// This is a comma-separated list of query names.
        /// </remarks>
        string FilterString { get; set; }

        /// <summary>
        /// Gets and sets if allowable actions should be retrieved.
        /// </summary>
        bool IncludeAllowableActions { get; set; }

        /// <summary>
        /// Gets and sets if ACLs should be retrieved.
        /// </summary>
        bool IncludeAcls { get; set; }

        /// <summary>
        /// Gets and sets if relationships should be retrieved.
        /// </summary>
        IncludeRelationshipsFlag? IncludeRelationships { get; set; }

        /// <summary>
        /// Gets and sets if policies should be retrieved.
        /// </summary>
        bool IncludePolicies { get; set; }

        /// <summary>
        /// Gets and sets the rendition filter.
        /// </summary>
        /// <remarks>
        /// This is a set of rendition kinds or MIME types.
        /// </remarks>
        HashSet<string> RenditionFilter { get; set; }

        /// <summary>
        /// Gets and sets the rendition filter.
        /// </summary>
        /// <remarks>
        /// This is a comma-separated list of rendition kinds or MIME types.
        /// </remarks>
        string RenditionFilterString { get; set; }

        /// <summary>
        /// Gets and sets if path segements should be retrieved.
        /// </summary>
        bool IncludePathSegments { get; set; }

        /// <summary>
        /// Gets and sets order by list. 
        /// </summary>
        /// <remarks>
        /// This is a comma-separated list of query names.
        /// </remarks>
        string OrderBy { get; set; }

        /// <summary>
        /// Gets and sets if object fetched with this <see cref="DotCMIS.Client.IOperationContext"/>
        /// should be cached or not.
        /// </summary>
        bool CacheEnabled { get; set; }

        /// <summary>
        /// Gets the cache key. (For internal use.)
        /// </summary>
        string CacheKey { get; }

        /// <summary>
        /// Gets and sets how many items should be fetched per page.
        /// </summary>
        int MaxItemsPerPage { get; set; }
    }

    public interface ITree<T>
    {
        T Item { get; }
        IList<ITree<T>> Children { get; }
    }

    /// <summary>
    /// Base interface for all CMIS types.
    /// </summary>
    public interface IObjectType : ITypeDefinition
    {
        bool IsBaseType { get; }
        IObjectType GetBaseType();
        IObjectType GetParentType();
        IItemEnumerable<IObjectType> GetChildren();
        IList<ITree<IObjectType>> GetDescendants(int depth);
    }

    /// <summary>
    /// Document type interface.
    /// </summary>
    public interface IDocumentType : IObjectType
    {
        bool? IsVersionable { get; }
        ContentStreamAllowed? ContentStreamAllowed { get; }
    }

    /// <summary>
    /// Folder type interface.
    /// </summary>
    public interface IFolderType : IObjectType
    {
    }

    /// <summary>
    /// Relationship type interface.
    /// </summary>
    public interface IRelationshipType : IObjectType
    {
        IList<IObjectType> GetAllowedSourceTypes { get; }
        IList<IObjectType> GetAllowedTargetTypes { get; }
    }

    /// <summary>
    /// Policy type interface.
    /// </summary>
    public interface IPolicyType : IObjectType
    {
    }

    public interface IItemEnumerable<T> : IEnumerable<T>
    {
        IItemEnumerable<T> SkipTo(long position);
        IItemEnumerable<T> GetPage();
        IItemEnumerable<T> GetPage(int maxNumItems);
        long PageNumItems { get; }
        bool HasMoreItems { get; }
        long TotalNumItems { get; }
    }

    public interface IObjectId
    {
        /// <summary>
        /// Gets the object id.
        /// </summary>
        string Id { get; }
    }

    public interface IRendition : IRenditionData
    {
        IDocument GetRenditionDocument();
        IDocument GetRenditionDocument(IOperationContext context);
        IContentStream GetContentStream();
    }

    /// <summary>
    /// Property interface.
    /// </summary>
    public interface IProperty
    {
        /// <summary>
        /// Gets the property id.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the property local name.
        /// </summary>
        string LocalName { get; }

        /// <summary>
        /// Gets the property display name.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the property query name.
        /// </summary>
        string QueryName { get; }

        /// <summary>
        /// Gets if the property is a multi-value proprty.
        /// </summary>
        bool IsMultiValued { get; }

        /// <summary>
        /// Gets the proprty type.
        /// </summary>
        PropertyType? PropertyType { get; }

        /// <summary>
        /// Gets the property defintion.
        /// </summary>
        IPropertyDefinition PropertyDefinition { get; }

        /// <summary>
        /// Gets the value of the property.
        /// </summary>
        /// <remarks>
        /// If the property is a single-value property the single value is returned.
        /// If the property is a multi-value property a IList&lt;object&gt; is returned.
        /// </remarks>
        object Value { get; }

        /// <summary>
        /// Gets the value list of the property.
        /// </summary>
        /// <remarks>
        /// If the property is a single-value property a list with one or no items is returend.
        /// </remarks>
        IList<object> Values { get; }

        /// <summary>
        /// Gets the first value of the value list or <c>null</c> if the list has no values.
        /// </summary>
        object FirstValue { get; }

        /// <summary>
        /// Gets a string representation of the first value of the value list.
        /// </summary>
        string ValueAsString { get; }

        /// <summary>
        /// Gets a string representation of the value list.
        /// </summary>
        string ValuesAsString { get; }
    }

    /// <summary>
    /// Collection of common CMIS properties.
    /// </summary>
    public interface ICmisObjectProperties
    {
        /// <summary>
        /// Gets a list of all available CMIS properties.
        /// </summary>
        IList<IProperty> Properties { get; }

        /// <summary>
        /// available
        /// </summary>
        /// <param name="propertyId">the property id</param>
        /// <returns>the property or <c>null</c> if the property is not available</returns>
        IProperty this[string propertyId] { get; }

        /// <summary>
        /// Gets the value of the requested property.
        /// </summary>
        /// <param name="propertyId">the property id</param>
        /// <returns>the property value or <c>null</c> if the property is not available or not set</returns>
        object GetPropertyValue(string propertyId);

        /// <summary>
        /// Gets the name of this CMIS object (CMIS property <c>cmis:name</c>).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the user who created this CMIS object (CMIS property <c>cmis:createdBy</c>).
        /// </summary>
        string CreatedBy { get; }

        /// <summary>
        /// Gets the timestamp when this CMIS object has been created (CMIS property <c>cmis:creationDate</c>).
        /// </summary>
        DateTime? CreationDate { get; }

        /// <summary>
        /// Gets the user who modified this CMIS object (CMIS property <c>cmis:lastModifiedBy</c>).
        /// </summary>
        string LastModifiedBy { get; }

        /// <summary>
        /// Gets the timestamp when this CMIS object has been modified (CMIS property <c>cmis:lastModificationDate</c>).
        /// </summary>
        DateTime? LastModificationDate { get; }

        /// <summary>
        /// Gets the id of the base type of this CMIS object (CMIS property <c>cmis:baseTypeId</c>).
        /// </summary>
        BaseTypeId BaseTypeId { get; }

        /// <summary>
        /// Gets the base type of this CMIS object (object type identified by <c>cmis:baseTypeId</c>).
        /// </summary>
        IObjectType BaseType { get; }

        /// <summary>
        /// Gets the type of this CMIS object (object type identified by <c>cmis:objectTypeId</c>).
        /// </summary>
        IObjectType ObjectType { get; }

        /// <summary>
        /// Gets the change token (CMIS property <c>cmis:changeToken</c>).
        /// </summary>
        string ChangeToken { get; }
    }

    public enum ExtensionLevel
    {
        Object, Properties, AllowableActions, Acl, Policies, ChangeEvent
    }

    /// <summary>
    /// Base interface for all CMIS objects.
    /// </summary>
    public interface ICmisObject : IObjectId, ICmisObjectProperties
    {
        /// <summary>
        /// Gets the allowable actions if they have been fetched for this object.
        /// </summary>
        IAllowableActions AllowableActions { get; }

        /// <summary>
        /// Gets the relationships if they have been fetched for this object.
        /// </summary>
        IList<IRelationship> Relationships { get; }

        /// <summary>
        /// Gets the ACL if it has been fetched for this object.
        /// </summary>
        IAcl Acl { get; }

        /// <summary>
        /// Deletes this object.
        /// </summary>
        /// <param name="allVersions">if this object is a document this parameter defines if just this version or all versions should be deleted</param>
        void Delete(bool allVersions);

        /// <summary>
        /// Updates the properties that are provided.
        /// </summary>
        /// <param name="properties">the properties to update</param>
        /// <returns>the updated object (a repository might have created a new object)</returns>
        ICmisObject UpdateProperties(IDictionary<string, object> properties);

        /// <summary>
        /// Updates the properties that are provided.
        /// </summary>
        /// <param name="properties">the properties to update</param>
        /// <param name="refresh">indicates if the object should be refresh after the update</param>
        /// <returns>the object id of the updated object (a repository might have created a new object)</returns>
        IObjectId UpdateProperties(IDictionary<string, object> properties, bool refresh);

        /// <summary>
        /// Gets the renditions if they have been fetched for this object.
        /// </summary>
        IList<IRendition> Renditions { get; }

        /// <summary>
        /// Applies the given policies to the object.
        /// </summary>
        void ApplyPolicy(params IObjectId[] policyId);

        /// <summary>
        /// Removes the given policies from the object.
        /// </summary>
        void RemovePolicy(params IObjectId[] policyId);

        /// <summary>
        /// Gets a list of policies applied to this object.
        /// </summary>
        IList<IPolicy> Policies { get; }

        /// <summary>
        /// Adds and removes ACEs to this object.
        /// </summary>
        /// <returns>the new ACL of this object</returns>
        IAcl ApplyAcl(IList<IAce> addAces, IList<IAce> removeAces, AclPropagation? aclPropagation);

        /// <summary>
        /// Adds ACEs to this object.
        /// </summary>
        /// <returns>the new ACL of this object</returns>
        IAcl AddAcl(IList<IAce> addAces, AclPropagation? aclPropagation);

        /// <summary>
        /// Removes ACEs from this object.
        /// </summary>
        /// <returns>the new ACL of this object</returns>
        IAcl RemoveAcl(IList<IAce> removeAces, AclPropagation? aclPropagation);

        /// <summary>
        /// Gets the extensions of the given level.
        /// </summary>
        IList<ICmisExtensionElement> GetExtensions(ExtensionLevel level);

        /// <summary>
        /// Gets the timestamp of the last refresh.
        /// </summary>
        DateTime RefreshTimestamp { get; }

        /// <summary>
        /// Reloads the data from the repository.
        /// </summary>
        void Refresh();

        /// <summary>
        /// Reloads the data from the repository if the last refresh did not occur within <c>durationInMillis</c>.
        /// </summary>
        void RefreshIfOld(long durationInMillis);
    }

    /// <summary>
    /// Base interface for all fileable CMIS objects.
    /// </summary>
    public interface IFileableCmisObject : ICmisObject
    {
        /// <summary>
        /// Moves this object from a source folder to a target folder.
        /// </summary>
        /// <param name="sourceFolderId">the source folder id</param>
        /// <param name="targetFolderId">the target folder id</param>
        /// <returns>the object in the new location</returns>
        IFileableCmisObject Move(IObjectId sourceFolderId, IObjectId targetFolderId);

        /// <summary>
        /// Gets a list of all parent folders. 
        /// </summary>
        /// <remarks>
        /// Returns an empty list if it is an unfiled object or the root folder.
        /// </remarks>
        IList<IFolder> Parents { get; }

        /// <summary>
        /// Gets all paths for this object
        /// </summary>
        /// <remarks>
        /// Returns an empty list for unfiled objects.
        /// </remarks>
        IList<string> Paths { get; }

        /// <summary>
        /// Adds this object to the given folder.
        /// </summary>
        /// <param name="folderId">the id of the target folder</param>
        /// <param name="allVersions">indicates if only this object or all versions of the object should be added</param>
        void AddToFolder(IObjectId folderId, bool allVersions);

        /// <summary>
        /// Removes this object from the given folder.
        /// </summary>
        /// <param name="folderId">the id of the folder</param>
        void RemoveFromFolder(IObjectId folderId);
    }

    /// <summary>
    /// Document properties.
    /// </summary>
    public interface IDocumentProperties
    {
        /// <summary>
        /// Gets if this CMIS object is immutable (CMIS property <c>cmis:isImmutable</c>).
        /// </summary>
        bool? IsImmutable { get; }

        /// <summary>
        /// Gets if this CMIS object is the latest version (CMIS property <c>cmis:isLatestVersion</c>)
        /// </summary>
        bool? IsLatestVersion { get; }

        /// <summary>
        /// Gets if this CMIS object is the latest version (CMIS property <c>cmis:isMajorVersion</c>).
        /// </summary>
        bool? IsMajorVersion { get; }

        /// <summary>
        /// Gets if this CMIS object is the latest major version (CMIS property <c>cmis:isLatestMajorVersion</c>).
        /// </summary>
        bool? IsLatestMajorVersion { get; }

        /// <summary>
        /// Gets the version label (CMIS property <c>cmis:versionLabel</c>).
        /// </summary>
        string VersionLabel { get; }

        /// <summary>
        /// Gets the version series id (CMIS property <c>cmis:versionSeriesId</c>).
        /// </summary>
        string VersionSeriesId { get; }

        /// <summary>
        /// Gets if this version series is checked out (CMIS property <c>cmis:isVersionSeriesCheckedOut</c>).
        /// </summary>
        bool? IsVersionSeriesCheckedOut { get; }

        /// <summary>
        /// Gets the user who checked out this version series (CMIS property <c>cmis:versionSeriesCheckedOutBy</c>).
        /// </summary>
        string VersionSeriesCheckedOutBy { get; }

        /// <summary>
        /// Gets the PWC id of this version series (CMIS property <c>cmis:versionSeriesCheckedOutId</c>).
        /// </summary>
        string VersionSeriesCheckedOutId { get; }

        /// <summary>
        /// Gets the checkin comment (CMIS property <c>cmis:checkinComment</c>).
        /// </summary>
        string CheckinComment { get; }

        /// <summary>
        /// Gets the content stream length or <c>null</c> if the document has no content (CMIS property <c>cmis:contentStreamLength</c>).
        /// </summary>
        long? ContentStreamLength { get; }

        /// <summary>
        /// Gets the content stream MIME type or <c>null</c> if the document has no content (CMIS property <c>cmis:contentStreamMimeType</c>).
        /// </summary>
        string ContentStreamMimeType { get; }

        /// <summary>
        /// Gets the content stream filename or <c>null</c> if the document has no content (CMIS property <c>cmis:contentStreamFileName</c>).
        /// </summary>
        string ContentStreamFileName { get; }

        /// <summary>
        /// Gets the content stream id or <c>null</c> if the document has no content (CMIS property <c>cmis:contentStreamId</c>).
        /// </summary>
        string ContentStreamId { get; }
    }

    /// <summary>
    /// Document interface.
    /// </summary>
    public interface IDocument : IFileableCmisObject, IDocumentProperties
    {
        /// <summary>
        /// Deletes all versions of this document.
        /// </summary>
        void DeleteAllVersions();

        /// <summary>
        /// Gets the content stream of this document.
        /// </summary>
        /// <returns>the content stream or <c>null</c> if the document has no content</returns>
        IContentStream GetContentStream();

        /// <summary>
        /// Gets the content stream identified by the given stream id.
        /// </summary>
        /// <returns>the content stream or <c>null</c> if the stream id is not associated with content</returns>
        IContentStream GetContentStream(string streamId);

        /// <summary>
        /// Gets the content stream identified by the given stream id with the given offset and length.
        /// </summary>
        /// <returns>the content stream or <c>null</c> if the stream id is not associated with content</returns>
        IContentStream GetContentStream(string streamId, long? offset, long? length);

        /// <summary>
        /// Sets a new content stream for this document.
        /// </summary>
        /// <param name="contentStream">the content stream</param>
        /// <param name="overwrite">indicates if the current stream should be overwritten</param>
        /// <returns>the new document object</returns>
        /// <remarks>
        /// Repositories might create a new version if the content is updated.
        /// </remarks>
        IDocument SetContentStream(IContentStream contentStream, bool overwrite);

        /// <summary>
        /// Sets a new content stream for this document.
        /// </summary>
        /// <param name="contentStream">the content stream</param>
        /// <param name="overwrite">indicates if the current stream should be overwritten</param>
        /// <param name="refresh">indicates if this object should be refreshed after the new content is set</param>
        /// <returns>the new document object id</returns>
        /// <remarks>
        /// Repositories might create a new version if the content is updated.
        /// </remarks>
        IObjectId SetContentStream(IContentStream contentStream, bool overwrite, bool refresh);

        /// <summary>
        /// Deletes the current content stream for this document.
        /// </summary>
        /// <returns>the new document object</returns>
        /// <remarks>
        /// Repositories might create a new version if the content is deleted.
        /// </remarks>
        IDocument DeleteContentStream();

        /// <summary>
        /// Deletes the current content stream for this document.
        /// </summary>
        /// <param name="refresh">indicates if this object should be refreshed after the content is deleted</param>
        /// <returns>the new document object id</returns>
        /// <remarks>
        /// Repositories might create a new version if the content is deleted.
        /// </remarks>
        IObjectId DeleteContentStream(bool refresh);

        /// <summary>
        /// Checks out this document.
        /// </summary>
        /// <returns>the object id of the newly created private working copy (PWC).</returns>
        IObjectId CheckOut();

        /// <summary>
        /// Cancels the check out.
        /// </summary>
        void CancelCheckOut();

        /// <summary>
        /// Checks in this private working copy (PWC).
        /// </summary>
        /// <returns>the object id of the new created document</returns>
        IObjectId CheckIn(bool major, IDictionary<string, object> properties, IContentStream contentStream, string checkinComment,
                IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces);

        /// <summary>
        /// Checks in this private working copy (PWC).
        /// </summary>
        /// <returns>the object id of the new created document</returns>
        IObjectId CheckIn(bool major, IDictionary<string, object> properties, IContentStream contentStream, string checkinComment);

        IDocument GetObjectOfLatestVersion(bool major);
        IDocument GetObjectOfLatestVersion(bool major, IOperationContext context);

        /// <summary>
        /// Gets a list of all versions in this version series.
        /// </summary>
        IList<IDocument> GetAllVersions();

        /// <summary>
        /// Gets a list of all versions in this version series using the given <see cref="DotCMIS.Client.IOperationContext"/>.
        /// </summary>
        IList<IDocument> GetAllVersions(IOperationContext context);

        IDocument Copy(IObjectId targetFolderId);
        IDocument Copy(IObjectId targetFolderId, IDictionary<string, object> properties, VersioningState? versioningState,
                IList<IPolicy> policies, IList<IAce> addACEs, IList<IAce> removeACEs, IOperationContext context);
    }

    /// <summary>
    /// Folder properties.
    /// </summary>
    public interface IFolderProperties
    {
        string ParentId { get; }
        IList<IObjectType> AllowedChildObjectTypes { get; }
    }

    /// <summary>
    /// Folder interface.
    /// </summary>
    public interface IFolder : IFileableCmisObject, IFolderProperties
    {
        IDocument CreateDocument(IDictionary<string, object> properties, IContentStream contentStream, VersioningState? versioningState,
                IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces, IOperationContext context);
        IDocument CreateDocument(IDictionary<string, object> properties, IContentStream contentStream, VersioningState? versioningState);
        IDocument CreateDocumentFromSource(IObjectId source, IDictionary<string, object> properties, VersioningState? versioningState,
                IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces, IOperationContext context);
        IDocument CreateDocumentFromSource(IObjectId source, IDictionary<string, object> properties, VersioningState? versioningState);
        IFolder CreateFolder(IDictionary<string, object> properties, IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces,
                IOperationContext context);
        IFolder CreateFolder(IDictionary<string, object> properties);
        IPolicy CreatePolicy(IDictionary<string, object> properties, IList<IPolicy> policies, IList<IAce> addAces, IList<IAce> removeAces,
                IOperationContext context);
        IPolicy CreatePolicy(IDictionary<string, object> properties);
        IList<string> DeleteTree(bool allversions, UnfileObject? unfile, bool continueOnFailure);

        /// <summary>
        /// Gets the folder tress of this folder (only folder).
        /// </summary>
        /// <param name="depth">the depth</param>
        /// <returns>a list of folder trees</returns>
        /// <remarks>
        /// If depth == 1 only objects that are children of this folder are returned.
        /// If depth &gt; 1 only objects that are children of this folder and descendants up to "depth" levels deep are returned.
        /// If depth == -1 all descendant objects at all depth levels in the CMIS hierarchy are returned.
        /// </remarks>
        IList<ITree<IFileableCmisObject>> GetFolderTree(int depth);

        /// <summary>
        /// Gets the folder tress of this folder (only folder) using the given <see cref="DotCMIS.Client.IOperationContext"/>.
        /// </summary>
        /// <param name="depth">the depth</param>
        /// <param name="context">the <see cref="DotCMIS.Client.IOperationContext"/></param>
        /// <returns>a list of folder trees</returns>
        /// <remarks>
        /// If depth == 1 only objects that are children of this folder are returned.
        /// If depth &gt; 1 only objects that are children of this folder and descendants up to "depth" levels deep are returned.
        /// If depth == -1 all descendant objects at all depth levels in the CMIS hierarchy are returned.
        /// </remarks>
        IList<ITree<IFileableCmisObject>> GetFolderTree(int depth, IOperationContext context);

        /// <summary>
        /// Gets the descendants of this folder (all filable objects).
        /// </summary>
        /// <param name="depth">the depth</param>
        /// <returns>a list of descendant trees</returns>
        /// <remarks>
        /// If depth == 1 only objects that are children of this folder are returned.
        /// If depth &gt; 1 only objects that are children of this folder and descendants up to "depth" levels deep are returned.
        /// If depth == -1 all descendant objects at all depth levels in the CMIS hierarchy are returned.
        /// </remarks>
        IList<ITree<IFileableCmisObject>> GetDescendants(int depth);

        /// <summary>
        /// Gets the descendants of this folder (all filable objects) using the given <see cref="DotCMIS.Client.IOperationContext"/>.
        /// </summary>
        /// <param name="depth">the depth</param>
        /// <param name="context">the <see cref="DotCMIS.Client.IOperationContext"/></param>
        /// <returns>a list of descendant trees</returns>
        /// <remarks>
        /// If depth == 1 only objects that are children of this folder are returned.
        /// If depth &gt; 1 only objects that are children of this folder and descendants up to "depth" levels deep are returned.
        /// If depth == -1 all descendant objects at all depth levels in the CMIS hierarchy are returned.
        /// </remarks>
        IList<ITree<IFileableCmisObject>> GetDescendants(int depth, IOperationContext context);

        /// <summary>
        /// Gets the children of this folder.
        /// </summary>
        IItemEnumerable<ICmisObject> GetChildren();

        /// <summary>
        /// Gets the children of this folder ussing the given <see cref="DotCMIS.Client.IOperationContext"/>.
        /// </summary>
        IItemEnumerable<ICmisObject> GetChildren(IOperationContext context);

        /// <summary>
        /// Gets if this folder is the root folder.
        /// </summary>
        bool IsRootFolder { get; }

        /// <summary>
        /// Gets the parent of this folder or <c>null</c> if this folder is the root folder.
        /// </summary>
        IFolder FolderParent { get; }

        /// <summary>
        /// Gets the path of this folder.
        /// </summary>
        string Path { get; }

        IItemEnumerable<IDocument> GetCheckedOutDocs();
        IItemEnumerable<IDocument> GetCheckedOutDocs(IOperationContext context);
    }

    /// <summary>
    /// Policy properties.
    /// </summary>
    public interface IPolicyProperties
    {
        /// <summary>
        /// Gets the policy text of this CMIS policy (CMIS property <c>cmis:policyText</c>).
        /// </summary>
        string PolicyText { get; }
    }

    /// <summary>
    /// Policy interface.
    /// </summary>
    public interface IPolicy : IFileableCmisObject, IPolicyProperties
    {
    }

    /// <summary>
    /// Relationship properties.
    /// </summary>
    public interface IRelationshipProperties
    {
        /// <summary>
        /// Gets the id of the relationship source object.
        /// </summary>
        IObjectId SourceId { get; }

        /// <summary>
        /// Gets the id of the relationships target object.
        /// </summary>
        IObjectId TargetId { get; }
    }

    /// <summary>
    /// Relationship interface.
    /// </summary>
    public interface IRelationship : ICmisObject, IRelationshipProperties
    {
        /// <summary>
        /// Gets the relationship source object.
        /// </summary>
        /// <remarks>
        /// If the source object id is invalid, <c>null</c> will be returned.
        /// </remarks>
        ICmisObject GetSource();

        /// <summary>
        /// Gets the relationship source object using the given <see cref="DotCMIS.Client.IOperationContext"/>.
        /// </summary>
        /// <remarks>
        /// If the source object id is invalid, <c>null</c> will be returned.
        /// </remarks>
        ICmisObject GetSource(IOperationContext context);

        /// <summary>
        /// Gets the relationship target object.
        /// </summary>
        /// <remarks>
        /// If the target object id is invalid, <c>null</c> will be returned.
        /// </remarks>
        ICmisObject GetTarget();

        /// <summary>
        /// Gets the relationship target object using the given <see cref="DotCMIS.Client.IOperationContext"/>.
        /// </summary>
        /// <remarks>
        /// If the target object id is invalid, <c>null</c> will be returned.
        /// </remarks>
        ICmisObject GetTarget(IOperationContext context);
    }

    /// <summary>
    /// Query result.
    /// </summary>
    public interface IQueryResult
    {
        /// <summary>
        /// Gets the property.
        /// </summary>
        /// <param name="queryName">the propertys query name or alias</param>
        IPropertyData this[string queryName] { get; }

        /// <summary>
        /// Gets a list of all properties in this query result.
        /// </summary>
        IList<IPropertyData> Properties { get; }

        /// <summary>
        /// Returns a property by id.
        /// </summary>
        /// <param name="propertyId">the property id</param>
        /// <remarks>
        /// Since repositories are not obligated to add property ids to their
        /// query result properties, this method might not always work as expected with
        /// some repositories. Use <see cref="P:this[string]"/> instead.
        /// </remarks>
        IPropertyData GetPropertyById(string propertyId);

        /// <summary>
        /// Gets the property (single) value by query name or alias.
        /// </summary>
        object GetPropertyValueByQueryName(string queryName);

        /// <summary>
        /// Gets the property (single) value by property id.
        /// </summary>
        object GetPropertyValueById(string propertyId);

        /// <summary>
        /// Gets the property value by query name or alias.
        /// </summary>
        IList<object> GetPropertyMultivalueByQueryName(string queryName);

        /// <summary>
        /// Gets the property value by property id.
        /// </summary>
        IList<object> GetPropertyMultivalueById(string propertyId);

        /// <summary>
        /// Gets the allowable actions if they were requested.
        /// </summary>
        IAllowableActions AllowableActions { get; }

        /// <summary>
        /// Gets the relationships if they were requested.
        /// </summary>
        IList<IRelationship> Relationships { get; }

        /// <summary>
        /// Gets the renditions if they were requested.
        /// </summary>
        IList<IRendition> Renditions { get; }
    }

    public interface IChangeEvent : IChangeEventInfo
    {
        string ObjectId { get; }
        IDictionary<string, IList<object>> Properties { get; }
        IList<string> PolicyIds { get; }
        IAcl Acl { get; }
    }

    public interface IChangeEvents
    {
        string LatestChangeLogToken { get; }
        IList<IChangeEvent> ChangeEventList { get; }
        bool? HasMoreItems { get; }
        long? TotalNumItems { get; }
    }
}
