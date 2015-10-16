
###############
Version History
###############

1.1.168
-------

   * Added a "Find Volumes" button to the login UI.
   * Added support for viewing Open Connectome Project volumes

1.1.165
-------

   * Added context menu for locations to copy the ID into the clipboard.  This is useful to use to navigate to location in Tulip when rendering morphology.
   * Made exception catches when loading textures more specific in hopes of obtaining more informative bug reports for rare Acccess Violation.

1.1.162
-------

   * Only visible annotations are loaded from the server

1.1.159
-------
   
   * Reverted changes that used Microsoft.SqlServer.Types due to deployment error which I cannot investigate fully at this time
   * Added chevron arrow to animated structure links
   * Changed lines colors used when creating structure links. 
   * Fixed issue where invisbile adjacent section locations which were overlapped by locations on the current section could still be selected.
   * Improved logic to determine if a proposed LocationLink or StructureLink is valid.  This prevents linking child structures to their parents.
   * Fixed issue where two structure links were created for each structure link.
   
1.1.152
-------

   * Lines and Adjacent location indicators now use HSL blending to make details under the line more visible
   * Animated Structure Links are now longer to make the direction more apparent.
   * Preview feature, Hitting "L" enables one to add a curve which will not be saved.  Hit Esc to exit the command. 
   

1.1.150
-------

   * Fixed crash when paging sections very fast
   
1.1.148
-------

   * Tweaks to reduce memory footprint
   * Updated installer to install the .NET 4.6 framework

1.1.145
-------

   * Changed the selection of structure links to require the point fall within the line segment between the linked locations.

1.1.141
-------

   * Fixed merge structures returning an error 

1.1.140
-------

   * Detect changes to files in stos.zip and correctly update viking

1.1.139
-------

   * Free memory more aggressively when changing sections

1.1.138
-------

   * Shift+X now toggles the "Terminal" flag on the location under the mouse
   * Locations marked Terminal do not render on adjacent sections
   * Cleanup up the selection of locations on adjacent sections

1.1.137
-------

   * Fixed problem with missing DLL's in deployment
   * Optimized drawing code for annotations to take advantage of RTree
   * Increased maximum downloadable graph size to fix sections, RC1 #240, with too many annotations
   

1.1.130
-------

   * Fixed (hopefully) bugs involved with commands not exiting correctly
   * Switched to RTree, for more accurate selection of structures in the UI.
   
   Known-issue:
   
   * Viewing annotations with the volume transform disabled shows them in the incorrect position
   

1.1.129
-------

   Fixed a bug where the resize command was launching the move command (the default) for the selected location after exit.

1.1.128
-------

  2015-09-01

* Added ability for hotkey commands to automatically add tags to new structures via WebAnnotationUserSettings.xml file
   
   * Ctrl+R : Create new ribbon post-synapse with “Bipolar”, “Ribbon”, “Glutamate” tags.
   * Ctrl+S : Create new conventional post-synapse with “Conventional” tag.
   * Ctrl+B : Create new conventional glutamatergic post-synapse with “Bipolar”, “Conventional”, “Glutamate” tags.
    
* Added support for hotkey commands to toggle structure attributes on/off.  Users can place the mouse over a structure and hit the hotkey to toggle one the following tags:  
   
   * Shift+C - Conventional     
   * Shift+E - Glutamate
   * Shift+G - Glycine
   * Shift+P - Peptide
   * Shift+R - Ribbon
   * Shift+T - Tyrosine Hydroxylase
   * Shift+Y - GABA
      
   Mappings and tags can be customized on the server by editing the WebAnnotationUserSettings.xml file

1.1.125
-------

* The measurement tool now reports two values when a volume transform is applied.  The volume distance is the distance as it appears on the screen.  The mosaic distance is measured after transforming the origin points into mosaic space which does not have the additional distortion of the slice-to-volume transformations.  

1.1.124
-------

* Mapped *Home* key to rounding the downsample to nearest integer value
* Use UTC time when checking cache validity.
