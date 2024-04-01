# Browser Picker

A default browser replacement for windows to let you pick your preferred browser on the fly or in accordance with your own rules.

## Installation

Simply download and install the [latest MSI](https://github.com/mortenn/BrowserPicker/releases).
To avoid warnings about unknown publisher, [import](https://stackoverflow.com/questions/49039136/powershell-script-to-install-trusted-publisher-certificates) the provided certificate into your certificate store first.

To enable the browser picker window, you need to set Browser Picker as your default browser. 

### .NET Runtime

Please note that you will need the .NET 8 runtime to run the version packaged as an msi, or the Bundle.zip

If you do not want to have the .net runtime installed on your computer, you may download the Portable version, which includes the runtime.
At the time of this writing, the portable version only comes in a zip, but you may install the .msi and extract the contents into the `Program Files\BrowserPicker` directory.

### Windows 10/11

You need to open the settings app from the start menu, navigate into Apps, select Default apps, then change the Web browser to BrowserPicker.
Please ensure BrowserPicker can be started before you do this.

## Configuration

By simply launching BrowserPicker from the start menu or double clicking the `BrowserPicker.exe` file, you will be presented with a GUI to configure the behaviour.

<!-- TODO Add screenshots and documentation for configuration options -->

The configuration is saved in the Windows registry: `HKEY_CURRENT_USER\Software\BrowserPicker`, if you ever need to manually edit it or make a backup.

## Usage

When you open a link outside a browser, one of these things will happen, in order:

1. If you have previously selected `Always ask`, the browser selection window is shown.
2. If you have set up a configuration rule matching the url being opened, the selected browser will be launched with the url.
3. If you only have one browser running, the link will be opened in that browser.
4. If you have configured a default browser, it will be asked to open the url.
3. Otherwise, you will be presented with a simple window asking you which browser you want to use.  

The url is shown at the top of the window, and if it matches a list of known url shorteners, BrowserPicker will expand this address and show you the real one after a short delay.
If you do not want BrowserPicker to perform this operation (it will call the internet), you may disable this feature in the settings.

<!-- TODO Replace screenshot -->
![Screenshot 1](http://i.imgur.com/Lq5t7UQ.png)

### Keyboard shortcuts

When this window is open and has focus, you can use the following keyboard shortcuts:

`[enter]` or `[1]` Pick the first browser in the list

`[2]` Pick the second browser in the list

...

`[9]` Pick the ninth browser in the list

If you keep `[alt]` pressed while hitting one of these, the browser will be opened in privacy mode.

`[esc]` Abort and close window

If you click outside the window such that it loses focus, it will close without opening the url in any browser.

Each browser that supports it, has a blue shield button on the right side.
Browsers currently supporting privacy mode are firefox, internet explorer, and chrome.
Microsoft has not made a way of opening edge in privacy mode available from the command line yet, so it is not supported.

Currently running browsers will have their name in bold, whilst browsers not currently running will have their names in cursive.

As you use the application, it keeps count of how many times you selected each browser. This information is used to show you your browsers in your preferred order automatically.

At the bottom of the window, there is a checkbox to enable "always ask" and a hyperlink to open settings.

### Settings

#### Browsers
<!-- TODO Replace screenshot -->
![Screenshot 2](http://i.imgur.com/rBzgDbw.png)

The browser list shows you the browsers BrowserPicker has been configured or detected to use.

You can disable a browser by clicking `Enabled`, this will hide the browser from the selection list.

If you click the red X, you may remove a browser, but if it was automatically detected, it will return to the list the next time the refresh function is executed.

The refresh function gets automatically executed in the background, in case a new browser has been installed, but it can also be done manually from the configuratrion window.

#### Add browser

You may click the hyperlink `Add browser` to open a popup where you may manually add a browser that has not been detected - or some other tool that isn't a browser.

<!-- TODO Replace screenshots -->
![Screenshot 4](http://i.imgur.com/ickDffz.png)
![Screenshot 5](http://i.imgur.com/bbFltpi.png)

If you browse for the command first, the application will assume the executable also has an icon, and prefill that box.

The name of the application will be attempted to be set automatically based on information in the executable.

##### Chrome profiles

Tip for Chrome Users: If you are using multiple Chrome profiles, by default if you choose Chrome it will launch in the last
profile you launched Chrome with.  To make it possibe for browser picker to select a profile you can create a new browser 
for each profile, set the program to the chrome executable, and add a command line argument to specify which profile to launch:
`--profile-directory=Default` for the first profile, `--profile-directory="Profile 1"` for the second profile, and so on.

Please note that arguments with spaces do require "" around them to be properly passed to chrome.

##### Firefox profiles

Similar configuration should be possible for firefox.

#### Behaviour

#### Defaults
The defaults tab lets you configure rules to map certain urls to certain browsers.

<!-- TODO Replace screenshot -->
![Screenshot 3](http://i.imgur.com/Ealb42I.png)

<!-- TODO rewrite -->
If you want the defaults to only apply when the browser is already running, check the checkbox above the list.

When you open a link, the application will check if you have defined a default matching the end of the host part of the URL.
That is, if you open `https://www.github.com/mortenn/BrowserPicker`, a default set for `github.com` will match.

As the match happens on the end of the host, that url would also match `hub.com`.

In addition to the default behaviour to match the end of the URL host, you can also apply `prefix` and `regex` matching:
- A rule of `|prefix|https://github.com/mortenn` applies matching to the start of the full URL, so would match `https://github.com/mortenn/BrowserPicker` but not `https://github.com/stuartleeks/devcontainer-cli`
- A rule of `|regex|.*/mortenn` applies the `.*/mortenn` to the full URL, so would match `https://github.com/mortenn/BrowserPicker`

If multiple matches are found for a url, the longest match will be used.