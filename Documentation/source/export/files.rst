
###################
Export File Formats
###################

The Viking web services can export files for use in external tools.  While a nicer interface is planned, currently the export is performed with URL's.

Prerequisites
-------------

For graph visualization we recommend using `Tulip`_ and the TLP file format.

Export directly from a URL
--------------------------

Exports are available under a volume URL's /export/ subpath.  An Export URL has the following components

.. http:get:: /export/( report_type )/( format )/
   
   
Neuron connectivity network
===========================

.. http:get:: /export/network/( Format )

   Requests the connectivity graph for the neurons specified in the query string.

   **Example request**
      
      Get all cells within one degree of seperation of cells 476 and 514.
      
      .. sourcecode:: http
      
         http://websvc1.connectomes.utah.edu/RC1/export/network/tlp?id=476,514&hops=1
      
   **Format:**
      * *TLP* - Tulip file format
      * *DOT* - Graphviz DOT file format
        
   :query id: ID numbers of cells to include in connectivity graph.  Commas seperate multiple IDs.
   :query hops: Degrees of seperation to include additional neurons in graph
   
   :resheader Content-Type: text/plain

Motif connectivity
==================

.. http:get:: /export/motif/( Format )

   Connectivity between classes of neurons based on label.  Includes all neurons.
   
   **Example request**
   
      Get a dot file of the morphology for use in Graphviz
      
      .. sourcecode:: http   
         
         http://websvc1.connectomes.utah.edu/RC1/export/motifs/dot
   
   **Format:**
      * *TLP* - Tulip file format
      * *DOT* - Graphviz DOT file format
     
   :resheader Content-Type: text/plain

Morphology
==========

.. http:get:: /export/morphology/( Format )

   Returns a 3D graph using annotations to determine node position.

   **Example request**
   
      Get the morphology of cells 180 and 476.
      
      .. sourcecode:: http
      
         http://websvc1.connectomes.utah.edu/RC1/morphology/tlp?id=180,476
   
   **Format:**
      * *TLP* - Tulip file format
     
   :query id: ID numbers of cells to include in connectivity graph.  Commas seperate multiple IDs.
   
   :resheader Content-Type: text/plain
  
.. _Tulip: http://tulip.labri.fr/