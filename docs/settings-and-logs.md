# Settings file, backup and logs

## Where settings are stored

| Build | File |
| --- | --- |
| Installed (`BrowserPicker.msi`, `Dependent.zip`) | `%LOCALAPPDATA%\BrowserPicker\settings.json` |
| Portable (`BrowserPicker-Portable.msi`, `Portable.zip`) | `settings.json` in the same folder as `BrowserPicker.exe` |

Older versions stored settings in the registry under `HKEY_CURRENT_USER\Software\BrowserPicker`. When there is no `settings.json` yet, Browser Picker copies those settings into a new file on first start. A portable build that has no file of its own copies the installed build's `settings.json` instead.

Close Browser Picker before you edit the file by hand. The repository has a [JSON schema](https://github.com/mortenn/BrowserPicker/blob/main/schemas/browserpicker-settings.schema.json) that editors such as Visual Studio Code can use to check your changes.

## Back up and restore

The **Backup** tab of the settings window can:

- **Export Browser Picker settings** to a file, and **Import Browser Picker settings** from one;
- **Copy settings** to the clipboard, and **Paste settings** from it.

Use these to move your setup to another computer, or to give several people the same rules.

## Logs

Browser Picker logs through the standard .NET logging system, to the Windows Event Log (source `BrowserPicker`) and to the in-app log on the **Feedback** tab.

The log levels are set in `appsettings.json` next to `BrowserPicker.exe`:

- `Logging:LogLevel:Default` sets what the app records, including what appears on the Feedback tab. The default is `Information`.
- `Logging:EventLog:LogLevel:BrowserPicker` sets what goes to the Windows Event Log. The default is `Warning`.

For more detail while you troubleshoot, raise one or both:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "System": "Information",
      "Microsoft": "Information"
    },
    "EventLog": {
      "LogLevel": {
        "Default": "Warning",
        "BrowserPicker": "Information"
      }
    }
  }
}
```

You can also set them with environment variables, such as `Logging__LogLevel__Default=Debug` or `Logging__EventLog__LogLevel__BrowserPicker=Information`.

The **Level** list on the Feedback tab only filters what the viewer shows. It doesn't change what Browser Picker records.

### Diagnostic log file

Each link starts a new Browser Picker process, so the log on the Feedback tab only shows the current window. To see how earlier links were handled, turn on **Write diagnostic log to file** on the Feedback tab. Every process then appends to a daily file in `%LOCALAPPDATA%\BrowserPicker\logs`, including the Debug lines that explain each decision, whatever the levels in `appsettings.json`.

- It is off by default.
- URLs are reduced to scheme and host, such as `https://example.com/[redacted]`, unless you turn on **Include full URLs (path and query)**. Full URLs can contain private data, so only turn it on while you investigate, then delete the logs.
- Your user profile path is written as `%USERPROFILE%`.
- **Delete log files after** keeps files for 1, 3, 7 or 30 days. Each file stops growing at 5 MB.
- **Open latest log**, **Open log folder** and **Delete log files** are next to the options.

If you use a `.zip` package instead of an installer, register the event source once before logs appear in the Event Log. Run this in an administrator PowerShell window:

```powershell
New-EventLog -LogName Application -Source BrowserPicker
```
