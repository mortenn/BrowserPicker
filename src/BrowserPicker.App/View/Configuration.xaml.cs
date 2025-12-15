using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Win32;

namespace BrowserPicker.View;

public partial class Configuration
{

	private void OnMouseEnter(object sender, MouseEventArgs e)
	{
		var hyperlink = sender as Hyperlink;
		if (hyperlink != null)
		{
			hyperlink.TextDecorations = TextDecorations.Underline;
		}
	}

	private void OnMouseLeave(object sender, MouseEventArgs e)
	{
		var hyperlink = sender as Hyperlink;
		if (hyperlink != null)
		{
			hyperlink.TextDecorations = null;
		}
	}

	private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
	{
		try
		{
			string url = e.Uri.ToString();

			// 相对 URL 自动补充
			if (!e.Uri.IsAbsoluteUri)
			{
				if (url.StartsWith("/"))
				{
					url = "https://github.com" + url;
				}
				else
				{
					url = "https://" + url;
				}
			}
			else if (!url.StartsWith("http://") && !url.StartsWith("https://"))
			{
				url = "https://" + url;
			}

			Process.Start(new ProcessStartInfo(url)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			string msg = BrowserPicker.Resources.i18n.CsConfigHyperlinkError;
			string error = BrowserPicker.Resources.i18n.UniError;
			MessageBox.Show($"{msg} '{e.Uri}': {ex.Message}", error, MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}



	// 外壳刷新API
	[DllImport("shell32.dll", SetLastError = true)]
	private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

	private const uint SHCNE_ASSOCCHANGED = 0x08000000;
	private const uint SHCNF_IDLIST = 0x0000;
	private const uint SHCNF_FLUSH = 0x1000;

	private readonly string _appPath;
	private readonly string _appExeName;
	private const string AppName = "BrowserPicker";
	private const string ProgIdHtml = $"{AppName}.html";
	private const string ProgIdUrl = $"{AppName}.url";

	// 仅操作普通用户可写的路径，避免权限问题
	private readonly string[] _fileTypes = new[] { ".htm", ".html", ".url" };
	private readonly string[] _protocols = new[] { "http", "https" };
	private readonly string[] _protocolFileAssocs = new[] { "InternetShortcut", "URLFile" };

	public Configuration()
	{
		InitializeComponent();
		// 确保路径非空
		string msg = BrowserPicker.Resources.i18n.CsConfigConfigurationNameErorr;
		var entryAssembly = Assembly.GetEntryAssembly();
		_appPath = !string.IsNullOrEmpty(Environment.ProcessPath)
			? Environment.ProcessPath
			: Path.Combine(AppContext.BaseDirectory, Path.GetFileName(AppName + ".exe"));
		_appExeName = Path.GetFileName(_appPath) ?? throw new InvalidOperationException(msg);
	}

	// 注册按钮点击事件
	private void RegisterOpenWith_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			// 1. 优先创建ProgID（系统核心识别项）
			CreateProgId(ProgIdHtml, "HTML", isUrl: false);
			CreateProgId(ProgIdUrl, "URL", isUrl: true);

			// 2. 注册文件类型（仅User）
			foreach (var ext in _fileTypes)
			{
				RegisterFileType(ext, ext == ".url" ? ProgIdUrl : ProgIdHtml);
			}

			// 3. 注册URL协议
			foreach (var proto in _protocols)
			{
				RegisterUrlProtocol(proto, ProgIdUrl);
				AddProtocolContextMenu(proto); // 直接加菜单，绕开权限限制
			}

			// 4. 注册为默认程序
			RegisterAsDefaultProgram();

			// 5. 关联系统URL文件类型
			foreach (var fileType in _protocolFileAssocs)
			{
				RegisterProtocolFileAssoc(fileType);
			}

			// 6. 无感知刷新（不重启资源管理器）
			RefreshSystemAssociations(false);


			string msg = BrowserPicker.Resources.i18n.CsConfigRegistrationSuccessful;
			string Completed = BrowserPicker.Resources.i18n.UniCompleted;

			MessageBox.Show(msg, Completed, MessageBoxButton.OK, MessageBoxImage.Information);
		}
		catch (UnauthorizedAccessException ex)
		{
			string msg = BrowserPicker.Resources.i18n.CsConfigAdminTipText;
			string tip = BrowserPicker.Resources.i18n.CsConfigAdminTip;
			MessageBox.Show($"{msg}{ex.Message}", tip, MessageBoxButton.OK, MessageBoxImage.Warning);
		}
		catch (Exception ex)
		{
			string msg = BrowserPicker.Resources.i18n.CsConfigRegistrationFailed;
			string error = BrowserPicker.Resources.i18n.UniError;
			MessageBox.Show($"{msg}{ex.Message}", error, MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	// 移除按钮点击事件
	private void UnregisterOpenWith_Click(object sender, RoutedEventArgs e)
	{

		string msg = BrowserPicker.Resources.i18n.CsConfigUnregisterTip;
		string remove = BrowserPicker.Resources.i18n.UniRemove;
		var result = MessageBox.Show(msg, remove, MessageBoxButton.YesNo, MessageBoxImage.Warning);

		if (result != MessageBoxResult.Yes) return;

		try
		{
			// 1. 删除文件类型关联
			foreach (var ext in _fileTypes)
			{
				DeleteFileTypeAssoc(ext);
			}

			// 2. 删除URL协议关联
			foreach (var proto in _protocols)
			{
				DeleteProtocolAssoc(proto);
				DeleteProtocolContextMenu(proto);
			}

			// 3. 删除系统URL文件类型关联
			foreach (var fileType in _protocolFileAssocs)
			{
				DeleteProtocolFileAssoc(fileType);
			}

			// 4. 删除默认程序注册
			DeleteDefaultProgramReg();

			// 5. 删除自定义ProgID
			DeleteProgId(ProgIdHtml);
			DeleteProgId(ProgIdUrl);

			// 6. 无感知刷新
			RefreshSystemAssociations(false);


			string msg1 = BrowserPicker.Resources.i18n.CsConfigUnregisterSuccessful;
			string Completed = BrowserPicker.Resources.i18n.UniCompleted;
			MessageBox.Show(msg1, Completed, MessageBoxButton.OK, MessageBoxImage.Information);
		}
		catch (Exception ex)
		{
			string msg1 = BrowserPicker.Resources.i18n.CsConfigUnregisterFailed;
			string error = BrowserPicker.Resources.i18n.UniError;
			MessageBox.Show($"{msg1}{ex.Message}", error,
				MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	// 文件类型注册
	private void RegisterFileType(string extension, string progId)
	{
		string msg = BrowserPicker.Resources.i18n.CsConfigFileTypeRegistration;
		if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(progId))
			throw new ArgumentNullException(msg);

		// 仅操作 CurrentUser\Software\Classes\扩展名
		using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}"))
		{
			string msg1 = BrowserPicker.Resources.i18n.CsConfigFileTypeRegistrationSuccessful;
			extKey.SetValue("", progId, RegistryValueKind.String);
			extKey.SetValue("FriendlyTypeName", $"{AppName} - {msg}{extension}", RegistryValueKind.String);

			// OpenWithList（右键打开方式核心）
			using (var openWithList = extKey.CreateSubKey(@"OpenWithList\" + AppName))
			{
				openWithList.SetValue("", _appPath, RegistryValueKind.String);
			}

			// OpenWithProgids（系统应用列表识别项）
			using (var openWithProgids = extKey.CreateSubKey("OpenWithProgids"))
			{
				openWithProgids.SetValue(progId, string.Empty, RegistryValueKind.String);
			}

			// DefaultIcon（标记为有效应用，避免被过滤）
			using (var iconKey = extKey.CreateSubKey("DefaultIcon"))
			{
				iconKey.SetValue("", $"\"{_appPath}\",0", RegistryValueKind.String);
			}
		}

		// 操作 Explorer\FileExts（文件管理器关联）
		using (var fileExtKey = Registry.CurrentUser.CreateSubKey(
			$@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}"))
		{
			// OpenWithList（字母键关联）
			using (var openWithKey = fileExtKey.CreateSubKey("OpenWithList"))
			{
				char keyChar = 'a';
				while (openWithKey.GetValue(keyChar.ToString()) != null && keyChar <= 'z') keyChar++;
				if (keyChar <= 'z')
				{
					openWithKey.SetValue(keyChar.ToString(), _appExeName, RegistryValueKind.String);
					string mruList = openWithKey.GetValue("MRUList")?.ToString() ?? "";
					openWithKey.SetValue("MRUList", keyChar + mruList.Trim(keyChar), RegistryValueKind.String);
				}
			}

			// OpenWithProgids（补充关联）
			using (var progIdsKey = fileExtKey.CreateSubKey("OpenWithProgids"))
			{
				progIdsKey.SetValue(progId, string.Empty, RegistryValueKind.String);
			}
		}
	}

	// 默认程序注册
	private void RegisterAsDefaultProgram()
	{
		// 路径1：CurrentUser\Software\Classes\Applications\{EXE名}
		using (var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{_appExeName}"))
		{
			appKey.SetValue("FriendlyAppName", AppName, RegistryValueKind.String);
			appKey.SetValue("ApplicationIcon", $"\"{_appPath}\",0", RegistryValueKind.String);
			appKey.SetValue("NoOpenWith", string.Empty, RegistryValueKind.String); // 允许打开方式关联

			// SupportedTypes（明确支持的类型）
			using (var supportedTypesKey = appKey.CreateSubKey("SupportedTypes"))
			{
				supportedTypesKey.SetValue(".htm", string.Empty);
				supportedTypesKey.SetValue(".html", string.Empty);
				supportedTypesKey.SetValue(".url", string.Empty);
				supportedTypesKey.SetValue("http", string.Empty);
				supportedTypesKey.SetValue("https", string.Empty);
			}

			// 功能描述
			using (var capabilitiesKey = appKey.CreateSubKey("Capabilities"))
			{
				string msg = BrowserPicker.Resources.i18n.CsConfigDefaultProgramRegistration;
				capabilitiesKey.SetValue("ApplicationDescription", $"{AppName} - {msg}", RegistryValueKind.String);
				capabilitiesKey.SetValue("ApplicationName", AppName, RegistryValueKind.String);

				// 文件关联
				using (var fileAssocKey = capabilitiesKey.CreateSubKey("FileAssociations"))
				{
					fileAssocKey.SetValue(".htm", ProgIdHtml, RegistryValueKind.String);
					fileAssocKey.SetValue(".html", ProgIdHtml, RegistryValueKind.String);
					fileAssocKey.SetValue(".url", ProgIdUrl, RegistryValueKind.String);
				}

				// URL协议关联
				using (var urlAssocKey = capabilitiesKey.CreateSubKey("URLAssociations"))
				{
					urlAssocKey.SetValue("http", ProgIdUrl, RegistryValueKind.String);
					urlAssocKey.SetValue("https", ProgIdUrl, RegistryValueKind.String);
				}
			}

			// 打开命令
			using (var shellKey = appKey.CreateSubKey(@"shell\open"))
			{
				string msg = BrowserPicker.Resources.i18n.CsConfigProgramRegistrationOpen;
				shellKey.SetValue("FriendlyName", $"{msg} {AppName} ", RegistryValueKind.String);
				shellKey.SetValue("Icon", $"\"{_appPath}\",0", RegistryValueKind.String);
				using (var cmdKey = shellKey.CreateSubKey("command"))
				{
					cmdKey.SetValue("", $"\"{_appPath}\" \"%1\"", RegistryValueKind.String);
				}
			}
		}

		// 路径2：CurrentUser\Software\RegisteredApplications
		using (var regAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
		{
			regAppsKey.SetValue(AppName, $@"Software\Classes\Applications\{_appExeName}\Capabilities", RegistryValueKind.String);
		}
	}

	// URL协议注册（绕开权限限制）
	private void RegisterUrlProtocol(string protocol, string progId)
	{
		if (string.IsNullOrEmpty(protocol) || string.IsNullOrEmpty(progId))
			throw new ArgumentNullException("The protocol or ProgID cannot be empty");

		using (var protoKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}"))
		{
			protoKey.SetValue("", $"{AppName} - {protocol}Agreement", RegistryValueKind.String);
			protoKey.SetValue("URL Protocol", string.Empty, RegistryValueKind.String);
			protoKey.SetValue("EditFlags", 0x00000002, RegistryValueKind.DWord);
			protoKey.SetValue("AlwaysShowExt", "yes", RegistryValueKind.String); // 强制显示

			// OpenWithProgids（核心关联项）
			using (var openWithProgids = protoKey.CreateSubKey("OpenWithProgids"))
			{
				openWithProgids.SetValue(progId, string.Empty, RegistryValueKind.String);
				openWithProgids.SetValue(_appExeName, string.Empty, RegistryValueKind.String);
			}

			// 打开命令（优先级高）
			using (var cmdKey = protoKey.CreateSubKey(@"shell\open\command"))
			{
				cmdKey.SetValue("", $"\"{_appPath}\" \"%1\"", RegistryValueKind.String);
				cmdKey.SetValue(AppName, $"\"{_appPath}\" \"%1\"", RegistryValueKind.String);
			}

			// OpenWithList（补充关联）
			using (var openWithList = protoKey.CreateSubKey(@"OpenWithList"))
			{
				openWithList.SetValue("", _appExeName, RegistryValueKind.String);
				openWithList.SetValue(AppName, _appPath, RegistryValueKind.String);
			}
		}
	}

	// 直接添加右键菜单
	private void AddProtocolContextMenu(string protocol)
	{
		string open = BrowserPicker.Resources.i18n.CsConfigProgramRegistrationOpen2;
		// 直接添加协议右键菜单
		using (var shellKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}\shell\{AppName}"))
		{
			shellKey.SetValue("", $"{open} {AppName} {protocol.ToUpper()}", RegistryValueKind.String);
			shellKey.SetValue("Position", "Top", RegistryValueKind.String); // 置顶
			shellKey.SetValue("Icon", $"\"{_appPath}\",0", RegistryValueKind.String);
		}

		using (var cmdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}\shell\{AppName}\command"))
		{
			cmdKey.SetValue("", $"\"{_appPath}\" \"%1\"", RegistryValueKind.String);
		}

		// 添加URL文件类型右键菜单
		foreach (var fileType in _protocolFileAssocs)
		{
			using (var shellKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{fileType}\shell\{AppName}_{protocol}"))
			{
				shellKey.SetValue("", $"{open} {AppName} {protocol.ToUpper()}", RegistryValueKind.String);
			}
			using (var cmdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{fileType}\shell\{AppName}_{protocol}\command"))
			{
				cmdKey.SetValue("", $"\"{_appPath}\" \"%1\"", RegistryValueKind.String);
			}
		}
	}

	// 关联系统URL文件类型
	private void RegisterProtocolFileAssoc(string fileType)
	{
		using (var fileTypeKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{fileType}"))
		{
			// OpenWithList
			using (var openWithList = fileTypeKey.CreateSubKey(@"OpenWithList\" + AppName))
			{
				openWithList.SetValue("", _appPath, RegistryValueKind.String);
			}

			// OpenWithProgids
			using (var openWithProgids = fileTypeKey.CreateSubKey("OpenWithProgids"))
			{
				openWithProgids.SetValue(ProgIdUrl, string.Empty, RegistryValueKind.String);
			}
		}
	}

	// 创建ProgID（系统识别核心）
	private void CreateProgId(string progId, string description, bool isUrl)
	{
		using (var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
		{
			string open = BrowserPicker.Resources.i18n.UniOpen;
			progKey.SetValue("", $"{AppName} - {description}", RegistryValueKind.String);
			progKey.SetValue("FriendlyTypeName", $"{AppName} - {open}{description}", RegistryValueKind.String);
			if (isUrl) progKey.SetValue("URL Protocol", string.Empty, RegistryValueKind.String);

			// 打开命令
			using (var cmdKey = progKey.CreateSubKey(@"shell\open\command"))
			{
				cmdKey.SetValue("", $"\"{_appPath}\" \"%1\"", RegistryValueKind.String);
			}

			// 默认图标
			using (var iconKey = progKey.CreateSubKey("DefaultIcon"))
			{
				iconKey.SetValue("", $"\"{_appPath}\",0", RegistryValueKind.String);
			}
		}
	}

	// 所有删除方法保持普通用户兼容
	private void DeleteProtocolContextMenu(string protocol)
	{
		var shellKeyPath = $@"Software\Classes\{protocol}\shell\{AppName}";
		Registry.CurrentUser.DeleteSubKeyTree(shellKeyPath, throwOnMissingSubKey: false);

		foreach (var fileType in _protocolFileAssocs)
		{
			var fileTypeShellPath = $@"Software\Classes\{fileType}\shell\{AppName}_{protocol}";
			Registry.CurrentUser.DeleteSubKeyTree(fileTypeShellPath, throwOnMissingSubKey: false);
		}
	}

	private void DeleteProtocolFileAssoc(string fileType)
	{
		var fileTypeKeyPath = $@"Software\Classes\{fileType}";
		using (var fileTypeKey = Registry.CurrentUser.OpenSubKey(fileTypeKeyPath, writable: true))
		{
			if (fileTypeKey != null)
			{
				fileTypeKey.DeleteSubKeyTree(@"OpenWithList\" + AppName, throwOnMissingSubKey: false);
				using (var openWithProgids = fileTypeKey.OpenSubKey("OpenWithProgids", writable: true))
				{
					openWithProgids?.DeleteValue(ProgIdUrl, throwOnMissingValue: false);
				}
			}
		}
	}

	private void DeleteFileTypeAssoc(string extension)
	{
		var extKeyPath = $@"Software\Classes\{extension}";
		using (var extKey = Registry.CurrentUser.OpenSubKey(extKeyPath, writable: true))
		{
			if (extKey != null)
			{
				extKey.DeleteSubKeyTree(@"OpenWithList\" + AppName, throwOnMissingSubKey: false);
				extKey.DeleteSubKeyTree("DefaultIcon", throwOnMissingSubKey: false);
				extKey.DeleteValue(ProgIdHtml, throwOnMissingValue: false);
				extKey.DeleteValue(ProgIdUrl, throwOnMissingValue: false);
				extKey.DeleteValue("FriendlyTypeName", throwOnMissingValue: false);
			}
		}

		var fileExtKeyPath = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}";
		using (var fileExtKey = Registry.CurrentUser.OpenSubKey(fileExtKeyPath, writable: true))
		{
			if (fileExtKey != null)
			{
				using (var openWithKey = fileExtKey.OpenSubKey("OpenWithList", writable: true))
				{
					if (openWithKey != null)
					{
						foreach (var valueName in openWithKey.GetValueNames())
						{
							if (string.Equals(openWithKey.GetValue(valueName)?.ToString(), _appExeName, StringComparison.OrdinalIgnoreCase))
							{
								openWithKey.DeleteValue(valueName);
								var mruList = openWithKey.GetValue("MRUList")?.ToString() ?? "";
								if (mruList.Contains(valueName))
								{
									openWithKey.SetValue("MRUList", mruList.Replace(valueName, ""));
								}
								break;
							}
						}
					}
				}
				using (var progIdsKey = fileExtKey.OpenSubKey("OpenWithProgids", writable: true))
				{
					progIdsKey?.DeleteValue(ProgIdHtml, throwOnMissingValue: false);
					progIdsKey?.DeleteValue(ProgIdUrl, throwOnMissingValue: false);
				}
			}
		}
	}

	private void DeleteProtocolAssoc(string protocol)
	{
		var protoKeyPath = $@"Software\Classes\{protocol}";
		using (var protoKey = Registry.CurrentUser.OpenSubKey(protoKeyPath, writable: true))
		{
			if (protoKey != null)
			{
				protoKey.DeleteSubKeyTree(@"OpenWithList\" + AppName, throwOnMissingSubKey: false);
				protoKey.DeleteSubKeyTree(@"shell\open\command", throwOnMissingSubKey: false);
				protoKey.DeleteSubKeyTree("OpenWithProgids", throwOnMissingSubKey: false);
				protoKey.DeleteValue(ProgIdUrl, throwOnMissingValue: false);
				protoKey.DeleteValue("EditFlags", throwOnMissingValue: false);
				protoKey.DeleteValue("AlwaysShowExt", throwOnMissingValue: false);
			}
		}
	}

	private void DeleteDefaultProgramReg()
	{
		var appKeyPath = $@"Software\Classes\Applications\{_appExeName}";
		Registry.CurrentUser.DeleteSubKeyTree(appKeyPath, throwOnMissingSubKey: false);

		using (var regAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", writable: true))
		{
			regAppsKey?.DeleteValue(AppName, throwOnMissingValue: false);
		}
	}

	private void DeleteProgId(string progId)
	{
		var progIdPath = $@"Software\Classes\{progId}";
		Registry.CurrentUser.DeleteSubKeyTree(progIdPath, throwOnMissingSubKey: false);
	}

	// 无感知刷新（不重启资源管理器）
	private void RefreshSystemAssociations(bool forceRestart = false)
	{
		// 强制刷新缓存
		SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);

		// 仅当用户主动选择时才重启（默认不重启）
		if (forceRestart)
		{
			string restart = BrowserPicker.Resources.i18n.UniRestart;
			string msg = BrowserPicker.Resources.i18n.CsConfigRestartExplorerTip;
			var confirmResult = MessageBox.Show(msg, restart, MessageBoxButton.YesNo, MessageBoxImage.Question);

			if (confirmResult == MessageBoxResult.Yes)
			{
				try
				{
					Process.Start(new ProcessStartInfo("cmd.exe", "/c taskkill /f /im explorer.exe && start explorer.exe")
					{
						CreateNoWindow = true,
						WindowStyle = ProcessWindowStyle.Hidden
					});
				}
				catch
				{
					string tip = BrowserPicker.Resources.i18n.UniTip;
					string msg1 = BrowserPicker.Resources.i18n.CsConfigRestartExplorerFailTip;
					MessageBox.Show(msg1, tip, MessageBoxButton.OK, MessageBoxImage.Warning);
				}
			}
		}
	}

	private void CheckBox_Checked(object sender, System.Windows.RoutedEventArgs e) { }

	private void OpenDefaultAppsSettings_Click(object sender, RoutedEventArgs e)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = "ms-settings:defaultapps",
			UseShellExecute = true
		});
	}
}
