using AwesomeAssertions;

namespace BrowserPicker.Common.Tests;

/// <summary>
/// Regression coverage for issue #299: WPF crashes during startup with a
/// <see cref="UriFormatException"/> inside <c>MS.Internal.FontCache.Util</c> when BrowserPicker is
/// launched (e.g. by the Codex Azure DevOps MCP auth flow) with a stripped environment that has no
/// <c>windir</c> variable.
///
/// These tests mutate a process-global environment variable, so they share a non-parallel collection.
/// </summary>
[Collection(WindowsEnvironmentCollection.Name)]
public sealed class WindowsEnvironmentTests
{
	private const string FontsSuffix = @"\Fonts\";

	/// <summary>Reproduces the exact operation WPF performs, demonstrating why a missing windir crashes startup.</summary>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void EmptyWindirProducesTheInvalidFontsUriThatCrashesWpf(string windir)
	{
		var fontsPath = windir + FontsSuffix;

		var buildFontsUri = () => new Uri(fontsPath, UriKind.Absolute);

		buildFontsUri.Should().Throw<UriFormatException>();
	}

	[Fact]
	public void EnsureWindowsDirectoryRestoresAValidValueWhenMissing()
	{
		WithWindir(string.Empty, () =>
		{
			var resolved = WindowsEnvironment.EnsureWindowsDirectory();

			resolved.Should().NotBeNullOrWhiteSpace();
			Environment.GetEnvironmentVariable(WindowsEnvironment.WindowsDirectoryVariable)
				.Should()
				.Be(resolved);

			// The whole point of the fix: WPF can now build the fonts URI without throwing.
			var buildFontsUri = () => new Uri(resolved + FontsSuffix, UriKind.Absolute);
			buildFontsUri.Should().NotThrow();
		});
	}

	[Fact]
	public void EnsureWindowsDirectoryLeavesAnExistingValueUntouched()
	{
		const string existing = @"C:\Windows";

		WithWindir(existing, () =>
		{
			var resolved = WindowsEnvironment.EnsureWindowsDirectory();

			resolved.Should().Be(existing);
			Environment.GetEnvironmentVariable(WindowsEnvironment.WindowsDirectoryVariable)
				.Should()
				.Be(existing);
		});
	}

	/// <summary>
	/// The end-to-end regression test: clear windir like the crashing host does, apply the fix, then
	/// initialize the very WPF font subsystem that threw in issue #299 and assert it no longer crashes.
	/// </summary>
	[Fact]
	public void WpfFontSubsystemInitializesAfterWindirIsRestored()
	{
		WithWindir(string.Empty, () =>
		{
			WindowsEnvironment.EnsureWindowsDirectory();

			var failure = TouchWpfFontSubsystem();

			failure.Should().BeNull("WPF font initialization must not crash once windir is restored");
		});
	}

	/// <summary>
	/// Touches the WPF font types from the crash stack on a dedicated STA thread so the
	/// <c>MS.Internal.FontCache.Util</c> static constructor runs against the current environment.
	/// </summary>
	private static Exception? TouchWpfFontSubsystem()
	{
		Exception? failure = null;
		var thread = new Thread(() =>
		{
			try
			{
				_ = new System.Windows.Media.FontFamily("Segoe UI").FamilyNames;
				_ = System.Windows.SystemFonts.MessageFontFamily;
			}
			catch (Exception exception)
			{
				failure = exception;
			}
		});
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();
		return failure;
	}

	private static void WithWindir(string? value, Action body)
	{
		var original = Environment.GetEnvironmentVariable(WindowsEnvironment.WindowsDirectoryVariable);
		try
		{
			Environment.SetEnvironmentVariable(
				WindowsEnvironment.WindowsDirectoryVariable,
				value,
				EnvironmentVariableTarget.Process
			);
			body();
		}
		finally
		{
			Environment.SetEnvironmentVariable(
				WindowsEnvironment.WindowsDirectoryVariable,
				original,
				EnvironmentVariableTarget.Process
			);
		}
	}
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WindowsEnvironmentCollection
{
	public const string Name = "WindowsEnvironment";
}
