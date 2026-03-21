using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BrowserPicker.Common;

namespace BrowserPicker.UI;

/// <summary>
/// Renders structured log message segments as inline runs inside a <see cref="TextBlock"/>.
/// </summary>
public static class TextBlockInlineSegments
{
	public static readonly DependencyProperty SegmentsProperty =
		DependencyProperty.RegisterAttached(
			"Segments",
			typeof(IEnumerable<InMemoryLogSegment>),
			typeof(TextBlockInlineSegments),
			new PropertyMetadata(null, OnSegmentsChanged));

	// ReSharper disable once UnusedMember.Global
	public static void SetSegments(DependencyObject element, IEnumerable<InMemoryLogSegment>? value) =>
		element.SetValue(SegmentsProperty, value);

	// ReSharper disable once UnusedMember.Global
	public static IEnumerable<InMemoryLogSegment>? GetSegments(DependencyObject element) =>
		(IEnumerable<InMemoryLogSegment>?)element.GetValue(SegmentsProperty);

	private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TextBlock textBlock)
		{
			return;
		}

		textBlock.Inlines.Clear();
		if (e.NewValue is not IEnumerable<InMemoryLogSegment> segments)
		{
			return;
		}

		foreach (var segment in segments)
		{
			var run = new Run(segment.Text);
			if (segment.IsValue)
			{
				run.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xD0, 0x7A));
				run.FontWeight = FontWeights.SemiBold;
			}

			textBlock.Inlines.Add(run);
		}
	}
}
