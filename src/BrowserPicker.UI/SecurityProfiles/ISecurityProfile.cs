using BrowserPicker.Common;

namespace BrowserPicker.UI.SecurityProfiles;

public interface ISecurityProfile
{
	string Id { get; }
	string DisplayName { get; }
	SecurityOptions Options { get; }
}
