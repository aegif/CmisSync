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
using System.Xml;
using System.Xml.Serialization;
using DotCMIS.CMISWebServicesReference;

namespace DotCMIS.Binding.AtomPub
{
    internal class AtomPubParser
    {
        private static XmlSerializer RepositoryInfoSerializer;
        private static XmlSerializer TypeDefintionSerializer;
        private static XmlSerializer ObjectSerializer;
        private static XmlSerializer AllowableActionsSerializer;
        private static XmlSerializer AclSerializer;

        static AtomPubParser()
        {
            XmlRootAttribute repositoryInfoXmlRoot = new XmlRootAttribute("repositoryInfo");
            repositoryInfoXmlRoot.Namespace = AtomPubConstants.NamespaceRestAtom;
            RepositoryInfoSerializer = new XmlSerializer(typeof(cmisRepositoryInfoType), repositoryInfoXmlRoot);

            XmlRootAttribute typeDefinitionXmlRoot = new XmlRootAttribute("type");
            typeDefinitionXmlRoot.Namespace = AtomPubConstants.NamespaceRestAtom;
            TypeDefintionSerializer = new XmlSerializer(typeof(cmisTypeDefinitionType), typeDefinitionXmlRoot);

            XmlRootAttribute objectXmlRoot = new XmlRootAttribute("object");
            objectXmlRoot.Namespace = AtomPubConstants.NamespaceRestAtom;
            ObjectSerializer = new XmlSerializer(typeof(cmisObjectType), objectXmlRoot);

            XmlRootAttribute allowableActionsXmlRoot = new XmlRootAttribute("allowableActions");
            allowableActionsXmlRoot.Namespace = AtomPubConstants.NamespaceCMIS;
            AllowableActionsSerializer = new XmlSerializer(typeof(cmisAllowableActionsType), allowableActionsXmlRoot);

            XmlRootAttribute aclXmlRoot = new XmlRootAttribute("acl");
            aclXmlRoot.Namespace = AtomPubConstants.NamespaceCMIS;
            AclSerializer = new XmlSerializer(typeof(cmisAccessControlListType), aclXmlRoot);
        }

        private Stream stream;
        private AtomBase parseResult;

        public AtomPubParser(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            this.stream = stream;
        }

        public AtomBase GetParseResults()
        {
            return parseResult;
        }

        public void Parse()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;

            try {
                using (XmlReader reader = XmlReader.Create(stream, settings))
                {
                    while (true)
                    {
                        if (reader.IsStartElement())
                        {
                            if (AtomPubConstants.NamespaceAtom == reader.NamespaceURI)
                            {
                                if (AtomPubConstants.TagFeed == reader.LocalName)
                                {
                                    parseResult = ParseFeed(reader);
                                    break;
                                }
                                else if (AtomPubConstants.TagEntry == reader.LocalName)
                                {
                                    parseResult = ParseEntry(reader);
                                    break;
                                }
                            }
                            else if (AtomPubConstants.NamespaceCMIS == reader.NamespaceURI)
                            {
                                if (AtomPubConstants.TagAllowableActions == reader.LocalName)
                                {
                                    parseResult = ParseAllowableActions(reader);
                                    break;
                                }
                                else if (AtomPubConstants.TagACL == reader.LocalName)
                                {
                                    parseResult = ParseACL(reader);
                                    break;
                                }
                            }
                            else if (AtomPubConstants.NamespaceAPP == reader.NamespaceURI)
                            {
                                if (AtomPubConstants.TagService == reader.LocalName)
                                {
                                    parseResult = ParseServiceDoc(reader);
                                    break;
                                }
                            }
                        }

                        if (!reader.Read()) { break; }
                    }
                }
            }
            finally
            {
                try { stream.Close(); }
                catch (Exception) { }
            }
        }

        private ServiceDoc ParseServiceDoc(XmlReader reader)
        {
            ServiceDoc result = new ServiceDoc();

            reader.Read();
            while (true)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (AtomPubConstants.NamespaceAPP == reader.NamespaceURI)
                    {
                        if (AtomPubConstants.TagWorkspace == reader.LocalName)
                        {
                            result.AddWorkspace(ParseWorkspace(reader));
                        }
                        else
                        {
                            Skip(reader);
                        }
                    }
                    else
                    {
                        Skip(reader);
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }
                else
                {
                    if (!reader.Read()) { break; }
                }
            }

            return result;
        }

        private RepositoryWorkspace ParseWorkspace(XmlReader reader)
        {
            RepositoryWorkspace workspace = new RepositoryWorkspace();

            reader.Read();
            while (true)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    AtomElement element = ParseWorkspaceElement(reader);

                    if (element != null && element.Object is cmisRepositoryInfoType)
                    {
                        workspace.Id = ((cmisRepositoryInfoType)element.Object).repositoryId;
                    }

                    workspace.AddElement(element);
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }
                else
                {
                    if (!reader.Read()) { break; }
                }
            }

            reader.Read();

            return workspace;
        }

        private AtomElement ParseWorkspaceElement(XmlReader reader)
        {
            if (AtomPubConstants.NamespaceRestAtom == reader.NamespaceURI)
            {
                if (AtomPubConstants.TagRepositoryInfo == reader.LocalName)
                {
                    return DeserializeRepositoryInfo(reader);
                }
                else if (AtomPubConstants.TagUriTemplate == reader.LocalName)
                {
                    return ParseTemplate(reader);
                }
            }
            else if (AtomPubConstants.NamespaceAtom == reader.NamespaceURI)
            {
                if (AtomPubConstants.TagLink == reader.LocalName)
                {
                    return ParseLink(reader);
                }
            }
            else if (AtomPubConstants.NamespaceAPP == reader.NamespaceURI)
            {
                if (AtomPubConstants.TagCollection == reader.LocalName)
                {
                    return ParseCollection(reader);
                }
            }

            Skip(reader);

            return null;
        }

        private AtomElement ParseTemplate(XmlReader reader)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string ns = reader.NamespaceURI;
            string ln = reader.LocalName;

            reader.Read();
            while (true)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (AtomPubConstants.NamespaceRestAtom == reader.NamespaceURI)
                    {
                        if (AtomPubConstants.TagTemplateTemplate == reader.LocalName)
                        {
                            result["template"] = ReadText(reader);
                        }
                        else if (AtomPubConstants.TagTemplateType == reader.LocalName)
                        {
                            result["type"] = ReadText(reader);
                        }
                        else
                        {
                            Skip(reader);
                        }
                    }
                    else
                    {
                        Skip(reader);
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }
                else
                {
                    if (!reader.Read()) { break; }
                }
            }

            reader.Read();

            return new AtomElement(ns, ln, result);
        }

        private AtomElement ParseCollection(XmlReader reader)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string ns = reader.NamespaceURI;
            string ln = reader.LocalName;

            if (reader.MoveToAttribute("href"))
            {
                result["href"] = reader.Value;
                reader.MoveToElement();
            }

            reader.Read();
            while (true)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (AtomPubConstants.NamespaceRestAtom == reader.NamespaceURI && AtomPubConstants.TagCollectionType == reader.LocalName)
                    {
                        result["collectionType"] = ReadText(reader);
                    }
                    else
                    {
                        Skip(reader);
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }
                else
                {
                    if (!reader.Read()) { break; }
                }
            }

            reader.Read();

            return new AtomElement(ns, ln, result);
        }

        private AtomElement ParseLink(XmlReader reader)
        {
            AtomLink result = new AtomLink();
            string ns = reader.NamespaceURI;
            string ln = reader.LocalName;

            if (reader.HasAttributes)
            {
                while (reader.MoveToNextAttribute())
                {
                    if (AtomPubConstants.LinkRel == reader.Name)
                    {
                        result.Rel = reader.Value;
                    }
                    else if (AtomPubConstants.LinkHref == reader.Name)
                    {
                        result.Href = reader.Value;
                    }
                    else if (AtomPubConstants.LinkType == reader.Name)
                    {
                        result.Type = reader.Value;
                    }
                }
                reader.MoveToElement();
            }

            Skip(reader);

            return new AtomElement(ns, ln, result);
        }

        private AtomFeed ParseFeed(XmlReader reader)
        {
            AtomFeed feed = new AtomFeed();

            reader.Read();
            while (true)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (AtomPubConstants.NamespaceAtom == reader.NamespaceURI)
                    {
                        if (AtomPubConstants.TagLink == reader.LocalName)
                        {
                            feed.AddElement(ParseLink(reader));
                        }
                        else if (AtomPubConstants.TagEntry == reader.LocalName)
                        {
                            feed.AddEntry(ParseEntry(reader));
                        }
                        else
                        {
                            Skip(reader);
                        }
                    }
                    else if (AtomPubConstants.NamespaceRestAtom == reader.NamespaceURI)
                    {
                        if (AtomPubConstants.TagNumItems == reader.LocalName)
                        {
                            feed.AddElement(ParseLong(reader));
                        }
                        else
                        {
                            Skip(reader);
                        }
                    }
                    else
                    {
                        Skip(reader);
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }
                else
                {
                    if (!reader.Read()) { break; }
                }
            }

            reader.Read();

            return feed;
        }

        private AtomEntry ParseEntry(XmlReader reader)
        {
            AtomEntry entry = new AtomEntry();

            reader.Read();
            while (true)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    AtomElement element = ParseElement(reader);

                    if (element != null)
                    {
                        entry.AddElement(element);

                        if (element.Object is cmisObjectType && ((cmisObjectType)element.Object).properties != null)
                        {
                            foreach (cmisProperty prop in ((cmisObjectType)element.Object).properties.Items)
                            {
                                if (PropertyIds.ObjectId == prop.propertyDefinitionId)
                                {
                                    entry.Id = ((cmisPropertyId)prop).value[0];
                                }
                            }
                        }
                        else if (element.Object is cmisTypeDefinitionType)
                        {
                            entry.Id = ((cmisTypeDefinitionType)element.Object).id;
                        }
                    }

                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }
                else
                {
                    if (!reader.Read()) { break; }
                }
            }

            reader.Read();

            return entry;
        }

        private AtomAcl ParseACL(XmlReader reader)
        {
            AtomAcl result = new AtomAcl();
            result.ACL = DeserializeACL(reader);
            return result;
        }

        private AtomAllowableActions ParseAllowableActions(XmlReader reader)
        {
            AtomAllowableActions result = new AtomAllowableActions();
            result.AllowableActions = DeserializeAllowableActions(reader);
            return result;
        }

        private AtomElement ParseElement(XmlReader reader)
        {
            if (AtomPubConstants.NamespaceRestAtom == reader.NamespaceURI)
            {
                if (AtomPubConstants.TagObject == reader.LocalName)
                {
                    return DeserializeObject(reader);
                }
                else if (AtomPubConstants.TagPathSegment == reader.LocalName
                      || AtomPubConstants.TagRelativePathSegment == reader.LocalName)
                {
                    return ParseText(reader);
                }
                else if (AtomPubConstants.TagType == reader.LocalName)
                {
                    return DeserializeTypeDefinition(reader);
                }
                else if (AtomPubConstants.TagChildren == reader.LocalName)
                {
                    return ParseChildren(reader);
                }
            }
            else if (AtomPubConstants.NamespaceAtom == reader.NamespaceURI)
            {
                if (AtomPubConstants.TagLink == reader.LocalName)
                {
                    return ParseLink(reader);
                }
                else if (AtomPubConstants.TagContent == reader.LocalName)
                {
                    return ParseAtomContentSrc(reader);
                }
            }

            Skip(reader);

            return null;
        }

        private AtomElement ParseChildren(XmlReader reader)
        {
            AtomElement result = null;
            string childName = reader.LocalName;
            string childNamespace = reader.NamespaceURI;

            reader.Read();
            while (true)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (AtomPubConstants.NamespaceAtom == reader.NamespaceURI && AtomPubConstants.TagFeed == reader.LocalName)
                    {
                        result = new AtomElement(childNamespace, childName, ParseFeed(reader));
                    }
                    else
                    {
                        Skip(reader);
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }
                else
                {
                    if (!reader.Read()) { break; }
                }
            }

            reader.Read();

            return result;
        }

        private AtomElement ParseAtomContentSrc(XmlReader reader)
        {
            AtomLink result = new AtomLink();
            result.Rel = AtomPubConstants.LinkRelContent;

            if (reader.MoveToAttribute(AtomPubConstants.ContentSrc))
            {
                result.Href = reader.Value;
                reader.MoveToElement();
            }

            Skip(reader);

            return new AtomElement(reader.NamespaceURI, reader.LocalName, result);
        }

        private AtomElement ParseText(XmlReader reader)
        {
            string ns = reader.NamespaceURI;
            string ln = reader.LocalName;
            return new AtomElement(ns, ln, ReadText(reader));
        }

        private AtomElement ParseLong(XmlReader reader)
        {
            string ns = reader.NamespaceURI;
            string ln = reader.LocalName;
            return new AtomElement(ns, ln, Int64.Parse(ReadText(reader)));
        }

        private AtomElement DeserializeRepositoryInfo(XmlReader reader)
        {
            string ns = reader.NamespaceURI;
            string ln = reader.LocalName;
            return new AtomElement(ns, ln, RepositoryInfoSerializer.Deserialize(reader));
        }

        private AtomElement DeserializeTypeDefinition(XmlReader reader)
        {
            string ns = reader.NamespaceURI;
            string ln = reader.LocalName;
            return new AtomElement(ns, ln, TypeDefintionSerializer.Deserialize(reader));
        }

        private AtomElement DeserializeObject(XmlReader reader)
        {
            string ns = reader.NamespaceURI;
            string ln = reader.LocalName;
            return new AtomElement(ns, ln, ObjectSerializer.Deserialize(reader));
        }

        private cmisAccessControlListType DeserializeACL(XmlReader reader)
        {
            return (cmisAccessControlListType)AclSerializer.Deserialize(reader);
        }


        private cmisAllowableActionsType DeserializeAllowableActions(XmlReader reader)
        {
            return (cmisAllowableActionsType)AllowableActionsSerializer.Deserialize(reader);
        }

        private string ReadText(XmlReader reader)
        {
            string text = null;
            if (reader.Read())
            {
                text = reader.ReadContentAsString();
                reader.Read();
            }

            return text;
        }

        private void Skip(XmlReader reader)
        {
            if (!reader.IsEmptyElement)
            {
                int level = 1;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (!reader.IsEmptyElement)
                        {
                            level++;
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        level--;
                        if (level == 0)
                        {
                            break;
                        }
                    }
                }
            }

            reader.Read();
        }
    }

    internal class AtomElement
    {
        public string Namespace { get; private set; }
        public string LocalName { get; private set; }
        public object Object { get; private set; }

        public AtomElement(string elementNamespace, string elementLocalName, object elementObject)
        {
            Namespace = elementNamespace;
            LocalName = elementLocalName;
            Object = elementObject;
        }

        public override string ToString()
        {
            return "{" + Namespace + "}" + LocalName + ": " + Object;
        }
    }

    internal abstract class AtomBase
    {
        public IList<AtomElement> elements = new List<AtomElement>();

        public abstract string GetAtomType();

        public IList<AtomElement> GetElements()
        {
            return elements;
        }

        public void AddElement(AtomElement element)
        {
            if (element != null)
            {
                elements.Add(element);
            }
        }
    }

    internal class ServiceDoc : AtomBase
    {
        private IList<RepositoryWorkspace> workspaces = new List<RepositoryWorkspace>();

        public override string GetAtomType()
        {
            return "Service Document";
        }

        public IList<RepositoryWorkspace> GetWorkspaces()
        {
            return workspaces;
        }

        public void AddWorkspace(RepositoryWorkspace ws)
        {
            if (ws != null)
            {
                workspaces.Add(ws);
            }
        }

        public override string ToString()
        {
            return "Service Doc: " + workspaces;
        }
    }

    internal class RepositoryWorkspace : AtomBase
    {
        public string Id { get; set; }

        public override string GetAtomType()
        {
            return "Repository Workspace";
        }

        public override string ToString()
        {
            return "Workspace \"" + Id + "\": " + GetElements();
        }
    }

    internal class AtomEntry : AtomBase
    {
        public string Id { get; set; }

        public override string GetAtomType()
        {
            return "Atom Entry";
        }

        public override string ToString()
        {
            return "Entry \"" + Id + "\": " + GetElements();
        }
    }

    internal class AtomFeed : AtomBase
    {
        private IList<AtomEntry> entries = new List<AtomEntry>();

        public override string GetAtomType()
        {
            return "Atom Feed";
        }

        public IList<AtomEntry> GetEntries()
        {
            return entries;
        }

        public void AddEntry(AtomEntry entry)
        {
            if (entry != null)
            {
                entries.Add(entry);
            }
        }

        public override string ToString()
        {
            return "Feed : " + GetElements() + " " + entries;
        }
    }

    internal class AtomAcl : AtomBase
    {
        public cmisAccessControlListType ACL { get; set; }

        public override string GetAtomType()
        {
            return "ACL";
        }
    }

    internal class AtomAllowableActions : AtomBase
    {
        public cmisAllowableActionsType AllowableActions { get; set; }

        public override string GetAtomType()
        {
            return "Allowable Actions";
        }
    }

    internal class AtomLink
    {
        public string Rel { get; set; }
        public string Type { get; set; }
        public string Href { get; set; }

        public override string ToString()
        {
            return "Link: rel=\"" + Rel + "\" type=\"" + Type + "\" href=\"" + Href + "\"";
        }
    }
}
