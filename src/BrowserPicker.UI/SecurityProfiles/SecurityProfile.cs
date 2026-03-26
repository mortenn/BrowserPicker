using BrowserPicker.Common;

namespace BrowserPicker.UI.SecurityProfiles;

public sealed record SecurityProfile : ISecurityProfile
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public SecurityOptions Options { get; set; } = new();
}
