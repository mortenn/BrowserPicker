using BrowserPicker.Common;
using BrowserPicker.Common.Resources;

namespace BrowserPicker.UI.SecurityProfiles;

public static class PredefinedSecurityProfiles
{
	public static readonly ISecurityProfile Default = new SecurityProfile
	{
		Id = "default",
		DisplayName = Strings.Security_ProfileDefault,
		Options = SecurityOptions.Default,
	};

	public static readonly ISecurityProfile MaxPrivacy = new SecurityProfile
	{
		Id = "max-privacy",
		DisplayName = Strings.Security_ProfileMaxPrivacy,
		Options = SecurityOptions.MaxPrivacy,
	};

	public static readonly ISecurityProfile EnableAll = new SecurityProfile
	{
		Id = "enable-all",
		DisplayName = Strings.Security_ProfileEnableAll,
		Options = SecurityOptions.EnableAll,
	};

	public static readonly ISecurityProfile[] All = [Default, MaxPrivacy, EnableAll];
}
