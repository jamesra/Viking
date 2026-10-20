########################
Ports and remote access
########################

Viking rows of the shared port table. Remote access (SSH, tunnels, who may reach which host) is
covered once in the `Nornir deployment guide`_.

=========  =====================  ====================  ===================================================
Host       Port                   Stack                 Reached from
=========  =====================  ====================  ===================================================
web        80, 443                viking-proxy          Internet (443 SNI passthrough, 80 ACME + redirect)
web        5001 (5000 h)          viking-identity       Proxy; Internet if clients use ``:5001`` directly
web        4001, 6001 (4000/      viking-identity       Internet (management site, permissions WebApi)
           6000 h)
web        4024                   viking-identity       Loopback only (remote debugger)
web        5011 (5010 h2c)        viking-annotation     Proxy (5011); 5010 local tests
web        41443 (41080 h2c)      viking-corrections    Proxy (41443); 41080 local
web        1433                   viking-annotation     Local only (profile ``local-sql``)
web        8442                   sam2-gallery          Proxy (gallery-router, outside this repo)
compute    40443                  viking-segmentation   Web host (proxy)
compute    40444                  viking-segmentation   Web host, only with the demo page on
compute    40445-40447            viking-segmentation   Web host (v3, v4, sem-v1)
compute    40545-40547            viking-segmentation   Loopback (plain-HTTP model demo pages)
iis        443 (80)               viking-iis            Web host (proxy); 80 only with
                                                        ``VIKING_HTTP_UPSTREAM_WEBSVC``
external   1433                   SQL Server            Web host and IIS host
=========  =====================  ====================  ===================================================

"h" marks a plain-HTTP or h2c companion port that does not need to leave the host.

Only the proxy's 80/443 and Identity's 4001/6001 (and 5001 if used directly) need to be open to the
Internet. Restrict the compute and IIS ports to the web host's address.

.. _Nornir deployment guide: https://nornir.github.io/deployment/
