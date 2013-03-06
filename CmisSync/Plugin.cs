//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons (hylkebons@gmail.com)
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see (http://www.gnu.org/licenses/).


using System;
using System.Xml;

using IO = System.IO;

namespace CmisSync {

    public class Plugin {

        public static string PluginsPath = "";

        public static string LocalPluginsPath = new string [] {
            Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "CmisSync", "plugins" }.Combine ();

        public string Name { get { return GetValue ("info", "name"); } }
        public string Description { get { return GetValue ("info", "description"); } }
        public string Backend { get { return GetValue ("info", "backend"); } }
        public string Fingerprint { get { return GetValue ("info", "fingerprint"); } }
        public string AnnouncementsUrl { get { return GetValue ("info", "announcements_url"); } }
        public string Address { get { return GetValue ("address", "value"); } }
        public string AddressExample { get { return GetValue ("address", "example"); } }
        public string Repository { get { return GetValue("repository", "value"); } }
        public string RepositoryExample { get { return GetValue("repository", "example"); } }
        public string Path { get { return GetValue("path", "value"); } }
        public string PathExample { get { return GetValue ("path", "example"); } }
        public string User { get { return GetValue("user", "value"); } }
        public string UserExample { get { return GetValue("user", "example"); } }
        public string Password { get { return GetValue("password", "value"); } }
        public string PasswordExample { get { return GetValue("password", "example"); } }

        public string ImagePath {
            get {
                string image_file_name = GetValue ("info", "icon");
                string image_path      = IO.Path.Combine (this.plugin_directory, image_file_name);

                if (IO.File.Exists (image_path))
                    return image_path;
                else
                    return IO.Path.Combine (PluginsPath, image_file_name);
            }
        }
		
        public bool PathUsesLowerCase {
            get {
                string uses_lower_case = GetValue ("path", "uses_lower_case");
                
                if (!string.IsNullOrEmpty (uses_lower_case))
                    return uses_lower_case.Equals (bool.TrueString);
                else
                    return false;
            }
        }


        private XmlDocument xml = new XmlDocument ();
        private string plugin_directory;

        public Plugin (string plugin_path)
        {
            this.plugin_directory = System.IO.Path.GetDirectoryName (plugin_path);
            this.xml.Load (plugin_path);
        }


        public static Plugin Create (string name, string description, string address_value,
            string address_example, string repository_value, string repository_example,
            string path_value, string path_example, string user_value, string user_example,
            string password_value, string password_example)
        {
            string plugin_path = System.IO.Path.Combine (LocalPluginsPath, name + ".xml");

            if (IO.File.Exists (plugin_path))
                return null;

            string plugin_xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<CmisSync>" +
                "  <plugin>" +
                "    <info>" +
                "        <name>" + name + "</name>" +
                "        <description>" + description + "</description>" +
                "        <icon>own-server.png</icon>" +
                "    </info>" +
                "    <address>" +
                "      <value>" + address_value + "</value>" +
                "      <example>" + address_example + "</example>" +
                "    </address>" +
                "    <repository>" +
                "      <value>" + repository_value + "</value>" +
                "      <example>" + repository_example + "</example>" +
                "    </repository>" +
                "    <path>" +
                "      <value>" + path_value + "</value>" +
                "      <example>" + path_example + "</example>" +
                "    </path>" +
                "    <user>" +
                "      <value>" + user_value + "</value>" +
                "      <example>" + user_example + "</example>" +
                "    </user>" +
                "    <password>" +
                "      <value>" + password_value + "</value>" +
                "      <example>" + password_example + "</example>" +
                "    </password>" +
                "  </plugin>" +
                "</CmisSync>";

            plugin_xml = plugin_xml.Replace ("<value></value>", "<value/>");
            plugin_xml = plugin_xml.Replace ("<example></example>", "<example/>");

            if (!IO.Directory.Exists (LocalPluginsPath))
                IO.Directory.CreateDirectory (LocalPluginsPath);

            IO.File.WriteAllText (plugin_path, plugin_xml);

            return new Plugin (plugin_path);
        }


        private string GetValue (string a, string b)
        {
            XmlNode node = this.xml.SelectSingleNode ("/CmisSync/plugin/" + a + "/" + b + "/text()");

            if (node != null && !string.IsNullOrEmpty (node.Value))
                return node.Value;
            else
                return null;
        }
    }
}