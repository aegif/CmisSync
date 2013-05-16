dist_plugins_in_files = \
	filenet.xml.in \
	alfresco.xml.in \
    own-server.xml.in

dist_plugins_DATA = \
    $(dist_plugins_in_files:.xml.in=.xml) \
	filenet.png \
	alfresco.png \
    own-server.png

#@INTLTOOL_XML_RULE@

pluginsdir = $(pkgdatadir)/plugins/

EXTRA_DIST=$(dist_plugins_in_files) $(xml_DATA)

MAINTAINERCLEANFILES = \
	Makefile.in
