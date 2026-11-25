# Security and network lookups

Most of what Browser Picker does happens on your computer. A few features contact the network. The **Security** tab controls them.

## What leaves your computer

| Feature | When | Who receives what | Uses the Windows proxy |
| --- | --- | --- | --- |
| Expand short links | Automatically, when the picker opens | The shortener's server receives the full short URL | Yes |
| Favicon | Automatically, when the picker opens | The website receives a request for the full URL, then for its icon | Yes |
| Connection check | Only when you click the padlock and confirm | The website receives a TLS connection (no page request) | No, it connects directly |
| CAA records (part of the connection check) | Only with the connection check, when enabled | Your DNS resolver receives the host name and its parent domains | Not applicable (DNS) |

Wrapped links from Outlook Safe Links, Teams and Facebook are unwrapped locally, with no network request.

## Automatic probes

These run while the picker prepares the window, before you pick a browser.

- **Probe URL shorteners / redirects** requests the URL to find out where it redirects. With **Only for known URL shorteners** (the default), this only happens for hosts in the **URL shorteners** tab.
- **Probe favicon** requests the page to find its icon for the address bar. With **Only for URLs matching defaults** (the default), this only happens for URLs that match one of your [rules](rules.md).

The **URL shorteners** tab lists the built-in shortener hosts and lets you add your own.

## Manual connection check

The padlock in the picker checks the website's TLS certificate before you open the link. It shows the certificate, the encryption used and whether the certificate matches the host.

- Nothing is sent until you confirm in the check window, unless you turn on **Skip confirmation prompt**.
- **Include DNS CAA and certificate transparency evidence** also looks up the domain's CAA records in DNS and reads the certificate transparency proofs embedded in the certificate. Reading the embedded proofs needs no extra request.
- **Hide connection check button** removes the padlock from the picker.

A good result means the connection looked normal at that moment. It doesn't mean the website is safe.

## Security profiles

**Apply profile** sets all the options at once:

| Option | Default | Max privacy | Enable all |
| --- | --- | --- | --- |
| Probe URL shorteners / redirects | On | Off | On |
| Only for known URL shorteners | On | On | Off |
| Probe favicon | On | Off | On |
| Only for URLs matching defaults | On | On | Off |
| Include DNS CAA and certificate transparency evidence | On | Off | On |
| Hide connection check button | Off | On | Off |
| Skip confirmation prompt | Off | Off | On |

On a company network, also read [Corporate networks, proxies and DNS filters](enterprise-networks.md).
