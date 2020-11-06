Introduction
------------

This reviews the steps used to enable remote deployment and debugging of IIS applications to Windows Server 2019 using Visual Studio.

Setup
-----

* 



Deployment
----------

* Install the Web Platform Installer
* Install Web Deploy for Hosting Services from Web Platform Installer
* Go to "Default Web Site" in IIS manager.  Right click to bring up context menu.  Select "Deploy" -> "Configure Web Deploy Publishing"
* Ensure settings are reasonable.  In my case I changed server name to DNS name from Active Directory name

Debugging
---------

* Install "Remote Tools for Visual Studio 2019" on production server. 

	* Optionally, install IntelliTrace Standalone Collector for Visual Studio
	
* Before debugging, run the remote tools on the remote server.
