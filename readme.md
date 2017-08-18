# Browser Picker

A default browser replacement for windows to let you pick your preferred browser on the fly.

## Installation

Simply download and install the latest MSI.

## Configuration

To enable the browser picker window, you need to set Browser Picker as your default browser

### Windows 10

Open the settings app from the start menu, navigate into Apps, select Default apps, then change the Web browser to BrowserPicker.

## Usage

When you open a link outside a browser, one of two things will happen:

1) If you have a single browser running, that one will be fed the url, unless you have enabled the "always ask" option.
2) If you have more than one or zero, browsers running, you will be presented with a simple window asking you which browser to use.  

![Screenshot](http://i.imgur.com/vKIXk8G.png)

When this window is open and has focus, you can use the following keyboard shortcuts:

`[enter]` or `[1]` Pick the first browser in the list

`[2]` Pick the second browser in the list

...

`[9]` Pick the ninth browser in the list

If you keep `[alt]` pressed while hitting one of these, the browser will be opened in privacy mode.

`[esc]` Abort and close window

If you click outside the window such that it loses focus, it will close without opening the url in any browser.

Currently running browsers will have their name in bold, whilst browsers not currently running will have their names in cursive.

As you use the application, it keeps count of how many times you selected each browser. This information is used to show you your browsers in your preferred order automatically.

At the bottom of the window, there are two checkboxes and a hyperlink.

Clicking the hyperlink will rescan your computer for browsers.

Ticking the option to always ask will disable the autoselect feature.

Ticking privacy mode will open the link in privacy mode when you select a browser.
Note: Only supported browsers will be selectable if you tick this box.
Browsers supporting privacy mode are firefox, internet explorer, and chrome.
Microsoft has not made a way of opening edge in privacy mode available from the command line yet, so it is not supported.
