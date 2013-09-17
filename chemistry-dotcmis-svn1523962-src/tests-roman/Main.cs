using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;
using DotCMIS.Data.Impl;
using DotCMIS.Data.Extensions;

namespace testsroman
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			ConnectToCMIS();
		}
		
		public static void ConnectToCMIS() {
			// Connect to repository
            Dictionary<string, string> parameters = new Dictionary<string, string>();
			parameters[SessionParameter.BindingType] = BindingType.AtomPub;
			//parameters[SessionParameter.AtomPubUrl] = "http://localhost:8080/alfresco/cmisatom";
			//parameters[SessionParameter.User] = "admin";
			//parameters[SessionParameter.Password] = "admin";
			parameters[SessionParameter.AtomPubUrl] = "http://avenue.aegif.jp/alfresco/service/cmis";
			parameters[SessionParameter.User] = "nicolas.raoul";
			parameters[SessionParameter.Password] = "eR31g6HG";
			SessionFactory factory = SessionFactory.NewInstance();
			ISession session = factory.GetRepositories(parameters)[0].CreateSession();
			Console.WriteLine("Created CMIS session: " + session.ToString());
			
			// Get the root folder
			/*IFolder rootFolder =*/ session.GetRootFolder(); // Error happens here
		}
	}
}