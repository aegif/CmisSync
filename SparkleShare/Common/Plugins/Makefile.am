dist_plugins_in_files = \
    own-server.xml.in

dist_plugins_DATA = \
    $(dist_plugins_in_files:.xml.in=.xml) \
    own-server.png

#@INTLTOOL_XML_RULE@

pluginsdir = $(pkgdatadir)/plugins/

EXTRA_DIST=$(dist_plugins_in_files) $(xml_DATA)

MAINTAINERCLEANFILES = \
	Makefile.in
