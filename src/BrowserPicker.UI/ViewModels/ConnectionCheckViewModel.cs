using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BrowserPicker.Common;
using BrowserPicker.Common.Framework;

namespace BrowserPicker.UI.ViewModels;

public enum ConnectionCheckIndicatorState
{
	NotScanned,
	Good,
	Warning,
	Error,
	Unresolved,
}

public sealed class ConnectionCheckViewModel : ModelBase
{
	private readonly Uri? uri;
	private readonly bool include_certificate_records;
	private readonly bool skip_confirmation;
	private CancellationTokenSource? cancellation;
	private string status = "Ready";
	private string? reportText;
	private ConnectionCheckIndicatorState result_state;
	private bool has_summary;
	private bool is_running;
	private bool has_started;
	private bool can_start;

#if DEBUG
	public ConnectionCheckViewModel()
		: this(
			new Uri("https://github.com/mortenn/BrowserPicker"),
			includeCertificateRecords: true,
			skipConfirmation: false
		) { }
#endif

	public ConnectionCheckViewModel(Uri uri, bool includeCertificateRecords, bool skipConfirmation)
	{
		this.uri = uri;
		include_certificate_records = includeCertificateRecords;
		skip_confirmation = skipConfirmation;
		can_start = true;
		ConfirmText = includeCertificateRecords
			? $"Checking the TLS certificate will contact the target host ({uri.Host}) and may query DNS for CAA records. The website, DNS resolver, and network proxies may see this activity."
			: $"Checking the TLS certificate will contact only the target host ({uri.Host}). The website and network proxies may see this connection.";
	}

	public ConnectionCheckViewModel(string unavailableReason)
	{
		ConfirmText = unavailableReason;
		Status = "Cannot check this connection";
		ReportText = unavailableReason;
		HasStarted = true;
		can_start = false;
	}

	public event EventHandler? CloseRequested;

	public string ConfirmText { get; }

	public bool SkipConfirmation => skip_confirmation;

	public ConnectionCheckIndicatorState ResultState
	{
		get => result_state;
		private set => SetProperty(ref result_state, value);
	}

	public ObservableCollection<ConnectionCheckDetailViewModel> Details { get; } = [];

	public ObservableCollection<ConnectionCheckSectionViewModel> Sections { get; } = [];

	public ConnectionCheckSectionViewModel? OverviewSection =>
		Sections.FirstOrDefault(section => section.Title == "Overview");

	public IEnumerable<ConnectionCheckSectionViewModel> CollapsibleSections =>
		Sections.Where(section => section.Title != "Overview");

	public string Status
	{
		get => status;
		private set => SetProperty(ref status, value);
	}

	public string? ReportText
	{
		get => reportText;
		private set
		{
			if (SetProperty(ref reportText, value))
			{
				OnPropertyChanged(nameof(HasReportText));
			}
		}
	}

	public bool HasReportText => !string.IsNullOrWhiteSpace(ReportText);

	public bool HasSummary
	{
		get => has_summary;
		private set
		{
			if (SetProperty(ref has_summary, value))
			{
				OnPropertyChanged(nameof(OverviewSection));
				OnPropertyChanged(nameof(CollapsibleSections));
			}
		}
	}

	public bool IsRunning
	{
		get => is_running;
		private set
		{
			if (SetProperty(ref is_running, value))
			{
				start?.RaiseCanExecuteChanged();
				cancel?.RaiseCanExecuteChanged();
				close?.RaiseCanExecuteChanged();
			}
		}
	}

	public bool HasStarted
	{
		get => has_started;
		private set
		{
			if (SetProperty(ref has_started, value))
			{
				start?.RaiseCanExecuteChanged();
			}
		}
	}

	public ICommand Start => start ??= new DelegateCommand(() => _ = StartAsync(), () => can_start && !HasStarted);

	public ICommand Cancel => cancel ??= new DelegateCommand(CancelCheck, () => IsRunning);

	public ICommand Close => close ??= new DelegateCommand(RequestClose, () => !IsRunning);

	public void StartIfRequested()
	{
		if (SkipConfirmation)
		{
			_ = StartAsync();
		}
	}

	private async Task StartAsync()
	{
		if (uri == null || IsRunning)
		{
			return;
		}

		HasStarted = true;
		IsRunning = true;
		Status = "Checking connection...";
		ReportText = null;
		HasSummary = false;
		Details.Clear();
		Sections.Clear();
		cancellation = new CancellationTokenSource();

		try
		{
			var summary = await TlsCertificateSummary.InspectAsync(
				uri,
				include_certificate_records,
				cancellation.Token
			);
			Status = "Connection check complete";
			SetSummary(summary);
		}
		catch (OperationCanceledException)
		{
			Status = "Connection check canceled";
			ReportText = "The connection check was canceled or timed out.";
			ResultState = ConnectionCheckIndicatorState.NotScanned;
		}
		catch (SocketException ex)
		{
			Status = "Connection check failed";
			ReportText = $"The connection check failed: {ex.Message}";
			ResultState = IsDnsResolutionFailure(ex)
				? ConnectionCheckIndicatorState.Unresolved
				: ConnectionCheckIndicatorState.Error;
		}
		catch (Exception ex) when (ex is AuthenticationException or IOException or InvalidOperationException)
		{
			Status = "Connection check failed";
			ReportText = $"The connection check failed: {ex.Message}";
			ResultState = ConnectionCheckIndicatorState.Error;
		}
		finally
		{
			cancellation?.Dispose();
			cancellation = null;
			IsRunning = false;
		}
	}

	private void CancelCheck()
	{
		cancellation?.Cancel();
	}

	private void RequestClose()
	{
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	private void SetSummary(TlsCertificateSummary summary)
	{
		Details.Clear();
		Sections.Clear();
		AddSection(
			"Overview",
			string.Empty,
			[
				new ConnectionCheckDetailViewModel("Target", summary.Uri.ToString()),
				new ConnectionCheckDetailViewModel("Validation", summary.ValidationText),
				new ConnectionCheckDetailViewModel("Host match", summary.HostMatchText),
				new ConnectionCheckDetailViewModel("Chain", summary.ChainText),
			]
		);
		AddSection(
			"Certificate",
			FormatCertificateSummary(summary),
			[
				new ConnectionCheckDetailViewModel("Issuer", summary.Issuer),
				new ConnectionCheckDetailViewModel("Subject", summary.Subject),
				new ConnectionCheckDetailViewModel("Common name", summary.CommonName),
				new ConnectionCheckDetailViewModel("SAN DNS names", summary.SubjectAlternativeNamesText),
				new ConnectionCheckDetailViewModel("Valid from", summary.ValidFromText),
				new ConnectionCheckDetailViewModel("Expires", summary.ExpiresText),
			]
		);
		AddSection(
			"Encryption",
			FormatCipherSummary(summary.CipherStrengthText),
			[
				new ConnectionCheckDetailViewModel("Protocol", summary.ProtocolText),
				new ConnectionCheckDetailViewModel("Cipher strength", summary.CipherStrengthText),
				new ConnectionCheckDetailViewModel("Cipher suite", summary.CipherSuiteText),
			]
		);
		AddSection(
			"Transparency",
			FormatRecordsSummary(summary.CertificateAuthorityAuthorizationText, summary.CertificateTransparencyText),
			[
				new ConnectionCheckDetailViewModel("CAA", summary.CertificateAuthorityAuthorizationText),
				new ConnectionCheckDetailViewModel("Certificate transparency", summary.CertificateTransparencyText),
			]
		);
		ReportText = null;
		ResultState = ClassifySummary(summary);
		HasSummary = true;
	}

	private void AddSection(string title, string summary, IEnumerable<ConnectionCheckDetailViewModel> details)
	{
		var rows = details.Where(detail => !string.IsNullOrWhiteSpace(detail.Value)).ToArray();
		if (rows.Length > 0)
		{
			Sections.Add(new ConnectionCheckSectionViewModel(title, summary, rows));
			foreach (var row in rows)
			{
				Details.Add(row);
			}
		}
	}

	private static string FormatCertificateSummary(TlsCertificateSummary summary)
	{
		var now = DateTimeOffset.Now;
		if (summary.ValidFrom > now)
		{
			return "Not valid yet";
		}

		return summary.Expires <= now.AddDays(30) ? "Expiring" : "Valid";
	}

	private static string FormatCipherSummary(string? cipherStrength)
	{
		if (string.IsNullOrWhiteSpace(cipherStrength))
		{
			return "Unknown";
		}

		var end = cipherStrength.IndexOfAny(['.', '(']);
		return end > 0 ? cipherStrength[..end].Trim() : cipherStrength;
	}

	private static string FormatRecordsSummary(string caa, string ct)
	{
		if (caa == "Aligned" && ct.StartsWith("Looks normal", StringComparison.OrdinalIgnoreCase))
		{
			return "Aligned";
		}

		if (caa == "Not checked" || ct == "Not checked")
		{
			return "Not checked";
		}

		return caa == "None" ? "No CAA" : "Review";
	}

	private static ConnectionCheckIndicatorState ClassifySummary(TlsCertificateSummary summary)
	{
		if (
			summary.PolicyErrors != System.Net.Security.SslPolicyErrors.None
			|| !summary.HostNameMatchesCertificate
			|| summary.ChainStatus.Count > 0
			|| IsBadCipher(summary.CipherStrengthText)
			|| summary.CertificateAuthorityAuthorizationText == "Unaligned"
		)
		{
			return ConnectionCheckIndicatorState.Error;
		}

		if (
			FormatCertificateSummary(summary) != "Valid"
			|| IsIncompleteTransparency(
				summary.CertificateAuthorityAuthorizationText,
				summary.CertificateTransparencyText
			)
			|| IsUnknownCipher(summary.CipherStrengthText)
		)
		{
			return ConnectionCheckIndicatorState.Warning;
		}

		return ConnectionCheckIndicatorState.Good;
	}

	private static bool IsBadCipher(string? cipherStrength)
	{
		return StartsWithAny(cipherStrength, "Insecure", "Weak");
	}

	private static bool IsUnknownCipher(string? cipherStrength)
	{
		return StartsWithAny(cipherStrength, "Unknown", "Not post-quantum");
	}

	private static bool IsIncompleteTransparency(string caa, string ct)
	{
		return caa is "None" or "Not checked"
			|| ct == "Not checked"
			|| ct.StartsWith("No embedded", StringComparison.OrdinalIgnoreCase)
			|| ct.StartsWith("Inconclusive", StringComparison.OrdinalIgnoreCase);
	}

	private static bool StartsWithAny(string? value, params string[] prefixes)
	{
		return !string.IsNullOrWhiteSpace(value)
			&& prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsDnsResolutionFailure(SocketException ex)
	{
		return ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain;
	}

	private DelegateCommand? start;
	private DelegateCommand? cancel;
	private DelegateCommand? close;
}

public sealed record ConnectionCheckDetailViewModel(string Label, string? Value);

public sealed record ConnectionCheckSectionViewModel(
	string Title,
	string Summary,
	IReadOnlyList<ConnectionCheckDetailViewModel> Details
);
