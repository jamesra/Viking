
###############
Troubleshooting
###############

IIS
---

Make sure the application pool is using an identity.  Databases use integrated security.  If resources are on another machine the application pool must operate as a domain account.  Failure to do this results in a faulted state for the WCF communication channel in Viking when trying to check structure types.
Make sure the Membership and role providers are configured. 
  
