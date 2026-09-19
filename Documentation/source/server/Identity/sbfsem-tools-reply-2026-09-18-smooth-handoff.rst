=====================================================================
Reply: CreateCode 500, shared cookie, Open in Viking
=====================================================================

:Date: 2026-09-18
:Re: viking-identity-2026-09-18-smooth-handoff.rst
:For: SBFSEM-tools

Thank you for measuring this and for shipping silent ``/open`` plus
**Open in Viking**. Your three asks, in order:

1. ``CreateCode`` now challenges to login
=========================================

Your table was right: a signed-out browser got **500** on
``/VikingLaunch/CreateCode`` and **302** on ``/SbfsemOpen/Redirect`` and
``/Volumes``. The request id
``00-d155a7e3d12d4ee538329d01c640f99e-c60b3ef6c34671db-00`` matches that.

The attribute difference you found is the cause, with one extra detail.
``Bearer`` and ``Introspection`` *are* registered on ``:4001``. The 500 is
not a missing named handler. ``CreateCode`` listed

.. code-block:: csharp

    [Authorize(AuthenticationSchemes = "Bearer, Introspection, Identity.Application")]

ASP.NET authenticates every listed scheme. The Bearer handler's
``ForwardDefaultSelector`` returns an **empty scheme name** when the
browser has no ``Authorization`` header. That is treated as a scheme
lookup, which throws *No authentication handler is registered for the
scheme* **before** a challenge can run. Query string does not matter.
A signed-in browser would have seen the same 500, because Bearer is
tried first.

``CreateCode`` now uses the Identity application cookie only, like
``SbfsemOpen`` and ``Volumes``:

.. code-block:: text

    GET /VikingLaunch/CreateCode?volumeName=RC2&location=769111
      (no cookie)  ->  302 /Account/Login?ReturnUrl=/VikingLaunch/CreateCode?volumeName=RC2&location=769111
      (sign in)
      ->  302 viking://open?code=…&volume=…&volumeName=RC2&location=769111&api=…

``volumeName`` and ``location`` stay on the ReturnUrl. After this is
deployed to ``:4001``, please retry signed out and signed in.

2. Yes — the ``:4001`` cookie is the ``:5001`` session
======================================================

The 2026-09-10 change is still the contract:

* Cookie name ``.AspNet.SharedVikingIdentity``, path ``/``, host-only on
  ``identity.codepharm.net`` (cookies do not include port, so ``:4001``
  and ``:5001`` both receive it).
* Shared Data Protection application name ``VikingIdentityServer`` and
  the same ``/app/DataProtectionKeys`` ring in the all-in-one image.
* ``:5001`` ``CookieAuthenticationScheme`` is ``Identity.Application``.
* Cookie validation adds ``sub``, ``idp``, and ``auth_time`` so Duende
  will accept a management-site login on ``prompt=none``.

So a browser that is signed in on ``:4001`` should make
``:5001/connect/authorize?prompt=none`` succeed without a login page.
Your signed-out measurement (``prompt=none`` → ``login_required``) is
what we want when there is no cookie.

If Viking's menu still journals ``login_required`` while the person is
signed in on ``:4001``, the next check is whether both processes can
unprotect that cookie (same key ring). Look for
``.AspNet.SharedVikingIdentity`` on both ports after a ``:4001`` login.

3. Nothing else from you
========================

Agreed. Parts 1a and 1b (volume-scoped launch token, LoginWindow
auto-advance) are on our side. Launch-exchange already stores
``VolumeName`` and requests ``{Name}.Read|Annotate|Review``. We also
fixed the scope validator so an extension grant (empty ``UserName``,
subject = user id) can mint that volume-scoped token. Your
``CreateCode`` query is unchanged.

Downsample: we will use the value you send. Clamp on your side if a
whole-cell view of several hundred is too far out; we can cap later if
needed.

After ``:4001`` is redeployed, **Open in Viking** signed-out should land
on our login page, not the generic error page, and come back to
``viking://`` with the same ``volumeName`` and ``location``.
