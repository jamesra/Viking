
#####
Usage
#####

This documentation is the orinal documentation for Viking.  For a more comprehensive introduction we recommend this :download:`guide organized by Andrea Bordt <VikingUsage_MarshakLab2014.pdf>` based on documentation from the Marc Lab Annotation Fest. 

Startup
-------

The first step in using the Viking Annotation System is to `install the software on a PC`_.  Write permissions will require a user login and password assigned by the Marclab connectomics administrator. Every time Viking is launched thereafter it will automatically check for and install any updates. By default Viking will open the RC1 rabbit dataset or the last used dataset. To choose different datasets, examine the Viking dropdown list, enter new URL or see the Volumes page.

On startup a small window will appear as Viking downloads the transforms required to warp each image into the volume. Afterwards the main window appears after a short delay. The window has two fields. On left is a tabbed field that initially shows a list of available slices. Double-click a number to open that slice. By default Viking uses tiles and transforms optimized for internet use.

Navigation
----------

The escape key cancels any command. If you find yourself in an unfamiliar situation use escape to start over. No changes will be made to the database. The volume is viewed in read-only or annotate modes. Everyone has read privileges. Only validated analysts are permitted to annotate.

The volume has a number of structures:

   #. Greyscale image texture
   #. RGB image molecular channels
   #. Blue discs with identifying text - positional annotations
   #. Blue arrows indicate the location of annotations in slices above and below
   #. Small arrows within circles indicating links between overlapping annotations on adjacent sections
   #. White lines indicating links between non-overlapping annotations on adjacent sections.
   #. Other colored discs are child structures (synapses etc.) belonging to a parent structure (cell).
   #. The space bar toggles the annotations on and off
   #. The status bar at bottom gives the location:

      * **Section:** slice number
      * **X:** X location in physical pixels at magnification 1
      * **Y:** Y location in physical pixels at magnification 1
      * **Magnification:** actually the zoom out. Mag 1=maximum resolution Mag 10= 1/10 resolution
      * **Channels:** TEM or molecular channel names.  Font color matches channel color.
   
Moving around in XYZ with the mouse
===================================

Right click and drag on image (not annotations) to pan across the image. Avoid annotations.
Right click on annotations to open a context menu
Scroll wheel zooms
Left click and drag on blue arrows places new annotations
Left click and release on blank area continue the last annotation
Side clicks on a 5 button mouse move down and up 1 slice

Keyboard commands
=================

* **+** and **-** move down and up 1 slice
* **SHIFT +** and **-** move down and up 10 slices
* **PageUp/PageDown** Increase/Decrease zoom
* **Home** Roounds the zoom to the nearest integer.  If the zoom rounds to zero we use a zoom of 200%, downsampled by 0.5. 
   
Menu commands
=============

* Commands → Go to location → lets you to enter or paste (ctrl v) & jump to a site
* **Ctrl+C** copies a location
* Annotation → Open Structure → opens a navigation dialogue for a cell
* Bookmarks can be used (see below)
* Channels

Channels
========

There are two levels at which channel information is stored, volumes and sections. Default channel information is specified in the VolumeXML file. Section channel information overrides volume channel information. When drawing a section Viking first checks to see if the section specifies which channels should be used. If no channel information is present it then defaults to the volume channel setup. If no channel information is specified at all Viking defaults to displaying greyscale images.

Changing channel setup 
To override channels for a section right-click section number from the list on the left side. Select Properties from the drop down menu. One of the property page tabs be labeled channels. Select Default and click OK. The image should go to grayscale. Then you can switch channels by selecting Section → Channels from the drop down menu at the top menu bar.

.. _install the software on a PC: http://connectomes.utah.edu/Software/Viking4/setup.exe