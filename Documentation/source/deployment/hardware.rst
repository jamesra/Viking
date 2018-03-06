###############
Server Hardware
###############

Viking server installs have four servers:

- Storage
- Build (used for nornir)
- Database
- Annotation Server

If Viking is used as a read-only viewer then only Storage and Build machines are required. 

Storage Server
--------------

The storage machine holds Terabytes of images that compose a volume. Data on this server
should be accessible to the internet via HTTP.  All HTTP access to this machine is read-only.  It
on serves static files.

Requirements:

- Provides enough storage to hold the entire volume it serves.
- A public facing web server for static images
- Fast network access to the build machine.
- Addition of the following MIME types:

  ========== =========
  Extension  MIME Type
  ---------- ---------
  .mosaic    plain/text
  .stos      plain/text
  .vikingxml application/xhtml+xml
  
Marc Lab Setup:

- 10Gb/sec network connection to the build machine. 
- Synology server with RAID-6  
 
Build Machine
-------------

The build machine is responsible for using `Nornir`_ to convert raw data into aligned volumes.
  
Requirements:

- An installation of `Nornir`_
- Powerful CPU's.  
- Roughly 2GB of RAM per CPU
- Fast network connection to storage server.
- Minimal storage

Marc Lab Setup:

- 32 Cores across two Xeon CPU's. 
- 64GB of RAM

Database Server
---------------

The database server runs Microsoft SQL Server 2014 or later.  It is on the
internal network.  A typical annotation database uses roughly a Gigabyte for a 
million annotations.  

Requirements:

- Internal network

Marc Lab Setup:

- Virtual machine with up to 8 cores and 16GB RAM.
- 2x 500GB SSD's with RAID-0

Annotation Server
-----------------

The annotation server runs IIS (Internet Information Server) and serves active content using
ASP.NET to the internet.  Currently the annotation server also provides authentication as well.
This machine is the host all of the Viking server services.  

Requirements:

- Internet visible
- IIS role installed

Marc Lab Setup:

- Virtual machine with up to 4 cores and 8GB RAM.


	

.. _Nornir : http://nornir.github.io
