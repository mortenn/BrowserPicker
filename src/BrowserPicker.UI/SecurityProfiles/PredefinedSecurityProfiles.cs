using BrowserPicker.Common;

namespace BrowserPicker.UI.SecurityProfiles;

public static class PredefinedSecurityProfiles
{
	public static readonly ISecurityProfile Default = new SecurityProfile
	{
		Id = "default",
		DisplayName = "Default",
		Options = SecurityOptions.Default,
	};

	public static readonly ISecurityProfile MaxPrivacy = new SecurityProfile
	{
		Id = "max-privacy",
		DisplayName = "Max privacy",
		Options = SecurityOptions.MaxPrivacy,
	};

	public static readonly ISecurityProfile EnableAll = new SecurityProfile
	{
		Id = "enable-all",
		DisplayName = "Enable all",
		Options = SecurityOptions.EnableAll,
	};

	public static readonly ISecurityProfile[] All = [Default, MaxPrivacy, EnableAll];
}
