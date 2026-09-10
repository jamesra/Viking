=============================================================
Viking Identity Server: reply to returnUrl update 2026-09-10
=============================================================

:Date: 2026-09-10
:Re: vikingidentity20260910returnurl_update6.rst

Your diagnosis is correct
=========================

``Url.IsLocalUrl`` rejects the absolute issuer ``ReturnUrl``, and without an
allow-list the post-login path falls through to the management home page. That
matches what we see in our logs: successful password POST on ``:4001``, then
``302`` to ``/``, and never a hit on ``:5001/connect/authorize/callback``.

What we fixed
=============

We already had the allow-list you described (Authority origin +
``/connect/authorize`` / ``/connect/authorize/callback`` only — not an open
redirect). It was failing at runtime because management ``secrets.json`` had
overridden ``Authority`` to ``https://identity.codepharm.net:4001/`` (the UI
host). The allow-list compared your return URL on ``:5001`` to that value and
rejected it.

Correct configuration now:

* ``Authority`` / issuer = ``https://identity.codepharm.net:5001/``
* ``ManagementPublicUrl`` = ``https://identity.codepharm.net:4001/``

After restart, sign-in should follow:

.. code-block:: text

    POST :4001/Account/Login?returnurl=https://identity.codepharm.net:5001/connect/authorize/callback?...
      -> 302 https://identity.codepharm.net:5001/connect/authorize/callback?...
      -> 302 https://sbfsem-tools.com/auth/callback?code=...

Please retry “Sign in with Viking”. You should land back on your callback with
a code. If anything still sticks after the password is accepted, send the new
browser address bar URL and we will chase the next hop (shared session cookie
on ``:5001``).

Thanks for the precise report — it saved a wild-goose chase.
