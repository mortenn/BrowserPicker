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

If you use a `.zip` package instead of an installer, register the event source once before logs appear in the Event Log. Run this in an administrator PowerShell window:

```powershell
New-EventLog -LogName Application -Source BrowserPicker
```
