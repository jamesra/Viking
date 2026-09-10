=============================================================
Viking Identity Server: Permissions API introspection fix
=============================================================

:Date: 2026-09-10
:Re: vikingidentity20260910permissionsapi_10.35 pm.rst

Your report matches our ``:6001`` logs
======================================

Authority on the Permissions API was already ``:5001``. The smoking gun was:

.. code-block:: text

    Success token introspection. Token active: False, for caller: api
    ApiName: null

So introspection reached the issuer and returned HTTP 200 with
``active: false``. That is why ``userinfo`` worked (issuer trusts the
reference handle) while ``:6001`` treated every call as anonymous — including
``UserAccessibleVolumeTree`` falling back to the public three volumes.

Cause
=====

``AddOAuth2Introspection`` was using OAuth **client** id ``api``. Duende
introspection authenticates an **ApiResource**: name + ``ApiSecrets``. Ours is
``Viking.Annotation`` (scope ``Viking.Annotation``). Caller ``api`` is not that
resource, so Duende deliberately reports the token inactive.

Fix (deploying)
===============

Introspection ``ClientId`` is now ``Viking.Annotation``, secret still the API
resource secret (``ApiSecret`` / ``GetClientSecret("api")``). Memory cache is
registered so ``EnableCaching`` is safe after restart.

After the container is up, ``GET :6001/Permissions/AccessibleVolumes`` with a
fresh access token should return ``jkuchen``'s ten volumes. Please retry and
confirm.

On the stale-cookie aside: clearing ``identity.codepharm.net`` cookies after the
``sub``/``idp`` fix is the right advice for browsers that still hold a
pre-fix session ticket.
