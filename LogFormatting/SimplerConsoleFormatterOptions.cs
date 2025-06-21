using Microsoft.Extensions.Logging.Console;

namespace WindowCloser.LogFormatting;

internal sealed class SimplerConsoleFormatterOptions : SimpleConsoleFormatterOptions {
	/// <summary>
	/// Gets or sets a value that indicates whether the message category is logged.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if the message category is logged.
	/// </value>
	public bool IncludeCategory { get; set; }
}