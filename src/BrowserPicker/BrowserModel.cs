using BrowserPicker.Framework;
using System.Diagnostics;

namespace BrowserPicker;

[DebuggerDisplay("{" + nameof(Name) + "}")]
public sealed class BrowserModel : ModelBase
{
	public BrowserModel()
	{
		name = string.Empty;
		command = string.Empty;
	}
		
	public BrowserModel(IWellKnownBrowser known, string? icon, string shell)
	{
		name = known.Name;
		command = shell;
		PrivacyArgs = known.PrivacyArgs;
		Executable = known.RealExecutable;
		IconPath = icon;
	}

	public BrowserModel(string name, string? icon, string shell)
	{
		this.name = name;
		icon_path = icon;
		command = shell;
	}

	public string Name
	{
		get => name;
		set => SetProperty(ref name, value);
	}

	public string? IconPath
	{
		get => icon_path;
		set => SetProperty(ref icon_path, value);
	}

	public string Command
	{
		get => command;
		set => SetProperty(ref command, value);
	}

	public string? Executable
	{
		get => executable;
		set => SetProperty(ref executable, value);
	}

	public string? CommandArgs
	{
		get => command_args;
		set => SetProperty(ref command_args, value);
	}

	public string? PrivacyArgs
	{
		get => privacy_args;
		set => SetProperty(ref privacy_args, value);
	}

	public char? KeyBinding
	{
		get => key_binding;
		set => SetProperty(ref key_binding, value);
	}

	public int Usage { get; set; }

	public bool Disabled
	{
		get => disabled;
		set => SetProperty(ref disabled, value);
	}

	public bool Removed
	{
		get => removed;
		set
		{
			removed = value;
			Disabled = value;
			OnPropertyChanged();
		}
	}

	public int ManualOrder
	{
		get => manual_order;
		set => SetProperty(ref manual_order, value);
	}

	public bool ExpandFileUrls
	{
		get => expand_file_url;
		set => SetProperty(ref expand_file_url, value);
	}

	private bool disabled;
	private bool removed;
	private string name;
	private string? icon_path;
	private string command;
	private string? executable;
	private string? command_args;
	private string? privacy_args;
	private char? key_binding;
	private int manual_order;
	private bool expand_file_url;
}