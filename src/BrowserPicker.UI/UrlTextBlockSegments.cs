using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BrowserPicker.Common;

namespace BrowserPicker.UI;

public static class UrlTextBlockSegments
{
	public static readonly DependencyProperty SegmentsProperty = DependencyProperty.RegisterAttached(
		"Segments",
		typeof(IEnumerable<UrlDisplaySegment>),
		typeof(UrlTextBlockSegments),
		new PropertyMetadata(null, OnSegmentsChanged)
	);

	// ReSharper disable once UnusedMember.Global
	public static void SetSegments(DependencyObject element, IEnumerable<UrlDisplaySegment>? value) =>
		element.SetValue(SegmentsProperty, value);

	// ReSharper disable once UnusedMember.Global
	public static IEnumerable<UrlDisplaySegment>? GetSegments(DependencyObject element) =>
		(IEnumerable<UrlDisplaySegment>?)element.GetValue(SegmentsProperty);

	private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TextBlock textBlock)
		{
			return;
		}

		textBlock.Inlines.Clear();
		if (e.NewValue is not IEnumerable<UrlDisplaySegment> segments)
		{
			return;
		}

		foreach (var segment in segments)
		{
			var run = new Run(segment.Text);
			ApplyStyle(textBlock, run, segment);
			textBlock.Inlines.Add(run);
		}
	}

	private static void ApplyStyle(TextBlock textBlock, Run run, UrlDisplaySegment segment)
	{
		switch (segment.Kind)
		{
			case UrlDisplaySegmentKind.Scheme:
				run.Foreground = GetSchemeBrush(segment.Text);
				break;
			case UrlDisplaySegmentKind.RegistrableDomain:
			case UrlDisplaySegmentKind.FileRoot:
			case UrlDisplaySegmentKind.FileName:
				run.Foreground = GetUrlBarBrush(textBlock);
				run.FontWeight = FontWeights.SemiBold;
				break;
			case UrlDisplaySegmentKind.NonAsciiHost:
				run.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x4B, 0x00));
				run.FontWeight = FontWeights.SemiBold;
				run.TextDecorations = TextDecorations.Underline;
				break;
			case UrlDisplaySegmentKind.Path:
			case UrlDisplaySegmentKind.Query:
			case UrlDisplaySegmentKind.Fragment:
				run.Foreground = new SolidColorBrush(Color.FromRgb(0x5C, 0x62, 0x68));
				break;
			default:
				run.Foreground = GetUrlBarBrush(textBlock);
				break;
		}
	}

	private static Brush GetSchemeBrush(string text)
	{
		if (text.StartsWith("https:", System.StringComparison.OrdinalIgnoreCase))
		{
			return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
		}

		if (text.StartsWith("http:", System.StringComparison.OrdinalIgnoreCase))
		{
			return new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20));
		}

		return new SolidColorBrush(Color.FromRgb(0x5C, 0x62, 0x68));
	}

	private static Brush GetUrlBarBrush(TextBlock textBlock)
	{
		return textBlock.TryFindResource(App.UrlBarForegroundBrushKey) as Brush ?? textBlock.Foreground;
	}
}
