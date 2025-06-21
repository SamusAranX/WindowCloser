namespace WindowCloser.LogFormatting;

internal static class ConsoleLoggerExtensions {
	public static ILoggingBuilder AddSimplerConsole(this ILoggingBuilder builder, Action<SimplerConsoleFormatterOptions> configure) {
		builder.AddConsole(options => options.FormatterName = "Simpler");
		builder.AddConsoleFormatter<SimplerConsoleFormatter, SimplerConsoleFormatterOptions>(configure);
		return builder;
	}
}
