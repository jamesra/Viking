
###############
Troubleshooting
###############

IIS
---

  * Make sure the application pool is using an identity.  Databases use integrated security.  If resources are on another machine the application pool must operate as a domain account.  Failure to do this results in a faulted state for the WCF communication channel in Viking when trying to check structure types.
  * Make sure the Membership and role providers are configured. 
  
SQL
---

  * Occasionally a section will be swapped in position with its neighbor.  There is a SQL script to swap existing annotations in limited circumstances: Viking\Servers\SQL\UtilityScripts\SwapLocationsOnSections_V2.sql.  This script will preserve location links when the sections are adjacent to each other in the volume, which is the most common case. 