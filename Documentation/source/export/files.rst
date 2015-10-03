
#####################
Network Visualization
#####################

The Viking web services can export files for use in external tools.  



For more technical users exporting directly using URL's is documented below 

Prerequisites
=============

For graph visualization we recommend using `Tulip`_ and the TLP file format.  We also provide .dot files for use with `Graphviz`_.

Export from the web page
========================

This user friendlier `website`_ exports files for Marc lab hosted databases.  

Export directly from a URL
==========================

Exports are available under a volume URL's /export/ subpath.  An Export URL has the following components

.. http:get:: /export/( report_type )/( format )/
    
   
Neuron connectivity network
===========================

.. http:get:: /export/network/( Format )

   Requests the connectivity graph for the neurons specified in the query string.
      
   **Format:**
      * *TLP* - Tulip file format
      * *DOT* - Graphviz DOT file format
        
   :query id: ID numbers of cells to include in connectivity graph.  Commas seperate multiple IDs.
   :query hops: Degrees of seperation to include additional neurons in graph
   
   :resheader Content-Type: text/plain
   
   **Example request**
      
      Get all cells within one degree of seperation of cells 476 and 514.
      
      .. sourcecode:: http
      
         http://websvc1.connectomes.utah.edu/RC1/export/network/tlp?id=476,514&hops=1
         
      Get all cells in the network:
      
      .. sourcecode:: http
      
         http://websvc1.connectomes.utah.edu/RC1/export/network/tlp
   
      .. figure::  Network_2014_11_25.png   

Motif connectivity
==================

.. http:get:: /export/motif/( Format )

   Connectivity between classes of neurons based on label.  Includes all neurons.
   
   **Format:**
      * *TLP* - Tulip file format
      * *DOT* - Graphviz DOT file format
     
   :resheader Content-Type: text/plain
   
   **Example request**
   
      Get a dot file of the morphology for use in Graphviz
      
      .. sourcecode:: http   
         
         http://websvc1.connectomes.utah.edu/RC1/export/motifs/dot
         
      .. figure::  Motif_Export1.png 

Morphology
==========

.. http:get:: /export/morphology/( Format )

   Returns a 3D graph using annotations to determine node position.
   
   Nodes with a glowing effect are involved in a structure link.
   
   **Format:**
      * *TLP* - Tulip file format
     
   :query id: ID numbers of cells to include in connectivity graph.  Commas seperate multiple IDs.
   
   :resheader Content-Type: text/plain
   
   **Example request**
   
      Get the morphology of cells 180 and 476.
      
      .. sourcecode:: http
      
         http://websvc1.connectomes.utah.edu/RC1/export/morphology/tlp?id=180,476
         
      .. figure:: Morphology_Export1.png
      
Navigation between Viking and Tulip
-----------------------------------

    * Tulip to Viking: Morphology nodes in Tulip contain a **LocationInViking** column.  The contents of that column can be copied into the clip board.  Then in Viking use CTRL+G and paste the coordinates to jump to that location
    * Viking to Tulip: The context menus for annotations in Viking contain a **Copy Location ID** column.  Selecting that option puts the ID into the clipboard.  Then switch to Tulip and use the ID value to search the **LocationID** column of all nodes.  The resulting node matches the annotation in Viking.
         
         Viking **Copy Location ID** context menu
         
         .. figure:: TulipLocationIDSearch0.png
            
         Tulip search UI
         
         .. figure:: TulipLocationIDSearch.png
         
.. _Tulip: http://tulip.labri.fr/
.. _Graphviz: http://www.graphviz.org/
.. _website: http://websvc1.connectomes.utah.edu/Export