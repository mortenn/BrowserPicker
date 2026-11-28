# Security and network lookups

Most of what Browser Picker does happens on your computer. A few features contact the network. The **Security** tab controls them.

## What leaves your computer

| Feature | When | Who receives what | Uses the Windows proxy |
| --- | --- | --- | --- |
| Expand short links | Automatically, when the picker opens | The shortener's server receives the full short URL | Yes |
| Favicon | Automatically, when the picker opens | The website receives a request for the full URL, then for its icon | Yes |
| Connection check | Only when you click the padlock and confirm | The website receives a TLS connection (no page request) | No, it connects directly |
| CAA records (part of the connection check) | Only with the connection check, when enabled | Your DNS resolver receives the host name and its parent domains | Not applicable (DNS) |
| Domain registration (RDAP) | Only with the connection check, when enabled | The domain's registry receives the domain, for example `example.com`; `data.iana.org` serves the list of registries | Yes |
| Certificate history (crt.sh) | Only with the connection check, when enabled | crt.sh, run by Sectigo, receives the host name, for example `www.example.com` | Yes |
| URLhaus (abuse.ch) | Only with the connection check, when enabled and an Auth-Key is saved | URLhaus receives the host name and your Auth-Key | Yes |

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

## Lookups with outside services

These are off by default and only run with the connection check, never when the picker opens. The check window names each service and what it receives before you start. Their results are extra information, not a verdict: a lookup that fails, is rate limited or finds nothing shows as **Not available** and doesn't change the padlock.

- **Look up domain registration (RDAP)** shows when the domain was registered and how long ago, when it last changed and expires, its registrar and status. A domain registered only days ago is a common sign of a phishing site. Only the domain is sent, never the full URL.
- **Look up certificate history (crt.sh)** shows how many certificates public certificate transparency logs hold for the host, when the first and latest were issued, and by whom. A host whose first certificate is days old deserves a closer look. Only the host name is sent. crt.sh is often busy, and a host covered only by a wildcard certificate has no history under its own name.
- **Look up the host on URLhaus (abuse.ch)** shows whether URLhaus has seen malware downloads on the host, whether any are online now, their tags, and the host's Spamhaus DBL and SURBL status. It needs your own free Auth-Key from the [abuse.ch Authentication Portal](https://auth.abuse.ch/); paste it below the checkbox and select **Save**. The key is kept in Windows Credential Manager, not in the settings file, so exports and backups don't include it. Only the host name is sent, with the key. URLhaus tracks malware downloads, not phishing, so **Not listed** doesn't mean a site is safe.

## Security profiles

**Apply profile** sets all the options at once:

| Option | Default | Max privacy | Enable all |
| --- | --- | --- | --- |
| Probe URL shorteners / redirects | On | Off | On |
| Only for known URL shorteners | On | On | Off |
| Probe favicon | On | Off | On |
| Only for URLs matching defaults | On | On | Off |
| Include DNS CAA and certificate transparency evidence | On | Off | On |
| Look up domain registration (RDAP) | Off | Off | On |
| Look up certificate history (crt.sh) | Off | Off | On |
| Look up the host on URLhaus (abuse.ch) | Off | Off | On |
| Hide connection check button | Off | On | Off |
| Skip confirmation prompt | Off | Off | On |

On a company network, also read [Corporate networks, proxies and DNS filters](enterprise-networks.md).
