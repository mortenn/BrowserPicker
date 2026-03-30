# XAML Styler

This repository uses [XAML Styler](https://github.com/Xavalon/XamlStyler) for consistent XAML formatting.

## Configuration

The shared configuration is stored in `Settings.XamlStyler` in the repository root.
XAML Styler (both the VS extension and the CLI tool) automatically picks up this file.

For details on available options see the [XAML Styler wiki](https://github.com/Xavalon/XamlStyler/wiki/External-Configurations).

## Visual Studio extension

Install **XAML Styler for Visual Studio 2022+** from the
[VS Marketplace](https://marketplace.visualstudio.com/items?itemName=TeamXavalon.XAMLStyler2022).
When you open the solution, Visual Studio should also prompt you to install the
extension automatically (via `.vsconfig`).

## CLI – batch formatting

The CLI tool is registered as a dotnet local tool. Restore and run it with:

```
dotnet tool restore
dotnet xstyler --recursive --directory .
```

This reformats every `*.xaml` file in the repository according to the shared
`Settings.XamlStyler` configuration.

The repository also declares formatter-specific line ending expectations in
`.gitattributes`, so `*.xaml` files follow the conventions expected by XAML
Styler on Windows.
