=====================================================================
Reply: Open in Viking measured, launch-code API, no password grant
=====================================================================

:Date: 2026-09-19
:Re: viking-identity-2026-09-18-open-in-viking-measured.rst,
     viking-identity-2026-09-18-seamless-both-ways.rst
:For: SBFSEM-tools

Companion #1 — snake_case on ``:6001``
======================================

Confirmed, and measured the same way you did: ``AddControllers()`` with no
naming policy emitted camelCase (``accessToken`` …). Installed Viking 1.2.61
reads ``access_token``. The exchange minted a token and spent the code; the
client never saw it.

``LaunchExchangeResponse`` now has ``JsonPropertyName`` attributes. After
``:6001`` is redeployed, the same Windows box should skip login, volume, and
segmentation. We will send the UTC time when that deploy lands. The wire
contract stays snake_case even after a tolerant desktop ships.

Companion #2–4
==============

In the next Viking Velopack release (not required for the 1.2.61 retest):

* Location-ID launches no longer reset ``UseDefaultPosition`` (the default
  section will not overwrite the jump).
* Exchange reads ``access_token`` or ``accessToken`` (and the same for the
  other three keys).
* Release writes ``%LOCALAPPDATA%\Viking\Logs`` (five rotated files) and puts
  the reason on the login status line when a code cannot be used.
* Auto-advance keys on the link's volume, not only on a token. Remembered
  credentials are submitted.
* A ``viking://`` start skips the blocking Velopack UI so the five-minute
  code is not burned on an update check.
* ``api=`` on the URL stays ignored.

You do not need to send coordinates until that client ships. Location is
already on ``viking://`` and is applied even when exchange fails. Coordinates
would only paper over the default-section race in 1.2.61.

Request 1 — ``POST /api/viking/launch-code``
============================================

Accepted, on ``:6001``, not a second ``CreateCode``:

.. code-block:: text

    POST /api/viking/launch-code
    Authorization: Bearer <sbfsem-tools access token>
    {"volume_name": "RPC1"}

    200 {"code":"...","expires_in":300,"viking_url":"viking://open?code=...&volume=...&volumeName=RPC1"}
    403 client is not sbfsem-tools, or the subject cannot open the volume
    404 no such volume

Only the ``sbfsem-tools`` client (``client_id`` / ``azp`` after introspect)
may mint. Append ``&location=`` yourselves. ``CreateCode`` stays for the
Identity website. JSON is snake_case; ``volumeName`` is accepted as an alias.
``volumeName`` remains the query-string name on ``viking://``.

The browser ``CreateCode`` route is the fallback for 401 / 5xx from this
endpoint.

Request 2 — password grant
==========================

Declined for now. Keep authorization code + PKCE.

Resource Owner Password is gone from OAuth 2.1. Your backend is a better
place than ``Viking.exe`` to hold a secret, but it is still a second password
form, and it cannot carry a second factor or an external provider. Identity
already has ``LoginWith2fa`` / external-login scaffolding (unused in
production; the login page says so). Turning on ROPC makes those a dead end
for SBFSEM-tools.

Chrome's "Dangerous site" report named a non-standard port and a long
redirect chain. ROPC hides that; it does not fix it. Serving Identity on 443
at one origin does.

Without Request 1, a password sign-in would leave no cookie with us, and
*Open in Viking* would hit ``CreateCode`` every time. Request 1 is the path
that removes the extra Identity tab.

Answers
=======

* Names: yes. ``POST /api/viking/launch-code``, ``volume_name`` (JSON),
  ``volumeName`` (URLs), ``code``, ``expires_in``, ``viking_url``.
* Second factor / external provider: not configured in production. The code
  is already there. If Pitt/SSO or 2FA is even a maybe, keep the code flow.

Nothing required on your side for companion #1. Retest 1.2.61 after we
confirm ``:6001`` is redeployed.
