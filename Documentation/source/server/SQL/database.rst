
###################
Database deployment
###################

Prerequistes
------------

Viking uses SQL 2014 Enterprise, but should work with other versions of SQL as well.  We use the enterprise version to enable the change logging feature which tracks the history of each change.

Create the database
-------------------

Deploy the database from using the `CreateUpdateDatabase.sql`_ script.  Find/replace the name of the database with the annotation database you want to create.

When this is complete you need to ensure the following users have been created.
   **<Annotation Account>** - Either an ActiveDirectory, IIS application identity, or local SQL account that the web service application pool can be configured to use to talk to the database.  Must have write access to the database.

.. _CreateUpdateDatabase.sql: http://github.com/jamesra/Viking/blob/master/Servers/AnnotationService/SQL/DatabaseCreateUpdate/CreateUpdateDatabase.sql
