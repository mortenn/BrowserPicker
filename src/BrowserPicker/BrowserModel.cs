using BrowserPicker.Framework;
using System.Diagnostics;

namespace BrowserPicker;

[DebuggerDisplay("{" + nameof(Name) + "}")]
public sealed class BrowserModel : ModelBase
{
	public string? CustomName
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public required string Name
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public string? CustomIcon
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public string? IconPath
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public string? CustomCommand
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public required string Command
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public string? CustomExecutable
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public string? Executable
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public string? CustomArgs
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public string? CommandArgs
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public string? CustomPrivacyArgs
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public string? PrivacyArgs
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public int Usage { get; set; }

	public bool Disabled
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public bool Removed
	{
		get => field;
		set
		{
			field = value;
			Disabled = value;
			OnPropertyChanged();
		}
	}

	public int ManualOrder
	{
		get => field;
		set => SetProperty(ref field, value);
	}

	public bool ExpandFileUrls
	{
		get => field;
		set => SetProperty(ref field, value);
	}
}