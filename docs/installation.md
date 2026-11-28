# Install and set as default

Download the latest release from [GitHub releases](https://github.com/mortenn/BrowserPicker/releases).

## Choose a package

| Package | Needs the .NET runtime | Settings stored in |
| --- | --- | --- |
| `BrowserPicker.msi` | Yes | `%LOCALAPPDATA%\BrowserPicker\settings.json` |
| `Dependent.zip` | Yes | `%LOCALAPPDATA%\BrowserPicker\settings.json` |
| `BrowserPicker-Portable.msi` | No (included) | `settings.json` next to `BrowserPicker.exe` |
| `Portable.zip` | No (included) | `settings.json` next to `BrowserPicker.exe` |

The runtime-dependent packages need the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). The portable packages are larger because they include the runtime.

`BrowserPicker.msi` runs `ngen` during installation to build a native image, so the picker starts faster. If you use `Dependent.zip`, you can get the same benefit by running `ngen install BrowserPicker.exe`.

### Signing certificate

To avoid the "unknown publisher" warning, you can [import the provided certificate](https://stackoverflow.com/questions/49039136/powershell-script-to-install-trusted-publisher-certificates) into your certificate store before you install.

## Set Browser Picker as your default browser

Browser Picker only sees links when Windows sends them to it.

1. Start Browser Picker from the Start menu.
2. Select **Behaviour** > **Windows integration** > **Open Windows Default apps**. The first time you start Browser Picker, the Welcome tab has the same button, **Make Browser Picker your default browser**. Windows Settings opens on Browser Picker's page. (Windows doesn't let apps make themselves the default browser, so this last step is yours.)
3. Select **Set default**, or set it for `HTTP` and `HTTPS` one at a time.

**Behaviour** > **Windows integration** always shows whether Browser Picker opens web links, and which app does if it doesn't. It updates when you come back from Windows Settings.

Some organizations manage the default browser with a policy. If Windows resets the default after you change it, ask your IT department.

## Portable registration

The portable build registers itself for the current Windows user as **Browser Picker (Portable)**. It uses `HKEY_CURRENT_USER`, so it needs no administrator rights, and it stays separate from a machine-wide **Browser Picker** installation.

When a portable release starts, it adds this registration automatically if Browser Picker isn't installed for the machine, or if the machine-wide registration points to a different executable. If a portable registration already exists and points somewhere else, Browser Picker leaves it alone.

To add or remove the registration yourself, open **Behaviour** > **Windows integration** in the settings window. The option is hidden when you run the machine-wide installation.
