
############
Architecture
############

Overview
--------

.. image:: Architecture.png

Source code
-----------

`The Viking source is available on Github`_

The source tree is divided into two main folders, clients and servers.  Below is a bried description of each project.

Clients
-------

Clients are built from the *Viking.sln* solution.  We use Visual Studio 2013 at this time.

**Viking:** The Viking client used to annotate RC1.  Built upon XNA and Windows Forms.

**Jotunn:** Prototype viewer built upon the Windows Presentation Foundation.  Currently missing the annotations.

Libraries shared by Viking and Jotunn
   
   **VolumeModel:** Model for loading sections and presenting them to a viewer as a single volume.  Shared by Viking and Jotunn.
   
   **WebAnnotationModel:** Model for loading annotations from the annotation web service.  Shared by Viking and Jotunn
   
   
Servers
-------

Servers are built from the *Connectome Project.sln* solution.  The retired WebVisualization (ConnectomeViz) site lives in VikingLegacy.

**Annotation Service:** Windows Communication Foundation service which is used to provide authenticated read/writes to an annotation database

**ConnectomeODataV4:** An OData v4 web service which provides anonymous read-only access to annotation databases over the web.

**DataExport:** A web service which exports files directly to clients

**WebAppSettings:** A package shared across all web services containing application settings.
    
Libraries
---------

These libraries are shared by both client and server components

**Geometry:** Contains the computational geometry routines and transformation math Viking uses to transform tiles and annotations into volume space.

**AnnotationVizLib:** Contains shared routines to perform analysis of annotations.

**GraphLib:** Shared library for graphs

**Utils:** Code used by both clients and server components


.. _The Viking source is available on Github: http://github.com/jamesra/Viking
