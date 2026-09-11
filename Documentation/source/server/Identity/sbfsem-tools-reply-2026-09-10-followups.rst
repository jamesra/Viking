================================================================
Reply: Chrome warning, secret rotation, discovery, Open in Viking
================================================================

:Date: 2026-09-10
:Re: vikingidentity20260910contextmenus.rst
:For: SBFSEM-tools

Thank you for the clear write-up. Sign-in confirmation noted. We are **not**
adding a Viking context menu for “Open cell in SBFSEM-tools” in this round.
Below is what we did for the other items.

Loose ends
==========

Chrome “Dangerous site”
-----------------------

Agreed on the shape of the problem: login on ``:4001``, issuer on ``:5001``,
callback URLs with ``code`` / ``state`` / ``session_state``, and a password
form on a non-443 port are exactly the signals on-device phishing classifiers
look for.

On our side we will:

1. Verify ``identity.codepharm.net`` in Google Search Console and check
   **Security issues** (and request a review if anything is listed).
2. Report a Safe Browsing false positive if the property is clean.
3. Treat serving login + issuer on **port 443 under one origin** as the durable
   fix. That is a reverse-proxy / deployment change; it is planned, not done in
   this pass.

Please keep registering ``sbfsem-tools.com`` on your side as you described.

Stale session error on ``:5001/connect/authorize``
--------------------------------------------------

Understood: clear ``identity.codepharm.net`` cookies (or a private window)
recovers it. If it returns, send the UTC time and we will pull the
``:5001`` exception from the container logs.

Client secret rotation
----------------------

The ``sbfsem-tools`` client secret has been **rotated** on the Identity
deployment. The new value is **not** included here (it must not sit in chat or
mail again). James will send it on a separate channel. After you put it in
your root-only config and restart, the old secret stops working immediately.

Discovery ``scopes_supported``
------------------------------

Fixed. ``GetAllResourcesAsync`` now includes the standard identity resources
(``openid``, ``profile``, and the rest of our ``StandardIdentityResources``),
so discovery should list them. Token validation was already fine; this is for
strict client libraries.

HumanAMD
--------

No action needed on our side.

Open in Viking (SBFSEM-tools → Viking)
======================================

Parameter names
---------------

``volumeName`` and ``location`` are the accepted names. Keep ``volume``
mandatory in spirit: Identity resolves ``volumeName`` to the volume endpoint
and still puts ``volume=`` (endpoint URL) on the ``viking://`` redirect so the
desktop client has an unambiguous target. Structure and Location IDs remain
per-volume; we will keep requiring a volume on this path.

``location`` formats
--------------------

- A Location ID (integer), e.g. ``769111`` — desktop performs the same action
  as *Annotation → Goto Location ID*.
- Or ``x,y,z[,downsample]`` for a camera jump when there is no annotation.

CreateCode
----------

Authenticated browser call (user already has an Identity session):

.. code-block:: text

    https://identity.codepharm.net:4001/VikingLaunch/CreateCode?volumeName=RC2&location=769111

Redirects to something like:

.. code-block:: text

    viking://open?code=…&volume=…&volumeName=RC2&location=769111&api=https://identity.codepharm.net:6001

``VolumeName`` is stored on ``VikingLaunchCodes`` so launch-exchange can return
``volumeName`` and mint volume-scoped tokens. ``location`` is query-only (not
stored). ``api`` is the Permissions Web API base used for
``POST /api/viking/launch-exchange``.

Desktop Viking
--------------

The desktop client now:

* Parses ``viking://open`` (volume required; location optional).
* Exchanges ``code`` via the ``api`` launch-exchange endpoint when present.
* After the volume opens, jumps to the Location ID or ``x,y,z[,downsample]``.

Protocol-handler registration with Windows remains an installer concern; once
the OS hands Viking the URI, the above path applies. Rebuild/redeploy the
desktop client to pick this up.

Deferred: Viking → SBFSEM-tools context menu
============================================

We are holding the “Open cell in SBFSEM-tools” context menu on Viking for a
later pass. The ``/open?volume=&cells=&location=`` contract you documented is
noted for when we do that work.
