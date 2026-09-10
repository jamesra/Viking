=============================================================
Viking Identity Server: reply to sbfsem-tools 2026-09-09
=============================================================

:Date: 2026-09-10
:Re: viking-identity-2026-09-09.rst

Login page
==========

Your 404 report is correct. ``/connect/authorize`` on
``https://identity.codepharm.net:5001`` was sending the browser to
``/Account/Login`` *on the issuer*, which had no UI.

That authorize request now bounces to the existing management login,
then back to the issuer to finish the code:

.. code-block:: text

    GET :5001/connect/authorize
      -> 302 https://identity.codepharm.net:4001/Account/Login?returnUrl=https://identity.codepharm.net:5001/connect/authorize/callback?...
      -> (sign in)
      -> 302 https://identity.codepharm.net:5001/connect/authorize/callback?...
      -> 302 https://sbfsem-tools.com/auth/callback?code=...

You can turn on the sbfsem-tools.com switch and complete the callback
half. The ``mvc`` client should no longer die at the same 404.

Localhost redirect URI
======================

Registered, exact match, HTTP loopback as you asked:

* ``http://localhost:8765/auth/callback``
* ``http://127.0.0.1:8765/auth/callback``

Production remains ``https://sbfsem-tools.com/auth/callback``.

Volume names in ``/open``
=========================

Use **our** ``AccessibleVolumes`` ``name`` in the query string. We do
not put SBFSEM-tools renderer ids in the URL. Your mapping table is
fine; we will not encode ``NeitzInferiorMonkey`` on our side.

==========================  ==========================
Viking Identity ``name``    Your renderer id
==========================  ==========================
``InferiorMonkey``          ``NeitzInferiorMonkey``
``NM``                      ``NeitzNasalMonkey``
``TemporalMonkey``          ``NeitzTemporalMonkey``
``cped``                    ``NeitzCped``
``RC1`` ``RC2``             same
``RPC1`` ``RPC2``           same
==========================  ==========================

The other Identity volumes (``Zia``, ``Wohl``, ``Yiu``, ``RPC3``,
``McCall``, ``Vinberg``, ``Lobanova``, ``Ash``, ``Dominic``,
``Mosaics``, ``RPEculture``, ``ImmunoEM_Tests``, ``RC2-Internal``,
``gRPC-RC1-Test``, …) are not aliases of those renderers. Showing
“no renderer” in the picker is the right behavior. If you later add
a renderer, keep using the Identity ``name`` as ``volume=``.

Deep link shape
===============

Agreed: plain HTTPS, not ``viking://``.

.. code-block:: text

    https://sbfsem-tools.com/open?volume=RC1&cells=1178,5678&technique=voxel2

* ``volume`` — Identity name (``InferiorMonkey``, ``NM``, ``RC1``, …).
* ``cells`` / ``technique`` — optional; omitted is fine.

The Identity **website** will link volumes as
``https://sbfsem-tools.com/open?volume={name}`` only (new tab). Cell
and technique belong on Viking’s context menu, which is a different
codebase and is not in this change.

Unauthenticated visitors going through OIDC and landing back on the
same ``/open`` URL is the right behavior now that issuer login exists.
