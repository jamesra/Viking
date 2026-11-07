
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
   #. Hold the space bar to hide the annotations; hold Ctrl+Space to hide the section image and show only the annotations. Annotations and image return when you release the keys
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

* **+** and **-** move up and down 1 slice (**+** goes to the next higher section number)
* **SHIFT +** and **-** move up and down 10 slices
* **Ctrl+Shift+Plus** and **Ctrl+Shift+Minus** brighten and darken sections Nornir built as noise-crushed; **Ctrl+Shift+0** undoes those brightness steps. It does not change a window saved with Section → Contrast... (see Menu commands). Other sections do not change
* **Arrow keys** pan the view by a quarter of the screen
* **PageUp/PageDown** zoom in/out by about 15% per press (the same step as one scroll wheel notch)
* **Home** rounds the magnification to the nearest power of two (1, 2, 4, 8 ... or 0.5, 0.25 ...)
* **Ctrl+Z** undoes your last annotation edit; repeated presses step back through up to 50 edits. It covers moving or reshaping a location, changing its type, cutting a hole, toggling Terminal or Off Edge, and giving an overlap to a cell. Creating, deleting, linking, merging and splitting are not undone. It does nothing while another command (a drag or pen stroke) is active
   
Menu commands
=============

* Commands → Go To Location → lets you to enter or paste (ctrl v) & jump to a site
* **Ctrl+C** copies the position of the mouse pointer as tab-separated X, Y, Z (section number) and DS (downsample) values; paste them into the Go To Location form (ctrl v) to return to that spot
* Commands → Capture Screen → saves part of the section as a PNG: left-click and drag a rectangle on the image, release, then set the center, size, **Downsample Factor**, folder and file name in the **Screenshot Parameters** dialog and click **Capture**. **Include overlays** adds the annotations to the picture. Image files already in the folder are reused, not re-rendered; to capture again, choose a new or empty folder
* Commands → Export Frames → opens **Frame Capture Setup**: a table with one row per picture (columns **X**, **Y**, **Z**, **Image Width**, **Image Height**, **Down Sample**; rows can be pasted from a spreadsheet with **Ctrl+V**), an output **Path**, a **File Prefix**, a **1st Frame #** and **Include Overlays**. **Export** saves each row as a PNG, skipping sections the volume does not have, and shows progress that you can cancel. The viewer returns to the section you were on afterwards. Image files already in the **Path** folder are reused, not re-rendered, so a repeated export skips frames it already wrote; to export again, choose a new or empty folder
* Commands → Export Tiles → opens **Export Tiles Configuration**: choose the **Output Directory**, one **Downsample Level**, and either **Export Tiles for all sections** or the **First Section in Range** and **Last Section in Range**. Viking writes the tile PNGs for each section under ``<Output Directory>/<volume name>/<section number>/Tiles/<downsample>/`` and shows progress that you can cancel
* Commands → Show Command Help → shows or hides the scrolling hint text for the active command along the bottom of the viewer. The choice is remembered for the next time Viking starts
* Volume → Transform → picks the volume transform to display (**None** or one of the volume's transforms; the active one is checked)
* Volume → Clear Image Cache → deletes the image tiles (texture cache) Viking saved on this computer for the open volume. A message names the cache folder, or says why some files could not be deleted; Viking keeps working either way
* Edit → Viewer Preferences → opens the **Viewer Preferences** window, which stays open beside the viewer. It has the groups **Section Number Overlay**, **Image Loading (Performance)** and **Animation PID Controller (Advanced)**
* Edit → Annotation Preferences → opens the annotation settings window
* Annotation → Open Structure → asks for a structure ID and opens that structure's properties window; Annotation → Goto Structure and Annotation → Goto Location ask for a structure ID or a location ID and move the viewer there
* Annotation → Open Last Modified Location → moves the viewer to the annotation that was changed most recently
* Annotation → Merge Structures → opens the **Merge Structures** dialog
* Annotation → Choose Favorited Structure Types → opens the window that picks the structure types offered by the **F6** shortcut
* Annotation → Pen Mode → switches Pen Mode on or off (the **\*** key does the same); Annotation → Show Pen Action Buttons shows or hides the round choice buttons that appear after a pen stroke; Annotation → Auto Polygonize Circles turns automatic circle-to-polygon conversion on or off. Each item is checked while it is on. Auto Polygonize Circles starts on for sessions with review access and off for annotate sessions until you change it
* Annotation → Show Pen Input Window → opens the pen input window, or hides and shows it if it is already open
* Annotation → Randomize All Colors → gives every cell a new random display color. Only what you see changes; nothing is saved, and reloading the annotations restores the original colors
* Annotation → Select Segmentation Service… → lists the segmentation servers you may use and applies the one you pick (or **None**); it needs a signed-in session
* Annotation → Update Volume Positions… → shown only to users with review access. It recomputes the stored volume positions under the volume's default alignment; run it after a section's alignment (.stos) changes
* Annotation → Export → Motifs → To Tulip Format → appears when the export service is available
* Help → Version Info shows the About box, Help → Change Log shows what changed in each Viking version, and Help → Viking Homepage opens the project web site in your browser
* Bookmarks can be used (see below)
* Section → Channel lists the section's channels; Section → Setup Channels... sets the volume's default channels
* Section → Contrast... opens the contrast editor for the noise-crushed channel on screen (greyed out for other channels). It shows the channel's histogram with **Black point**, **White point** and **Gamma** sliders. **Auto** clips the darkest and brightest 0.1% of pixels and sets the gamma that puts the median pixel at mid grey. **Reset to volume range** returns to the display range stored in the volume. Changes apply as you drag and are saved for that section and channel on this computer only

Channels
========

There are two levels at which channel information is stored, volumes and sections. Default channel information is specified in the VolumeXML file. Section channel information overrides volume channel information. When drawing a section Viking first checks to see if the section specifies which channels should be used. If no channel information is present it then defaults to the volume channel setup. If no channel information is specified at all Viking defaults to displaying greyscale images.

Changing channel setup 
To override channels for a section right-click section number from the list on the left side. Select Properties from the drop down menu. One of the property page tabs be labeled channels. Select Default and click OK. The image should go to grayscale. Then you can switch channels by selecting Section → Channel from the drop down menu at the top menu bar.

.. _install the software on a PC: https://websvc.codepharm.net/Software/Viking/Viking-win-Setup.exe