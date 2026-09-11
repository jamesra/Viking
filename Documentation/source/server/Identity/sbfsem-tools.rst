========================================================
Integrating sbfsem-tools with the Viking Identity Server
========================================================

This page is written for the developers of `sbfsem-tools <https://sbfsem-tools.com/>`_. It
describes how to log users in with the Viking Identity Server and how to discover which image
volumes each user may access.

.. contents:: Table of Contents
   :depth: 2
   :local:

Summary
=======

Your Python backend acts as a *confidential OpenID Connect client*. Users log in on the Viking
Identity Server's own login page, are redirected back to your site, and your backend exchanges the
resulting authorization code for tokens. Your backend then calls the Permissions Web API with the
access token to list volumes and check access.

The browser never sees the client secret and never calls the Identity Server token endpoint
directly.

.. code-block:: text

    Browser  --(1) login--------------->  Your Python backend
    Browser  <-(2) redirect------------   Your Python backend
    Browser  --(3) authorize---------->  Identity Server issuer (:5001)
    Browser  <-(4) redirect to login--   Identity Server
    Browser  --(5) sign in------------->  Management site (:4001)
    Browser  <-(6) redirect with cookie   Management site
    Browser  --(7) authorize callback-->  Identity Server issuer (:5001)
    Browser  <-(8) redirect with code--   Identity Server
    Browser  --(9) /auth/callback------>  Your Python backend
             --(10) exchange code------>  Identity Server
             <-(11) access + refresh---   Identity Server
             --(12) list volumes------->  Permissions Web API (:6001)

Interactive login uses the management site on port ``4001``. The OIDC issuer remains
``:5001``. After sign-in, the browser returns to ``:5001/connect/authorize/callback`` with a
shared Identity cookie so the authorization code can be issued.

Endpoints
=========

============================  ==========================================================
Purpose                       URL
============================  ==========================================================
Issuer / authority            ``https://identity.codepharm.net:5001/``
OpenID discovery document     ``https://identity.codepharm.net:5001/.well-known/openid-configuration``
Interactive login             ``https://identity.codepharm.net:4001/Account/Login`` (authorize redirects here)
Permissions Web API           ``https://identity.codepharm.net:6001/``
============================  ==========================================================

Read the authorize, token, and userinfo endpoints from the discovery document rather than
hard-coding them. Note that the login **form** is on port ``4001`` while the protocol endpoints
are on port ``5001``, and the Permissions API is on port ``6001``.

Client registration
===================

The following client has been registered for you.

========================  ==============================================================
Setting                   Value
========================  ==============================================================
Client id                 ``sbfsem-tools``
Client secret             Sent to you separately. Never commit it or expose it to the browser.
Grant type                Authorization code
PKCE                      Required (``S256``)
Redirect URI              ``https://sbfsem-tools.com/auth/callback``
Local redirect URIs       ``http://localhost:8765/auth/callback``, ``http://127.0.0.1:8765/auth/callback``
Post-logout redirect URI  ``https://sbfsem-tools.com/``
Scopes                    ``openid profile Viking.Annotation offline_access``
========================  ==============================================================

If your framework uses a different callback path, ask us to add it. The Identity Server rejects
any redirect URI that is not registered exactly. The localhost URIs above are for local
development of your confidential backend.

You do not need volume-specific scopes such as ``RC1.Read``. The Permissions API authorizes on the
signed-in user, not on the scopes in the token.

Logging a user in
=================

The example below uses `Authlib <https://docs.authlib.org/>`_ with Flask. The equivalent Authlib
integrations for FastAPI, Django, and Starlette work the same way.

.. code-block:: bash

    pip install authlib flask httpx

.. code-block:: python

    import os

    from authlib.integrations.flask_client import OAuth
    from flask import Flask, redirect, session, url_for

    IDENTITY_AUTHORITY = "https://identity.codepharm.net:5001/"

    app = Flask(__name__)
    app.secret_key = os.environ["FLASK_SECRET_KEY"]

    oauth = OAuth(app)
    oauth.register(
        name="viking",
        client_id="sbfsem-tools",
        client_secret=os.environ["VIKING_CLIENT_SECRET"],
        server_metadata_url=f"{IDENTITY_AUTHORITY}.well-known/openid-configuration",
        client_kwargs={
            "scope": "openid profile Viking.Annotation offline_access",
            "code_challenge_method": "S256",
        },
    )


    @app.route("/login")
    def login():
        return oauth.viking.authorize_redirect(url_for("callback", _external=True))


    @app.route("/auth/callback")
    def callback():
        token = oauth.viking.authorize_access_token()
        session["access_token"] = token["access_token"]
        session["refresh_token"] = token.get("refresh_token")
        session["user"] = token.get("userinfo")
        return redirect(url_for("volumes"))

``authorize_access_token`` performs the PKCE code exchange and validates the ID token for you.
Store the tokens in a server-side session, not in a cookie readable by JavaScript.

If you prefer to drive the flow manually, the exchange is a standard form post:

.. code-block:: python

    import httpx

    response = httpx.post(
        f"{IDENTITY_AUTHORITY}connect/token",
        data={
            "grant_type": "authorization_code",
            "code": code,
            "redirect_uri": "https://sbfsem-tools.com/auth/callback",
            "code_verifier": code_verifier,
            "client_id": "sbfsem-tools",
            "client_secret": os.environ["VIKING_CLIENT_SECRET"],
        },
    )
    tokens = response.json()

Refreshing the access token
---------------------------

Access tokens are short lived. Because ``offline_access`` is allowed, you receive a refresh token
and can renew silently:

.. code-block:: python

    tokens = httpx.post(
        f"{IDENTITY_AUTHORITY}connect/token",
        data={
            "grant_type": "refresh_token",
            "refresh_token": session["refresh_token"],
            "client_id": "sbfsem-tools",
            "client_secret": os.environ["VIKING_CLIENT_SECRET"],
        },
    ).json()

A refresh returns a new refresh token; store the new value and discard the old one.

Reading volume permissions
==========================

Call the Permissions Web API on port ``6001`` with the access token in an ``Authorization`` header.
Access tokens are *reference tokens*, so they are opaque strings; do not try to decode them.

============================================  ==========================================================
Request                                       Result
============================================  ==========================================================
``GET /Permissions/CurrentUser``              The signed-in user name, as a JSON string
``GET /Permissions/CurrentUserId``            The user's identifier, as a JSON string
``GET /Permissions/AccessibleVolumes``        Every volume the user may access, keyed by volume id
``GET /Permissions/resource/{volume}``        The user's permissions on one volume
============================================  ==========================================================

Listing the volumes a user can access
-------------------------------------

.. code-block:: python

    import httpx

    PERMISSIONS_API = "https://identity.codepharm.net:6001/"


    def accessible_volumes(access_token: str) -> list[dict]:
        response = httpx.get(
            f"{PERMISSIONS_API}Permissions/AccessibleVolumes",
            headers={"Authorization": f"Bearer {access_token}"},
        )
        response.raise_for_status()
        return list(response.json().values())

The response is an object keyed by volume id:

.. code-block:: json

    {
      "42": {
        "id": 42,
        "name": "RC1",
        "description": "Rabbit retinal connectome 1",
        "endpoint": "https://websvc.codepharm.net/RC1",
        "permissions": ["Read", "Annotate"]
      }
    }

Verifying access to one volume
------------------------------

.. code-block:: python

    def volume_permissions(access_token: str, volume_name: str) -> list[str]:
        response = httpx.get(
            f"{PERMISSIONS_API}Permissions/resource/{volume_name}",
            headers={"Authorization": f"Bearer {access_token}"},
        )
        if response.status_code == 404:
            raise ValueError(f"No such volume: {volume_name}")
        response.raise_for_status()
        return response.json()


    def may_annotate(access_token: str, volume_name: str) -> bool:
        return "Annotate" in volume_permissions(access_token, volume_name)

The three permission values are ``Read``, ``Annotate``, and ``Review``. An empty list means the
user has no access to that volume. A ``401`` means the token is missing, expired, or revoked;
refresh it and retry.

Volume names containing spaces are written with hyphens in URLs. The volume displayed as
``gRPC RC1 Test`` is requested as ``gRPC-RC1-Test``.

User accounts and grants
========================

Registering the ``sbfsem-tools`` client does not by itself give anyone access to data. Each person
must have a Viking Identity account, and access to each volume is granted per user or per group in
the Viking Identity management site. If a user signs in successfully but
``AccessibleVolumes`` is empty, their account exists but has not been granted any volumes yet.

Security notes
==============

- Keep the client secret on the server, in an environment variable or secret store. Never ship it
  to the browser and never commit it.
- Use the authorization code flow described here. Do not ask users for their Viking password and do
  not use the resource owner password grant.
- Do not call the Identity Server token endpoint from browser JavaScript.
- Serve your callback over HTTPS in production; the production redirect URI is HTTPS only.
  Loopback ``http://localhost:8765/...`` and ``http://127.0.0.1:8765/...`` are allowed for local development.
- Treat access and refresh tokens as credentials: server-side session storage, and clear them on
  logout.

Deep links from the Identity management site
============================================

The management website links volumes with Identity ``name`` values only:

.. code-block:: text

    https://sbfsem-tools.com/open?volume=RC1

Use the ``AccessibleVolumes`` ``name`` (``InferiorMonkey``, ``NM``, ``RC1``, …) as ``volume=``.
Optional ``cells`` and ``technique`` query parameters are for a future Viking context menu, not the
management site. Map your renderer ids on your side.

Opening a place in Viking from SBFSEM-tools
==========================================

With the user already signed in to Identity (cookie on the management site), send the browser to:

.. code-block:: text

    https://identity.codepharm.net:4001/VikingLaunch/CreateCode?volumeName=RC2&location=769111

=============  =============================================================
Parameter      Meaning
=============  =============================================================
``volumeName`` Identity volume name (``AccessibleVolumes`` ``name``). Preferred.
``volume``     Alternate: volume id, endpoint URL, or name (legacy).
``location``   Optional Location ID, or ``x,y,z[,downsample]`` for a camera jump.
=============  =============================================================

``CreateCode`` persists ``VolumeName`` on the launch code (for volume-scoped
tokens at exchange). ``location`` remains query-only on the ``viking://`` URL.

Opening SBFSEM-tools from Viking
================================

Viking opens an Identity bounce (so the browser has / reuses the Identity cookie),
not SBFSEM-tools directly:

.. code-block:: text

    https://identity.codepharm.net:4001/SbfsemOpen/Redirect?volume=RC1&cells=598&location=16568

After authentication (or if a cookie already exists), Identity checks volume
access and redirects to:

.. code-block:: text

    https://sbfsem-tools.com/open?volume=RC1&cells=598&location=16568

Configure the final ``/open`` base with ``SbfsemToolsOptions:OpenUrl`` on
WebManagement (default ``https://sbfsem-tools.com/open``).

See also the reply note :doc:`sbfsem-tools-reply-2026-09-10` for the confirmed mapping table.
