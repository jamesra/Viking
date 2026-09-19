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

Add a context menu that sends the browser (with the user's Identity session) to
``CreateCode`` **directly** — do not construct ``/Account/Login?ReturnUrl=…``
yourself. If the browser already has an Identity cookie from SBFSEM's OIDC
login, ``CreateCode`` should Challenge silently. A hand-built Login URL
forces a second Identity prompt and is the usual cause of "I already signed
into SBFSEM-tools but Identity asks again".

Required link::

    https://identity.codepharm.net:4001/VikingLaunch/CreateCode?volumeName=RC2&location=769111

``CreateCode`` must ``[Authorize]``, mint a one-use launch code (persisting
``VolumeName``, ``VolumeUrl``, and the place — Location ID or x,y,z), then
redirect to::

    viking://open?code=…&volume={endpointUrl}&volumeName=RC2&location=769111

``volume`` is the Identity volume **Endpoint** (the same URL Viking opens,
with or without ``volume.vikingxml``). ``volumeName`` is the Identity
resource name (``RC1``, ``RC2``, …). Both must be present so an already-open
Viking can match by URL **or** name.

The OS hands that URL to Viking. Viking exchanges the code at
``POST {LaunchExchangeBaseUrl}/api/viking/launch-exchange`` (default
``https://identity.codepharm.net:6001``). The exchange returns a
**volume-scoped** ``access_token``, ``volume_url``, ``volume_name``, and the
stored place (``location`` and/or ``x``/``y``/``z``/``ds``). LoginWindow then:

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

Store the place on ``VikingLaunchCode`` **and** put it on the ``viking://``
URL. Windows or a browser can drop query parameters after the first ``&``;
Viking merges location from the exchange response when the URL was stripped.
``VolumeName`` and the resolved endpoint URL are stored so launch-exchange
can mint the correct volume scopes and so an existing Viking instance can
match the open volume.

Same-volume instance reuse: if Viking already has that volume open, a new
``viking://`` click is forwarded to the running process (goto location /
coordinates and bring the window forward) instead of starting a second
window. A link for a **different** volume, or a click while Viking is still
on login/splash, still starts a new process.

SBFSEM-tools checklist (Jim)
----------------------------

1. **"Open in Viking"** must navigate the current tab (or a new tab) to
   ``https://identity.codepharm.net:4001/VikingLaunch/CreateCode?volumeName={IdentityName}&location={locationId}``.
   Optional ``cells`` is unused by Viking but may be logged. Do **not**
   build ``/Account/Login?ReturnUrl=…``.
2. SBFSEM-tools OIDC authority must be the same Identity issuer users hit
   for CreateCode (today ``https://identity.codepharm.net:4001`` / the
   configured Duende authority). If SBFSEM uses its own login or a
   different tenant, Identity will always show a password form even though
   the user is "already logged in" on sbfsem-tools.com. Cookies are
   host-scoped; they do not carry from ``sbfsem-tools.com`` to
   ``identity.codepharm.net`` unless the user completed an Identity OIDC
   redirect in that browser.
3. After a successful Identity session, CreateCode (Identity WebManagement)
   is responsible for the ``viking://`` redirect. SBFSEM-tools does not
   mint the launch code or put tokens in the URL.

Identity WebManagement / API checklist
--------------------------------------

1. ``VikingLaunch/CreateCode``: ``[Authorize]``, resolve ``volumeName`` to
   the volume Endpoint, persist subject + ``VolumeName`` + Endpoint +
   place, 302 to
   ``viking://open?code={code}&volume={endpoint}&volumeName={name}&location={id}``.
2. ``POST /api/viking/launch-exchange`` JSON must include at least::

       {
         "access_token": "…",
         "identity_server_url": "https://identity.codepharm.net:5001/",
         "volume_url": "https://…/RC1/",
         "volume_name": "RC1",
         "location": "6743"
       }

   ``x``, ``y``, ``z``, ``ds`` when the place was coordinates instead of a
   Location ID. Viking already reads these extra fields.
3. The volume Endpoint stored on the code must be the same host/path users
   open from the Identity volume tree. A name-only ``volume=RC1`` on
   ``viking://`` used to start a second Viking; Viking now matches by name
   as well, but the Endpoint still has to be correct for a **new** instance
   to skip the volume picker.

Migration
---------

Apply Identity DB migrations:

* ``20260311120000_AddVikingLaunchCodes`` — ``VikingLaunchCodes`` table.
* ``20260311130000_AddVikingLaunchCodeVolumeName`` — optional ``VolumeName``
  column on launch codes.
* Persist place on the launch code (Location ID and/or X/Y/Z/DS) and return
  it from launch-exchange. Add a migration if those columns are missing.
