using System;
using System.Collections.Generic;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Client;
using DotCMIS.Data.Impl;
using DotCMIS.Data.Extensions;
namespace tests.sln
{
    class MainClass
    {
        public static void Main (string[] args)
        {
            // Connect to repository
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters[SessionParameter.BindingType] = BindingType.AtomPub;
            parameters[SessionParameter.AtomPubUrl] = "http://localhost:8080/alfresco/service/cmis";
            parameters[SessionParameter.User] = "admin";
            parameters[SessionParameter.Password] = "admin";
            SessionFactory factory = SessionFactory.NewInstance();
            ISession session = factory.GetRepositories(parameters)[0].CreateSession();
            Console.WriteLine("Created CMIS session: " + session.ToString());

            // Get the root folder
            IFolder rootFolder = session.GetRootFolder(); // Error happens here
        }
    }
}
