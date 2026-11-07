#########################
Known Installation Issues
#########################

Hello, Viking's installer will send you to this page when an installation or upgrade fails.  Sorry about that.

After checking to see if this page has a solution please `contact`_ the software author for assistance. 

New Installations
-----------------

	Common installation issues:

		* Missing the requirements listed under :doc:`Viking Client Installation </index>`, such as .NET Framework 4.8 on a version of Windows older than Windows 10 1903
		* Missing critical hardware such as a GPU
		* Very rarely environmental issues such as IT departments blocking connections
    
    
Existing installations
----------------------

	Rarely Viking has incompatibilities with previous versions.  The solution is almost always to uninstall Viking using "Add/Remove Programs" in Windows.  Then reinstall a fresh download.
	
	Possible causes:
	
		* Viking's security certificate was updated
		* Interruption during installation, power failure, etc...
	        

Known issues with specific versions of Viking
---------------------------------------------

1.1.270
=======

  	**2018-07-23**
	
		**Existing installations will see an error when upgrading to this version.  Users must uninstall and reinstall Viking.  Security improvements broke the auto-upgrade ability of previously installed versions.  Future versions should upgrade normally.**
	 
	 
.. _contact: https://github.com/connectomes/Viking/issues
