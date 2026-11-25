# Corporate networks, proxies and DNS filters

This page is for people who use Browser Picker on a managed network, and for IT staff who support them. It explains how each network feature behaves behind a proxy, a firewall that inspects TLS, or a DNS filter such as Cisco Umbrella.

Browser Picker works without any network access. If a lookup fails, the picker still opens and still opens links; you only lose the extra information the lookup would have shown.

## Proxies

**Short link expansion and favicons** use the Windows proxy settings, like most Windows apps. Standard `HTTP_PROXY`, `HTTPS_PROXY` and `NO_PROXY` environment variables take precedence when they are set.

If the proxy requires you to sign in, or blocks the request, these lookups fail quietly. The picker then shows the short link instead of its target, and no icon.

**The connection check doesn't use the proxy.** It opens a direct connection to the website, because it needs to see the website's own certificate. On networks that only allow web traffic through a proxy, the check fails with a connection error. That result describes your network, not the website.

## Firewalls that inspect TLS

Some company firewalls decrypt and re-encrypt TLS traffic. When that happens, the connection check sees the firewall's certificate instead of the website's:

- the issuer is your company's certificate authority;
- the certificate may be valid, because your company's root certificate is trusted on your computer;
- CAA records may show **Unaligned**, because the domain owner never authorized your company's certificate authority, and the check then reports an error.

If you see your company's name as the issuer, the result reflects the inspection, not a problem with the website. Ask your IT department if you're unsure.

## DNS filters such as Cisco Umbrella

DNS filters answer lookups for blocked domains with the address of a block page.

- **Short link expansion and favicons** may receive the block page instead of the real response. A redirect may then point to the block page.
- **The connection check** connects to the block page server. Expect a certificate that doesn't match the host, or one issued by the filtering service.
- **CAA lookups** go to the DNS servers your computer is configured to use, which may be the filtering service.

Browser Picker doesn't detect DNS filters. A failed or unusual result on a filtered network usually means the domain was filtered.

## Recommendations for IT

- To stop all automatic requests, apply the **Max privacy** profile in the **Security** tab. It turns off short link expansion, favicons and CAA lookups, and hides the connection check button.
- To keep short link expansion but limit it, leave **Only for known URL shorteners** on and maintain the list in the **URL shorteners** tab.
- To roll out the same settings to several people, configure one computer and use **Backup** > **Export Browser Picker settings**, then import the file on the others. See [Settings file, backup and logs](settings-and-logs.md).
- To make Browser Picker the default browser for everyone, use a default associations file (the **Set a default associations configuration file** policy). The machine-wide installation registers the program ID `BrowserPicker` for `http`, `https` and `ftp`.

Browser Picker doesn't read Group Policy settings of its own yet.
