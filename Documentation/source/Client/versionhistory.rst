
###############
Version History
###############

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
