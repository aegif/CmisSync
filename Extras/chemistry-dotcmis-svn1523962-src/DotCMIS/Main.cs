using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

using DotCMIS.CMISWebServicesReference;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;
using DotCMIS.Data.Impl;
using DotCMIS.Data.Extensions;

namespace DotCMIS.MainClass
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			//Parse();
			ConnectToCMIS();
		}
		
		public static void ConnectToCMIS() {
			// Connect to repository
            Dictionary<string, string> parameters = new Dictionary<string, string>();
			parameters[SessionParameter.BindingType] = BindingType.AtomPub;
			parameters[SessionParameter.AtomPubUrl] = "http://localhost:8080/alfresco/cmisatom";
			parameters[SessionParameter.User] = "admin";
			parameters[SessionParameter.Password] = "admin";
			//parameters[SessionParameter.AtomPubUrl] = "http://58.156.2.18:8080/alfresco/service/cmis";
			//parameters[SessionParameter.User] = "nicolas.raoul";
			//parameters[SessionParameter.Password] = "eR31g6HG";
			SessionFactory factory = SessionFactory.NewInstance();
			ISession session = factory.GetRepositories(parameters)[0].CreateSession();
			Console.WriteLine("Created CMIS session: " + session.ToString());
			
			// Get the root folder
			/*IFolder rootFolder =*/ session.GetRootFolder(); // Error happens here
		}
		
		private static XmlSerializer ObjectSerializer;
		
		public static void Parse()
        {
			XmlRootAttribute objectXmlRoot = new XmlRootAttribute("object");
            objectXmlRoot.Namespace = "http://docs.oasis-open.org/ns/cmis/restatom/200908/";
            ObjectSerializer = new XmlSerializer(typeof(cmisObjectType), objectXmlRoot);
			
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;

            XmlReader reader = XmlReader.Create("/tmp/cmis.xml", settings);
            try
            {
                while (true)
                {
                    if (reader.IsStartElement())
                    {
                        if ("http://www.w3.org/2005/Atom" == reader.NamespaceURI)
                        {
                            /*if ("feed" == reader.LocalName)
                            {
                                parseResult = ParseFeed(reader);
                                break;
                            }
                            else*/ if ("entry" == reader.LocalName)
                            {
                                ParseEntry(reader);
                                break;
                            }
                        }
                        /*else if ("http://docs.oasis-open.org/ns/cmis/core/200908/" == reader.NamespaceURI)
                        {
                            if ("allowableActions" == reader.LocalName)
                            {
                                parseResult = ParseAllowableActions(reader);
                                break;
                            }
                            else if ("acl" == reader.LocalName)
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
                        }*/
                    }

                    if (!reader.Read()) { break; }
                }
            }
            finally
            {
                try { reader.Close(); }
                catch (Exception) { }
            }
        }
		
		public static void ParseEntry(XmlReader reader)
        {
            //AtomEntry entry = new AtomEntry();

            reader.Read();
            while (true)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    ParseElement(reader);
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
        }
		
		private static void ParseElement(XmlReader reader)
        {
            if ("http://docs.oasis-open.org/ns/cmis/restatom/200908/" == reader.NamespaceURI)
            {
                if ("object" == reader.LocalName)
                {
                    DeserializeObject(reader);
                }
            }

            skip(reader);
        }

		private static void DeserializeObject(XmlReader reader)
        {
            ObjectSerializer.Deserialize(reader);
        }
		
		private static void skip(XmlReader reader)
        {
            if (!reader.IsEmptyElement)
            {
                int level = 1;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        level++;
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
}