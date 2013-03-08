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

namespace DotCMIS
{
    public static class SessionParameter
    {
        // ---- general parameter ----
        public const string User = "org.apache.chemistry.dotcmis.user";
        public const string Password = "org.apache.chemistry.dotcmis.password";

        // ---- provider parameter ----
        // Predefined binding types
        public const string BindingType = "org.apache.chemistry.dotcmis.binding.spi.type";

        // Class name of the binding class.
        public const string BindingSpiClass = "org.apache.chemistry.dotcmis.binding.spi.classname";

        // URL of the AtomPub service document.
        public const string AtomPubUrl = "org.apache.chemistry.dotcmis.binding.atompub.url";

        // WSDL URLs for Web Services.
        public const string WebServicesRepositoryService = "org.apache.chemistry.dotcmis.binding.webservices.RepositoryService";
        public const string WebServicesNavigationService = "org.apache.chemistry.dotcmis.binding.webservices.NavigationService";
        public const string WebServicesObjectService = "org.apache.chemistry.dotcmis.binding.webservices.ObjectService";
        public const string WebServicesVersioningService = "org.apache.chemistry.dotcmis.binding.webservices.VersioningService";
        public const string WebServicesDiscoveryService = "org.apache.chemistry.dotcmis.binding.webservices.DiscoveryService";
        public const string WebServicesRelationshipService = "org.apache.chemistry.dotcmis.binding.webservices.RelationshipService";
        public const string WebServicesMultifilingService = "org.apache.chemistry.dotcmis.binding.webservices.MultiFilingService";
        public const string WebServicesPolicyService = "org.apache.chemistry.dotcmis.binding.webservices.PolicyService";
        public const string WebServicesAclService = "org.apache.chemistry.dotcmis.binding.webservices.ACLService";

        public const string WebServicesWCFBinding = "org.apache.chemistry.dotcmis.binding.webservices.wcfbinding";
        public const string WebServicesOpenTimeout = "org.apache.chemistry.dotcmis.binding.webservices.opentimeout";
        public const string WebServicesCloseTimeout = "org.apache.chemistry.dotcmis.binding.webservices.closetimeout";
        public const string WebServicesSendTimeout = "org.apache.chemistry.dotcmis.binding.webservices.sendtimeout";
        public const string WebServicesReceiveTimeout = "org.apache.chemistry.dotcmis.binding.webservices.receivetimeout";

        public const string WebServicesEnableUnsecuredResponse = "org.apache.chemistry.dotcmis.binding.webservices.enableUnsecuredResponse"; // requires hotfix 971493 or the .NET framework 4 

        // authentication provider
        public const string AuthenticationProviderClass = "org.apache.chemistry.dotcmis.binding.auth.classname";

        // compression flag
        public const string Compression = "org.apache.chemistry.dotcmis.binding.compression";

        // timeouts
        public const string ConnectTimeout = "org.apache.chemistry.dotcmis.binding.connecttimeout";
        public const string ReadTimeout = "org.apache.chemistry.dotcmis.binding.readtimeout";

        // binding caches
        public const string CacheSizeRepositories = "org.apache.chemistry.dotcmis.binding.cache.repositories.size";
        public const string CacheSizeTypes = "org.apache.chemistry.dotcmis.binding.cache.types.size";
        public const string CacheSizeLinks = "org.apache.chemistry.dotcmis.binding.cache.links.size";

        // message size
        public const string MessageSize = "org.apache.chemistry.dotcmis.binding.message.size";

        // session parameter
        public const string ObjectFactoryClass = "org.apache.chemistry.dotcmis.objectfactory.classname";
        public const string CacheClass = "org.apache.chemistry.dotcmis.cache.classname";
        public const string RepositoryId = "org.apache.chemistry.dotcmis.session.repository.id";

        public const string CacheSizeObjects = "org.apache.chemistry.dotcmis.cache.objects.size";
        public const string CacheTTLObjects = "org.apache.chemistry.dotcmis.cache.objects.ttl";
        public const string CacheSizePathToId = "org.apache.chemistry.dotcmis.cache.pathtoid.size";
        public const string CacheTTLPathToId = "org.apache.chemistry.dotcmis.cache.pathtoid.ttl";
        public const string CachePathOmit = "org.apache.chemistry.dotcmis.cache.path.omit";
    }

    public static class BindingType
    {
        public const string AtomPub = "atompub";
        public const string WebServices = "webservices";
        public const string Custom = "custom";
    }

    public static class PropertyIds
    {
        // ---- base ----
        public const string Name = "cmis:name";
        public const string ObjectId = "cmis:objectId";
        public const string ObjectTypeId = "cmis:objectTypeId";
        public const string BaseTypeId = "cmis:baseTypeId";
        public const string CreatedBy = "cmis:createdBy";
        public const string CreationDate = "cmis:creationDate";
        public const string LastModifiedBy = "cmis:lastModifiedBy";
        public const string LastModificationDate = "cmis:lastModificationDate";
        public const string ChangeToken = "cmis:changeToken";

        // ---- document ----
        public const string IsImmutable = "cmis:isImmutable";
        public const string IsLatestVersion = "cmis:isLatestVersion";
        public const string IsMajorVersion = "cmis:isMajorVersion";
        public const string IsLatestMajorVersion = "cmis:isLatestMajorVersion";
        public const string VersionLabel = "cmis:versionLabel";
        public const string VersionSeriesId = "cmis:versionSeriesId";
        public const string IsVersionSeriesCheckedOut = "cmis:isVersionSeriesCheckedOut";
        public const string VersionSeriesCheckedOutBy = "cmis:versionSeriesCheckedOutBy";
        public const string VersionSeriesCheckedOutId = "cmis:versionSeriesCheckedOutId";
        public const string CheckinComment = "cmis:checkinComment";
        public const string ContentStreamLength = "cmis:contentStreamLength";
        public const string ContentStreamMimeType = "cmis:contentStreamMimeType";
        public const string ContentStreamFileName = "cmis:contentStreamFileName";
        public const string ContentStreamId = "cmis:contentStreamId";

        // ---- folder ----
        public const string ParentId = "cmis:parentId";
        public const string AllowedChildObjectTypeIds = "cmis:allowedChildObjectTypeIds";
        public const string Path = "cmis:path";

        // ---- relationship ----
        public const string SourceId = "cmis:sourceId";
        public const string TargetId = "cmis:targetId";

        // ---- policy ----
        public const string PolicyText = "cmis:policyText";
    }

    public static class BasicPermissions
    {
        public const string Read = "cmis:read";
        public const string Write = "cmis:write";
        public const string All = "cmis:all";
    }

    public static class PermissionMappingKeys
    {
        public const string CanGetDescendentsFolder = "canGetDescendents.Folder";
        public const string CanGetChildrenFolder = "canGetChildren.Folder";
        public const string CanGetParentsFolder = "canGetParents.Folder";
        public const string CanGetFolderParentObject = "canGetFolderParent.Object";
        public const string CanCreateDocumentFolder = "canCreateDocument.Folder";
        public const string CanCreateFolderFolder = "canCreateFolder.Folder";
        public const string CanCreateRelationshipSource = "canCreateRelationship.Source";
        public const string CanCreateRelationshipTarget = "canCreateRelationship.Target";
        public const string CanGetPropertiesObject = "canGetProperties.Object";
        public const string CanViewContentObject = "canViewContent.Object";
        public const string CanUpdatePropertiesObject = "canUpdateProperties.Object";
        public const string CanMoveObject = "canMove.Object";
        public const string CanMoveTarget = "canMove.Target";
        public const string CanMoveSource = "canMove.Source";
        public const string CanDeleteObject = "canDelete.Object";
        public const string CanDeleteTreeFolder = "canDeleteTree.Folder";
        public const string CanSetContentDocument = "canSetContent.Document";
        public const string CanDeleteContentDocument = "canDeleteContent.Document";
        public const string CanAddToFolderObject = "canAddToFolder.Object";
        public const string CanAddToFolderFolder = "canAddToFolder.Folder";
        public const string CanRemoveFromFolderObject = "canRemoveFromFolder.Object";
        public const string CanRemoveFromFolderFolder = "canRemoveFromFolder.Folder";
        public const string CanCheckoutDocument = "canCheckout.Document";
        public const string CanCancelCheckoutDocument = "canCancelCheckout.Document";
        public const string CanCheckinDocument = "canCheckin.Document";
        public const string CanGetAllVersionsVersionSeries = "canGetAllVersions.VersionSeries";
        public const string CanGetObjectRelationshipSObject = "canGetObjectRelationships.Object";
        public const string CanAddPolicyObject = "canAddPolicy.Object";
        public const string CanAddPolicyPolicy = "canAddPolicy.Policy";
        public const string CanRemovePolicyObject = "canRemovePolicy.Object";
        public const string CanRemovePolicyPolicy = "canRemovePolicy.Policy";
        public const string CanGetAppliesPoliciesObject = "canGetAppliedPolicies.Object";
        public const string CanGetAclObject = "canGetAcl.Object";
        public const string CanApplyAclObject = "canApplyAcl.Object";
    }

    public static class Actions
    {
        public const string CanDeleteObject = "canDeleteObject";
        public const string CanUpdateProperties = "canUpdateProperties";
        public const string CanGetProperties = "canGetProperties";
        public const string CanGetObjectRelationships = "canGetObjectRelationships";
        public const string CanGetObjectParents = "canGetObjectParents";
        public const string CanGetFolderParent = "canGetFolderParent";
        public const string CanGetFolderTree = "canGetFolderTree";
        public const string CanGetDescendants = "canGetDescendants";
        public const string CanMoveObject = "canMoveObject";
        public const string CanDeleteContentStream = "canDeleteContentStream";
        public const string CanCheckOut = "canCheckOut";
        public const string CanCancelCheckOut = "canCancelCheckOut";
        public const string CanCheckIn = "canCheckIn";
        public const string CanSetContentStream = "canSetContentStream";
        public const string CanGetAllVersions = "canGetAllVersions";
        public const string CanAddObjectToFolder = "canAddObjectToFolder";
        public const string CanRemoveObjectFromFolder = "canRemoveObjectFromFolder";
        public const string CanGetContentStream = "canGetContentStream";
        public const string CanApplyPolicy = "canApplyPolicy";
        public const string CanGetAppliedPolicies = "canGetAppliedPolicies";
        public const string CanRemovePolicy = "canRemovePolicy";
        public const string CanGetChildren = "canGetChildren";
        public const string CanCreateDocument = "canCreateDocument";
        public const string CanCreateFolder = "canCreateFolder";
        public const string CanCreateRelationship = "canCreateRelationship";
        public const string CanDeleteTree = "canDeleteTree";
        public const string CanGetRenditions = "canGetRenditions";
        public const string CanGetAcl = "canGetACL";
        public const string CanApplyAcl = "canApplyACL";
    }

    internal static class AtomPubConstants
    {
        // namespaces
        public const string NamespaceCMIS = "http://docs.oasis-open.org/ns/cmis/core/200908/";
        public const string NamespaceAtom = "http://www.w3.org/2005/Atom";
        public const string NamespaceAPP = "http://www.w3.org/2007/app";
        public const string NamespaceRestAtom = "http://docs.oasis-open.org/ns/cmis/restatom/200908/";
        public const string NamespaceXSI = "http://www.w3.org/2001/XMLSchema-instance";
        public const string NamespaceApacheChemistry = "http://chemistry.apache.org/";

        // media types
        public const string MediatypeService = "application/atomsvc+xml";
        public const string MediatypeFeed = "application/atom+xml;type=feed";
        public const string MediatypeEntry = "application/atom+xml;type=entry";
        public const string MediatypeChildren = MediatypeFeed;
        public const string MediatypeDescendants = "application/cmistree+xml";
        public const string MediatypeQuery = "application/cmisquery+xml";
        public const string MediatypeAllowableAction = "application/cmisallowableactions+xml";
        public const string MediatypeACL = "application/cmisacl+xml";
        public const string MediatypeCMISAtom = "application/cmisatom+xml";
        public const string MediatypeOctetStream = "application/octet-stream";

        // collections
        public const string CollectionRoot = "root";
        public const string CollectionTypes = "types";
        public const string CollectionQuery = "query";
        public const string CollectionCheckedout = "checkedout";
        public const string CollectionUnfiled = "unfiled";

        // URI templates
        public const string TemplateObjectById = "objectbyid";
        public const string TemplateObjectByPath = "objectbypath";
        public const string TemplateTypeById = "typebyid";
        public const string TemplateQuery = "query";

        // Link rel
        public const string RelSelf = "self";
        public const string RelEnclosure = "enclosure";
        public const string RelService = "service";
        public const string RelDescribedBy = "describedby";
        public const string RelAlternate = "alternate";
        public const string RelDown = "down";
        public const string RelUp = "up";
        public const string RelFirst = "first";
        public const string RelLast = "last";
        public const string RelPrev = "previous";
        public const string RelNext = "next";
        public const string RelVia = "via";
        public const string RelEdit = "edit";
        public const string RelEditMedia = "edit-media";
        public const string RelVersionHistory = "version-history";
        public const string RelCurrentVersion = "current-version";
        public const string RelWorkingCopy = "working-copy";
        public const string RelFolderTree = "http://docs.oasis-open.org/ns/cmis/link/200908/foldertree";
        public const string RelAllowableActions = "http://docs.oasis-open.org/ns/cmis/link/200908/allowableactions";
        public const string RelACL = "http://docs.oasis-open.org/ns/cmis/link/200908/acl";
        public const string RelSource = "http://docs.oasis-open.org/ns/cmis/link/200908/source";
        public const string RelTarget = "http://docs.oasis-open.org/ns/cmis/link/200908/target";
        public const string RelRelationships = "http://docs.oasis-open.org/ns/cmis/link/200908/relationships";
        public const string RelPolicies = "http://docs.oasis-open.org/ns/cmis/link/200908/policies";

        public const string RepRelTypeDesc = "http://docs.oasis-open.org/ns/cmis/link/200908/typedescendants";
        public const string RepRelFolderTree = "http://docs.oasis-open.org/ns/cmis/link/200908/foldertree";
        public const string RepRelRootDesc = "http://docs.oasis-open.org/ns/cmis/link/200908/rootdescendants";
        public const string RepRelChanges = "http://docs.oasis-open.org/ns/cmis/link/200908/changes";

        // parameter
        public const string ParamACL = "includeACL";
        public const string ParamAllowableActions = "includeAllowableActions";
        public const string ParamAllVersions = "allVersions";
        public const string ParamChangeLogToken = "changeLogToken";
        public const string ParamChangeToken = "changeToken";
        public const string ParamCheckinComment = "checkinComment";
        public const string ParamCheckIn = "checkin";
        public const string ParamChildTypes = "childTypes";
        public const string ParamContinueOnFailure = "continueOnFailure";
        public const string ParamDepth = "depth";
        public const string ParamFilter = "filter";
        public const string ParamFolderId = "folderId";
        public const string ParamId = "id";
        public const string ParamMajor = "major";
        public const string ParamMaxItems = "maxItems";
        public const string ParamObjectId = "objectId";
        public const string ParamOnlyBasicPermissions = "onlyBasicPermissions";
        public const string ParamOrderBy = "orderBy";
        public const string ParamOverwriteFlag = "overwriteFlag";
        public const string ParamPath = "path";
        public const string ParamPathSegment = "includePathSegment";
        public const string ParamPolicyId = "policyId";
        public const string ParamPolicyIds = "includePolicyIds";
        public const string ParamProperties = "includeProperties";
        public const string ParamPropertyDefinitions = "includePropertyDefinitions";
        public const string ParamRelationships = "includeRelationships";
        public const string ParamRelationshipDirection = "relationshipDirection";
        public const string ParamRelativePathSegment = "includeRelativePathSegment";
        public const string ParamRemoveFrom = "removeFrom";
        public const string ParamRenditionFilter = "renditionFilter";
        public const string ParamRepositoryId = "repositoryId";
        public const string ParamReturnVersion = "returnVersion";
        public const string ParamSkipCount = "skipCount";
        public const string ParamSourceFolderId = "sourceFolderId";
        public const string ParamStreamId = "streamId";
        public const string ParamSubRelationshipTypes = "includeSubRelationshipTypes";
        public const string ParamTypeId = "typeId";
        public const string ParamUnfildeObjects = "unfileObjects";
        public const string ParamVersioningState = "versioningState";
        public const string ParamQ = "q";
        public const string ParamSearchAllVersions = "searchAllVersions";
        public const string ParamACLPropagation = "ACLPropagation";

        // rendition filter
        public const string RenditionNone = "cmis:none";

        // service doc
        public const string TagService = "service";
        public const string TagWorkspace = "workspace";
        public const string TagRepositoryInfo = "repositoryInfo";
        public const string TagCollection = "collection";
        public const string TagCollectionType = "collectionType";
        public const string TagUriTemplate = "uritemplate";
        public const string TagTemplateTemplate = "template";
        public const string TagTemplateType = "type";
        public const string TagLink = "link";

        // atom
        public const string TagAtomId = "id";
        public const string TagAtomTitle = "title";
        public const string TagAtomUpdated = "updated";

        // feed
        public const string TagFeed = "feed";

        // entry
        public const string TagEntry = "entry";
        public const string TagObject = "object";
        public const string TagNumItems = "numItems";
        public const string TagPathSegment = "pathSegment";
        public const string TagRelativePathSegment = "relativePathSegment";
        public const string TagType = "type";
        public const string TagChildren = "children";
        public const string TagContent = "content";
        public const string TagContentMediatype = "mediatype";
        public const string TagContentBase64 = "base64";
        public const string TagContentFilename = "filename";

        // allowable actions
        public const string TagAllowableActions = "allowableActions";

        // ACL
        public const string TagACL = "acl";

        // query
        public const string TagQuery = "query";
        public const string TagStatement = "statement";
        public const string TagSearchAllVersions = "searchAllVersions";
        public const string TagIncludeAllowableActions = "includeAllowableActions";
        public const string TagRenditionFilter = "renditionFilter";
        public const string TagIncludeRelationships = "includeRelationships";
        public const string TagMaxItems = "maxItems";
        public const string TagSkipCount = "skipCount";

        // links
        public const string LinkRel = "rel";
        public const string LinkHref = "href";
        public const string LinkType = "type";
        public const string ContentSrc = "src";
        public const string LinkRelContent = "@@content@@";
    }
}
