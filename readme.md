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

1) If you have a single browser running, that one will be fed the url.
2) If you have more than one or zero, browsers running, you will be presented with a simple window asking you which browser to use.  

![Screenshot](http://i.imgur.com/R5jru1m.png)

When this window is open and has focus, you can use the following keyboard shortcuts:

`[1]` Pick the first browser in the list

`[2]` Pick the second browser in the list

...

`[9]` Pick the ninth browser in the list

`[esc]` Abort and close window

If you click outside the window such that it loses focus, it will close without opening the url in any browser.

Currently running browsers will have their name in bold, whilst browsers not currently running will have their names in cursive.
