SBFSEM-tools and Viking deep links
==================================

Bidirectional deep links between Viking and SBFSEM-tools. Structure and Location
IDs are numbered **per volume**, so every link must include a volume.

Auth model (both directions)
----------------------------

Desktop Viking and the browser do **not** share tokens.

* **SBFSEM-tools → Viking**: one-use launch code. ``CreateCode`` (browser
  Identity session) stores the subject, volume URL, and Identity volume
  name. Viking exchanges the code for a **volume-scoped** access token
  (client ``Viking``, grant ``viking_user_token``, scopes such as
  ``openid profile Viking.Annotation {Volume}.Read …``). Password login is
  skipped on the happy path.
* **Viking → SBFSEM-tools**: Identity-first **browser bounce**. The menu
  opens WebManagement ``SbfsemOpen/Redirect`` (cookie Challenge if needed),
  which 302s to SBFSEM-tools ``/open``. SBFSEM’s OIDC then sees an Identity
  SSO cookie and can sign in silently. No Viking bearer token is placed in
  the URL.

Viking → SBFSEM-tools
---------------------

Right-click an annotation (or structure) and choose **Open in SBFSEM-tools**.
Viking opens the browser to the Identity bounce URL (not SBFSEM directly)::

    https://identity.codepharm.net:4001/SbfsemOpen/Redirect?volume={IdentityName}&cells={rootStructureId}&location={locationId}

After authentication (or if a cookie already exists), Identity redirects to::

    https://sbfsem-tools.com/open?volume={IdentityName}&cells={rootStructureId}&location={locationId}

=============  =============================================================
Parameter      Meaning
=============  =============================================================
``volume``     Identity resource name (``RC2``, ``InferiorMonkey``, …).
``cells``      Top-level structure (``ParentID`` null), walked from the
               clicked annotation's structure.
``location``   Optional. Location ID that was right-clicked.
=============  =============================================================

Settings:

* ``SbfsemToolsIdentityBounceUrl`` — bounce entry (default
  ``https://identity.codepharm.net:4001/SbfsemOpen/Redirect``).
* ``SbfsemToolsOpenUrl`` — final ``/open`` base used by Identity after auth
  (default ``https://sbfsem-tools.com/open``; also
  ``SbfsemToolsOptions:OpenUrl`` on WebManagement).

Effect: first open may show an Identity login in the browser; later opens
reuse the cookie and stay silent when SBFSEM OIDC uses the same issuer.

SBFSEM-tools → Viking
---------------------

Add a context menu that sends the browser (with the user's Identity session) to::

    https://identity.codepharm.net:4001/VikingLaunch/CreateCode?volumeName=RC2&location=769111

``CreateCode`` mints a one-use launch code (persisting ``VolumeName`` when
resolved) and redirects to::

    viking://open?code=…&volume={endpointUrl}&location=…

The OS hands that URL to Viking. Viking exchanges the code at
``POST {LaunchExchangeBaseUrl}/api/viking/launch-exchange`` (default
``https://identity.codepharm.net:6001``). The exchange returns a
**volume-scoped** ``access_token`` (and ``volume_name``). LoginWindow then:

1. Auto-selects the linked volume (no volume picker).
2. Reuses the last accessible segmentation service when known; otherwise
   selects the only service or skips segmentation.
3. Completes login and navigates to the location.

Happy path: click Open in Viking → Viking starts → volume loads → camera
goes to the location. No password form, no volume picker, no segmentation
picker in the common case.

=============  =============================================================
Parameter      Meaning
=============  =============================================================
``volumeName`` Identity volume name. Required for SBFSEM-tools. Resolved to
               the volume ``Endpoint`` on the Identity side and stored on
               the launch code for scoped token minting.
``volume``     Volume endpoint URL. Still accepted (Identity UI buttons).
``location``   Either a Location ID (``769111``) **or**
               ``x,y,z[,downsample]`` for a non-annotation pick.
``x``, ``y``,
``z``, ``ds``  Optional explicit coordinate keys (same as Viking's legacy
               startup args). If both a Location ID and coordinates are
               present, **Location ID wins**.
=============  =============================================================

Location and coordinates are **not** stored on ``VikingLaunchCode``; they
only ride on the ``viking://`` URL. ``VolumeName`` **is** stored so
launch-exchange can mint the correct volume scopes.

Same-volume instance reuse: if Viking already has that volume open, a new
``viking://`` click is forwarded to the running process (goto location /
coordinates and bring the window forward) instead of starting a second
window. A link for a **different** volume, or a click while Viking is still
on login/splash, still starts a new process.

Migration
---------

Apply Identity DB migrations:

* ``20260311120000_AddVikingLaunchCodes`` — ``VikingLaunchCodes`` table.
* ``20260311130000_AddVikingLaunchCodeVolumeName`` — optional ``VolumeName``
  column on launch codes.
