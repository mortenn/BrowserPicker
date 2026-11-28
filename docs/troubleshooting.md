# Troubleshooting

## The link opened without asking me

Browser Picker opens links without the picker when a [rule](rules.md) matches, when a fallback browser is set, or when exactly one browser is running. See [What happens when you open a link](picker.md#what-happens-when-you-open-a-link).

- To be asked every time, tick **Always ask** in the picker.
- To be asked whenever no rule matches, even with one browser running, turn on **Always ask when no default is matching URL** in [Behaviour](behaviour.md).
- To find out which rule matched, paste the URL into **Test defaults** on the Defaults tab.
- The **Feedback** tab's log shows a line such as `Opening the link in Microsoft Edge without showing the picker: no rule matches, and it is the only running browser.` for each link that skipped the picker.

## The picker never appears

Windows isn't sending links to Browser Picker. Check that it's set as the default browser in **Settings** > **Apps** > **Default apps**. See [Install and set as default](installation.md#set-browser-picker-as-your-default-browser).

If the default keeps changing back, your organization may set it with a policy. Ask your IT department.

## A browser is missing from the list

Select **Refresh browser list** on the Browsers tab. If it's still missing, the browser didn't register itself with Windows; add it with **Add browser**. See [Browsers and profiles](browsers.md).

## My changes to a browser keep disappearing

Detected browsers are updated when the list refreshes. Edit the browser and turn on **Disable automatic updates** to keep your changes.

## A profile is missing or has the wrong name

Check the profile's toggle on the Browsers tab. For Chromium browsers, the names come from the browser's `Local State` file; open the browser once so it updates the file.

## Container links open a page that doesn't load

The browser needs the [Open external links in a container](https://addons.mozilla.org/en-US/firefox/addon/open-url-in-container/) extension to understand `ext+container:` links. See [Firefox and Zen containers](browsers.md#firefox-and-zen-containers).

## Short links aren't expanded

Check **Probe URL shorteners / redirects** on the Security tab, and whether the host is in the **URL shorteners** list. On a company network, a proxy or DNS filter may block the request; see [Corporate networks, proxies and DNS filters](enterprise-networks.md).

## Report a problem

On the **Feedback** tab, select **Copy feedback report as Markdown** and paste it into a [new GitHub issue](https://github.com/mortenn/BrowserPicker/issues), or select **Report bug on GitHub**. Check the report before you post it; it can include URLs and paths from the log.
