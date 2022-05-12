.. Viking Documentation master file.

.. image::  Banner.jpg 
   :width: 768
   :align: center
 
#################################
Welcome to Viking's Documentation
#################################


Viking is a multi-user web-based collaborative management system for images and volumes which allows users to view multi-terabyte datasets,
annotate images with their own annotation schema, and summarize the results. Viking was developed by James Anderson at the `Marc Lab`_ for
use with the first retinal connectome which was assembled using `SSTEM and Computation Molecular Phenotyping`_

Viking's role begins after images are collected and registered into a volume.  The Marc Lab uses `Nornir`_ to build our volumes.

Viking Client Installation
==========================

Viking requires the following:
   * Windows 7+ 
   * `.NET Framework 4.8`_
   * `XNA 4.0 Framework Redistributable`_
   * A dedicated GPU.  Intel chips with embedded graphics also work.
   * An internet connection
   
**Note for IT Administrators:** Viking's installer is signed by the University of Utah. 
Adding Viking's `public key`_ as a trusted publisher allows users to install Viking without administrator rights.

`Click here to install the Viking client`_
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^

Contents 
========

.. toctree::
   User Documentation <client/toctree>
   Exporting Data <export/toctree>
   Developer Documentation <developerdocs/toctree>
   Server Operation Documentation <server/toctree>
   Deployment <deployment/toctree>
   About <about>
   :maxdepth: 2
   
Account creation
================

Accounts are only required to write to the database.  Read-only access via Viking may be done anonymously.

Create accounts and do some basic visualization on the `original visualization site`_

.. image::  Footer_v2.jpg
   :width: 768
   :align: center

Indices and tables
==================

* :ref:`genindex`
* :ref:`search` 

.. _Papers: 
.. _Click here to install the Viking client: http://connectomes.utah.edu/Software/Viking4/setup.exe
.. _Marc Lab : http://marclab.org/
.. _Marc Lab Papers: http://marclab.org/science/papers/
.. _Nornir: http://nornir.github.io
.. _SSTEM and Computation Molecular Phenotyping: http://www.plosbiology.org/article/info%3Adoi%2F10.1371%2Fjournal.pbio.1000074#abstract0
.. _original visualization site: http://connectomes.utah.edu/viz/
.. _.NET Framework 4.8: https://www.microsoft.com/en-us/download/details.aspx?id=48130
.. _XNA 4.0 Framework Redistributable: http://connectomes.utah.edu/XNA%20Framework%204.0%20Redist.msi
.. _public key: https://connectomes.utah.edu/Software/Viking4/VikingPublicKey.cer