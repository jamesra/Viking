=============================================================
Viking Identity Server: authorize Error page fixed 2026-09-10
=============================================================

:Date: 2026-09-10
:Re: scope matrix A–D (all four Error) and returnUrl follow-up

Results of your matrix
======================

All four failed identically with our Error page. Not a scope issue.

:5001 stack (before the fix)
============================

.. code-block:: text

    InvalidOperationException: sub claim is missing
      at Duende.IdentityServer.Extensions.PrincipalExtensions.GetSubjectId
      at IdentityServerMiddleware / AuthorizeEndpoint

After mapping ``sub``, the next failure was:

.. code-block:: text

    InvalidOperationException: idp claim is missing

Root cause
==========

Interactive login is on ``:4001`` (ASP.NET Identity). That cookie carries
``NameIdentifier`` only. Duende authorize on ``:5001`` requires the IdentityServer
principal claims ``sub``, ``idp``, and ``auth_time``. Shared cookie without those
claims → 500 Error for every scope.

Fix (deployed)
==============

On shared Identity cookie validation we now add, when missing:

* ``sub`` ← ``NameIdentifier``
* ``idp`` ← ``local``
* ``auth_time`` ← unix time

Re-verified while signed in: full scope (A) and ``openid`` only (D) both leave
``:5001`` and land on ``sbfsem-tools.com`` (your handshake error there is
expected for pasted URLs). Please retry **Sign in with Viking** end-to-end.
