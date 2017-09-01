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

![Screenshot 1](http://i.imgur.com/Lq5t7UQ.png)

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

At the bottom of the window, there is a checkboxes and a hyperlink.

Ticking the option to always ask will disable the autoselect feature.
Clicking the hyperlink will open the settings window

![Screenshot 2](http://i.imgur.com/rBzgDbw.png)

At the bottom of this window, there are three hyperlinks:

Add browser opens popup where you can manually add a browser that has not been detected - or some other tool that isn't a browser.

![Screenshot 4](http://i.imgur.com/ickDffz.png)
![Screenshot 5](http://i.imgur.com/bbFltpi.png)

If you browse for the command first, the application will assume the executable also has an icon, and prefill that box.

The name of the application will be attempted to be set automatically based on information in the executable.

Under the other tab, you can assign a default browser by domain:

![Screenshot 3](http://i.imgur.com/Ealb42I.png)

If you want the defaults to only apply when the browser is already running, check the checkbox above the list.

When you open a link, the application will check if you have defined a default matching the end of the host part of the URL.
That is, if you open https://www.github.com/mortenn/BrowserPicker, a default set for github.com will match.

As the match happens on the end of the host, that url would also match hub.com.

If multiple matches are found for a url, the longest match will be used.
