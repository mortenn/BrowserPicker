using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;

namespace BrowserPicker.Common;

public sealed record TlsCertificateSummary(
	Uri Uri,
	string Host,
	int Port,
	string Subject,
	string Issuer,
	DateTimeOffset ValidFrom,
	DateTimeOffset Expires,
	string? CommonName,
	IReadOnlyList<string> SubjectAlternativeNames,
	bool HostNameMatchesCertificate,
	SslPolicyErrors PolicyErrors,
	IReadOnlyList<string> ChainStatus,
	SslProtocols Protocol,
	TlsCipherSuite? CipherSuite,
	bool CertificateRecordsChecked,
	IReadOnlyList<string> CaaRecords,
	string CertificateTransparencyStatus
)
{
	private const int DefaultHttpsPort = 443;
	private const int DefaultTimeoutMilliseconds = 5000;
	private const string EmbeddedSctOid = "1.3.6.1.4.1.11129.2.4.2";

	public string ValidationText => FormatValidation();

	public string HostMatchText => $"{(HostNameMatchesCertificate ? "Yes" : "No")} ({Host})";

	public string ValidFromText => $"{ValidFrom.LocalDateTime:g}";

	public string ExpiresText => $"{Expires.LocalDateTime:g}";

	public string? SubjectAlternativeNamesText =>
		SubjectAlternativeNames.Count == 0 ? null : FormatNames(SubjectAlternativeNames);

	public string? ProtocolText => Protocol == SslProtocols.None ? null : Protocol.ToString();

	public string? CipherStrengthText => CipherSuite == null ? null : FormatCipherStrength();

	public string? CipherSuiteText => CipherSuite?.ToString();

	public string ChainText => FormatChainStatus();

	public string CertificateAuthorityAuthorizationText =>
		CertificateRecordsChecked ? FormatCaaStatus() : "Not checked";

	public string CertificateTransparencyText =>
		CertificateRecordsChecked ? CertificateTransparencyStatus : "Not checked";

	public static bool TryCreateTarget(string? targetUrl, out Uri uri, out string errorMessage)
	{
		if (string.IsNullOrWhiteSpace(targetUrl))
		{
			uri = null!;
			errorMessage = "No URL is available to check.";
			return false;
		}

		if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out uri!))
		{
			errorMessage = "This is not an absolute URL, so there is no TLS host to check.";
			return false;
		}

		if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			errorMessage =
				$"TLS certificate checks are only available for HTTPS URLs. This URL uses {uri.Scheme.ToUpperInvariant()}.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(uri.Host))
		{
			errorMessage = "This URL does not include a host name to check.";
			return false;
		}

		errorMessage = string.Empty;
		return true;
	}

	public static Task<TlsCertificateSummary> InspectAsync(Uri uri, CancellationToken cancellationToken) =>
		InspectAsync(uri, includeCertificateRecords: true, cancellationToken);

	public static Task<TlsCertificateSummary> InspectAsync(
		Uri uri,
		bool includeCertificateRecords,
		CancellationToken cancellationToken
	) =>
		InspectAsync(
			uri,
			includeCertificateRecords,
			TimeSpan.FromMilliseconds(DefaultTimeoutMilliseconds),
			cancellationToken
		);

	public static async Task<TlsCertificateSummary> InspectAsync(
		Uri uri,
		bool includeCertificateRecords,
		TimeSpan timeout,
		CancellationToken cancellationToken
	)
	{
		if (!TryCreateTarget(uri.ToString(), out var targetUri, out var errorMessage))
		{
			throw new ArgumentException(errorMessage, nameof(uri));
		}

		using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutSource.CancelAfter(timeout);

		using var client = new TcpClient();
		await client.ConnectAsync(targetUri.Host, GetPort(targetUri), timeoutSource.Token).ConfigureAwait(false);

		await using var networkStream = client.GetStream();
		X509Certificate2? certificate = null;
		var policyErrors = SslPolicyErrors.None;
		var chainStatus = Array.Empty<string>();

		using var sslStream = new SslStream(
			networkStream,
			leaveInnerStreamOpen: false,
			(_, remoteCertificate, chain, errors) =>
			{
				policyErrors = errors;
				certificate = remoteCertificate == null ? null : new X509Certificate2(remoteCertificate);
				chainStatus =
				[
					.. chain?.ChainStatus.Select(status =>
						string.IsNullOrWhiteSpace(status.StatusInformation)
							? status.Status.ToString()
							: $"{status.Status}: {status.StatusInformation.Trim()}"
					)
						?? [],
				];
				return true;
			}
		);

		await sslStream
			.AuthenticateAsClientAsync(
				new SslClientAuthenticationOptions
				{
					TargetHost = targetUri.Host,
					CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
					EnabledSslProtocols = SslProtocols.None,
				},
				timeoutSource.Token
			)
			.ConfigureAwait(false);

		certificate ??= sslStream.RemoteCertificate == null ? null : new X509Certificate2(sslStream.RemoteCertificate);
		if (certificate == null)
		{
			throw new InvalidOperationException("The TLS handshake completed without a certificate.");
		}

		return FromCertificate(
			targetUri,
			certificate,
			policyErrors,
			chainStatus,
			sslStream.SslProtocol,
			sslStream.NegotiatedCipherSuite,
			includeCertificateRecords,
			includeCertificateRecords
				? await GetCaaRecordsAsync(targetUri.Host, timeoutSource.Token).ConfigureAwait(false)
				: [],
			includeCertificateRecords ? GetCertificateTransparencyStatus(certificate) : "Not checked"
		);
	}

	public static TlsCertificateSummary FromCertificate(
		Uri uri,
		X509Certificate2 certificate,
		SslPolicyErrors policyErrors,
		IReadOnlyList<string> chainStatus,
		SslProtocols protocol = SslProtocols.None,
		TlsCipherSuite? cipherSuite = null,
		bool certificateRecordsChecked = true,
		IReadOnlyList<string>? caaRecords = null,
		string? certificateTransparencyStatus = null
	)
	{
		var names = GetSubjectAlternativeDnsNames(certificate);
		var commonName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
		var matchNames = names.Count > 0 ? names : [commonName];

		return new TlsCertificateSummary(
			uri,
			uri.Host,
			GetPort(uri),
			certificate.Subject,
			certificate.Issuer,
			new DateTimeOffset(certificate.NotBefore),
			new DateTimeOffset(certificate.NotAfter),
			string.IsNullOrWhiteSpace(commonName) ? null : commonName,
			names,
			matchNames.Any(name => CertificateNameMatchesHost(name, uri.Host)),
			policyErrors,
			chainStatus,
			protocol,
			cipherSuite,
			certificateRecordsChecked,
			caaRecords ?? [],
			certificateRecordsChecked
				? certificateTransparencyStatus ?? GetCertificateTransparencyStatus(certificate)
				: certificateTransparencyStatus ?? "Not checked"
		);
	}

	public string ToDisplayText()
	{
		var builder = new StringBuilder()
			.AppendLine($"Connection check for {Uri}")
			.AppendLine()
			.AppendLine(
				CertificateRecordsChecked
					? $"Privacy: this check contacted the target host ({Host}:{Port}) and may have queried DNS for CAA records. The website, DNS resolver, and network proxies may see this activity."
					: $"Privacy: this check contacted only the target host ({Host}:{Port}). The website and network proxies may see the connection."
			)
			.AppendLine()
			.AppendLine($"Validation: {FormatValidation()}")
			.AppendLine($"Host match: {(HostNameMatchesCertificate ? "Yes" : "No")} ({Host})")
			.AppendLine($"Subject: {Subject}")
			.AppendLine($"Issuer: {Issuer}")
			.AppendLine($"Valid from: {ValidFrom.LocalDateTime:g}")
			.AppendLine($"Expires: {Expires.LocalDateTime:g}");

		if (!string.IsNullOrWhiteSpace(CommonName))
		{
			builder.AppendLine($"Common name: {CommonName}");
		}

		if (SubjectAlternativeNames.Count > 0)
		{
			builder.AppendLine($"SAN DNS names: {FormatNames(SubjectAlternativeNames)}");
		}

		if (Protocol != SslProtocols.None)
		{
			builder.AppendLine($"Protocol: {Protocol}");
		}

		if (CipherSuite != null)
		{
			builder.AppendLine($"Cipher strength: {FormatCipherStrength()}");
			builder.AppendLine($"Cipher suite: {CipherSuite}");
		}

		builder.AppendLine($"Chain: {FormatChainStatus()}");
		if (CertificateRecordsChecked)
		{
			builder.AppendLine($"CAA: {FormatCaaStatus()}");
			builder.AppendLine($"Certificate transparency: {CertificateTransparencyStatus}");
		}
		else
		{
			builder.AppendLine("CAA / certificate transparency: Not checked");
		}
		return builder.ToString().TrimEnd();
	}

	private string FormatValidation()
	{
		return PolicyErrors == SslPolicyErrors.None ? "Valid" : $"Problems found ({PolicyErrors})";
	}

	private string FormatChainStatus()
	{
		return ChainStatus.Count == 0 ? "OK" : string.Join("; ", ChainStatus);
	}

	private string FormatCaaStatus()
	{
		if (CaaRecords.Count == 0)
		{
			return "None";
		}

		return CaaRecords.Any(record => CaaRecordMatchesIssuer(record, Issuer)) ? "Aligned" : "Unaligned";
	}

	private string FormatCipherStrength()
	{
		var protocolValue = (int)Protocol;
		if (protocolValue is 12 or 48 or 192 or 768)
		{
			return "Insecure. This connection used an obsolete TLS protocol.";
		}

		if (CipherSuite == null)
		{
			return "Unknown. BrowserPicker could not read the negotiated cipher suite.";
		}

		var suite = CipherSuite.Value.ToString().ToUpperInvariant();
		if (ContainsAny(suite, "NULL", "EXPORT", "RC4", "3DES", "DES", "_CBC_", "_MD5"))
		{
			return "Weak. The negotiated cipher uses older cryptography.";
		}

		if (ContainsAny(suite, "MLKEM", "KYBER", "HYBRID"))
		{
			return "Strong. Modern encryption with a post-quantum key exchange signal.";
		}

		if (Protocol == SslProtocols.Tls13 || ContainsAny(suite, "_GCM_", "CHACHA20_POLY1305"))
		{
			return "Strong classical (not post-quantum). Modern TLS encryption; no post-quantum key exchange was reported.";
		}

		return "Not post-quantum. No obvious weak cipher was reported, but this is not a modern post-quantum TLS signal.";
	}

	private static bool ContainsAny(string value, params string[] tokens)
	{
		return tokens.Any(token => value.Contains(token, StringComparison.Ordinal));
	}

	private static int GetPort(Uri uri)
	{
		return uri.IsDefaultPort ? DefaultHttpsPort : uri.Port;
	}

	private static string FormatNames(IReadOnlyList<string> names)
	{
		const int maxNames = 12;
		if (names.Count <= maxNames)
		{
			return string.Join(", ", names);
		}

		return $"{string.Join(", ", names.Take(maxNames))}, and {names.Count - maxNames} more";
	}

	private static bool CertificateNameMatchesHost(string certificateName, string host)
	{
		if (string.IsNullOrWhiteSpace(certificateName) || string.IsNullOrWhiteSpace(host))
		{
			return false;
		}

		var normalizedName = certificateName.TrimEnd('.');
		var normalizedHost = host.TrimEnd('.');
		if (!normalizedName.StartsWith("*.", StringComparison.Ordinal))
		{
			return string.Equals(normalizedName, normalizedHost, StringComparison.OrdinalIgnoreCase);
		}

		var suffix = normalizedName[1..];
		return normalizedHost.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
			&& normalizedHost.Length > suffix.Length
			&& normalizedHost[..^suffix.Length].Count(c => c == '.') == 0;
	}

	private static bool CaaRecordMatchesIssuer(string record, string issuer)
	{
		if (!TryGetCaaIssuer(record, out var caaIssuer))
		{
			return false;
		}

		var normalizedIssuer = NormalizeCertificateAuthorityName(issuer);
		var normalizedCaaIssuer = NormalizeCertificateAuthorityName(caaIssuer);
		return normalizedCaaIssuer.Length > 0
			&& normalizedIssuer.Contains(normalizedCaaIssuer, StringComparison.Ordinal);
	}

	private static bool TryGetCaaIssuer(string record, out string issuer)
	{
		issuer = string.Empty;
		var fields = record.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
		if (fields.Length < 3 || !fields[1].StartsWith("issue", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		issuer = fields[2].Trim().Trim('"');
		var parameterStart = issuer.IndexOf(';', StringComparison.Ordinal);
		if (parameterStart >= 0)
		{
			issuer = issuer[..parameterStart];
		}

		return !string.IsNullOrWhiteSpace(issuer);
	}

	private static string NormalizeCertificateAuthorityName(string value)
	{
		var hostLabels = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
		if (hostLabels.Length > 1)
		{
			value = hostLabels[^2];
		}

		return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
	}

	private static IReadOnlyList<string> GetSubjectAlternativeDnsNames(X509Certificate2 certificate)
	{
		foreach (var extension in certificate.Extensions)
		{
			if (extension.Oid?.Value == "2.5.29.17")
			{
				return ReadSubjectAlternativeDnsNames(extension.RawData);
			}
		}

		return [];
	}

	private static async Task<IReadOnlyList<string>> GetCaaRecordsAsync(
		string host,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var client = new LookupClient();
			foreach (var name in EnumerateCaaLookupNames(host))
			{
				var result = await client
					.QueryAsync(name, QueryType.CAA, QueryClass.IN, cancellationToken)
					.ConfigureAwait(false);
				var records = result.Answers.CaaRecords().ToArray();
				if (records.Length > 0)
				{
					return
					[
						.. records.Select(record =>
							string.IsNullOrWhiteSpace(record.Value)
								? $"{record.Flags} {record.Tag}"
								: $"{record.Flags} {record.Tag} \"{record.Value}\""
						),
					];
				}
			}

			return [];
		}
		catch (DnsResponseException ex)
		{
			return [$"Lookup failed: {ex.Message}"];
		}
		catch (SocketException ex)
		{
			return [$"Lookup failed: {ex.Message}"];
		}
	}

	private static IEnumerable<string> EnumerateCaaLookupNames(string host)
	{
		var labels = host.TrimEnd('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
		for (var start = 0; start < labels.Length - 1; start++)
		{
			yield return string.Join('.', labels.Skip(start));
		}
	}

	private static string GetCertificateTransparencyStatus(X509Certificate2 certificate)
	{
		var extension = certificate.Extensions.FirstOrDefault(extension => extension.Oid?.Value == EmbeddedSctOid);
		if (extension == null)
		{
			return "No embedded SCT extension found";
		}

		var scts = ReadEmbeddedSignedCertificateTimestamps(extension.RawData);
		if (scts.Count == 0)
		{
			return "Inconclusive. The certificate has a transparency extension, but BrowserPicker could not read its timestamps.";
		}

		var newest = scts.Max(sct => sct.Timestamp);
		return $"Looks normal. The certificate includes {scts.Count} transparency timestamp{Pluralize(scts.Count)}, newest {newest:yyyy-MM-dd}.";
	}

	private static IReadOnlyList<SignedCertificateTimestamp> ReadEmbeddedSignedCertificateTimestamps(byte[] rawData)
	{
		var data = UnwrapSctExtension(rawData);
		if (data.Length < 2)
		{
			return [];
		}

		var listLength = BinaryPrimitives.ReadUInt16BigEndian(data[..2]);
		var end = Math.Min(data.Length, listLength + 2);
		var offset = 2;
		var scts = new List<SignedCertificateTimestamp>();
		while (offset + 2 <= end)
		{
			var sctLength = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
			offset += 2;
			if (offset + sctLength > end)
			{
				break;
			}

			if (TryReadSignedCertificateTimestamp(data.Slice(offset, sctLength), out var sct))
			{
				scts.Add(sct);
			}
			offset += sctLength;
		}

		return scts;
	}

	private static ReadOnlySpan<byte> UnwrapSctExtension(byte[] rawData)
	{
		try
		{
			var reader = new AsnReader(rawData, AsnEncodingRules.DER);
			var octets = reader.ReadOctetString();
			return octets;
		}
		catch (AsnContentException)
		{
			return rawData;
		}
	}

	private static bool TryReadSignedCertificateTimestamp(ReadOnlySpan<byte> data, out SignedCertificateTimestamp sct)
	{
		sct = default;
		const int minimumLength = 1 + 32 + 8 + 2 + 2 + 2;
		if (data.Length < minimumLength)
		{
			return false;
		}

		var offset = 0;
		offset++;
		offset += 32;
		var timestampMilliseconds = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset, 8));
		offset += 8;
		var extensionsLength = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
		offset += 2 + extensionsLength;
		if (offset + 4 > data.Length)
		{
			return false;
		}

		offset += 2;
		var signatureLength = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
		offset += 2;
		if (offset + signatureLength > data.Length)
		{
			return false;
		}

		sct = new SignedCertificateTimestamp(DateTimeOffset.FromUnixTimeMilliseconds((long)timestampMilliseconds));
		return true;
	}

	private static string Pluralize(int count) => count == 1 ? string.Empty : "s";

	private readonly record struct SignedCertificateTimestamp(DateTimeOffset Timestamp);

	private static IReadOnlyList<string> ReadSubjectAlternativeDnsNames(byte[] rawData)
	{
		var names = new List<string>();
		var reader = new AsnReader(rawData, AsnEncodingRules.DER);
		var sequence = reader.ReadSequence();
		var dnsNameTag = new Asn1Tag(TagClass.ContextSpecific, 2);

		while (sequence.HasData)
		{
			if (sequence.PeekTag().HasSameClassAndValue(dnsNameTag))
			{
				names.Add(sequence.ReadCharacterString(UniversalTagNumber.IA5String, dnsNameTag));
				continue;
			}

			sequence.ReadEncodedValue();
		}

		return names;
	}
}
