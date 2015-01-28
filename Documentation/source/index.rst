.. Viking Documentation master file.

#################################
Welcome to Viking's Documentation
#################################


Viking is a multi-user web-based collaborative management system for images and volumes which allows users to view multi-terabyte datasets,
annotate images with their own annotation schema, and summarize the results. Viking was developed by James Anderson at the `Marc Lab`_ for
use with the first retinal connectome which was assembled using `SSTEM and Computation Molecular Phenotyping`_

Viking's role begins after images are collected and registered into a volume.  We use `Nornir`_ to build our volumes.  


Viking Client Installation
==========================

`Click here to install the Viking client`_

Viking requires a windows OS with a dedicated GPU.  Newer Intel chips with embedded graphics may also work.

.. toctree::
   Client Useage <Client/useage>
   Server Installation <installation/toctree>
   Export <export/toctree>
   :maxdepth: 2
   

Indices and tables
==================

* :ref:`genindex`
* :ref:`search`

.. _Click here to install the Viking client: http://connectomes.utah.edu/Software/Viking4/setup.exe
.. _Marc Lab : http://prometheus.med.utah.edu/~marclab/index.html
.. _Nornir: http://nornir.github.io
.. _SSTEM and Computation Molecular Phenotyping: http://www.plosbiology.org/article/info%3Adoi%2F10.1371%2Fjournal.pbio.1000074#abstract0