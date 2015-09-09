
###############
Version History
###############

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
   
   * Alt+R : Create new ribbon post-synapse with “Bipolar”, “Ribbon”, “Glutamate” tags.
   * Alt+S : Create new conventional post-synapse with “Conventional” tag.
   * Alt+B : Create new conventional glutamatergic post-synapse with “Bipolar”, “Conventional”, “Glutamate” tags.
    
* Added support for hotkey commands to toggle structure attributes on/off.  Users can place the mouse over a structure and hit the hotkey to toggle one the following tags:  
        
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
