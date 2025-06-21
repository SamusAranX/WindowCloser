using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace WindowCloser.LogFormatting;

[SuppressMessage("Style", "IDE0072:Add missing cases")]
[SuppressMessage("ReSharper", "SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault")]
internal sealed class SimplerConsoleFormatter : ConsoleFormatter, IDisposable {
	private const string LOGLEVEL_PADDING = ": ";
	private static readonly string MESSAGE_PADDING = new(' ', GetLogLevelString(LogLevel.Information).Length + LOGLEVEL_PADDING.Length);
	private static readonly string NEW_LINE_WITH_MESSAGE_PADDING = Environment.NewLine + MESSAGE_PADDING;
	private readonly IDisposable? _optionsReloadToken;

	public SimplerConsoleFormatter(IOptionsMonitor<SimplerConsoleFormatterOptions> options) : base("Simpler") {
		this.ReloadLoggerOptions(options.CurrentValue);
		this._optionsReloadToken = options.OnChange(this.ReloadLoggerOptions);
	}

	[MemberNotNull(nameof(FormatterOptions))]
	private void ReloadLoggerOptions(SimplerConsoleFormatterOptions options) { this.FormatterOptions = options; }

	public void Dispose() { this._optionsReloadToken?.Dispose(); }

	internal SimplerConsoleFormatterOptions FormatterOptions { get; set; }

	public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter) {
		if (logEntry.State is BufferedLogRecord bufferedRecord) {
			var message = bufferedRecord.FormattedMessage ?? string.Empty;
			this.WriteInternal(null, textWriter, message, bufferedRecord.LogLevel, bufferedRecord.EventId.Id, bufferedRecord.Exception, logEntry.Category, bufferedRecord.Timestamp);
		} else {
			var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
			if (logEntry.Exception == null && string.IsNullOrWhiteSpace(message))
				return;

			// We extract most of the work into a non-generic method to save code size. If this was left in the generic
			// method, we'd get generic specialization for all TState parameters, but that's unnecessary.
			this.WriteInternal(scopeProvider, textWriter, message, logEntry.LogLevel, logEntry.EventId.Id, logEntry.Exception?.ToString(), logEntry.Category, this.GetCurrentDateTime());
		}
	}

	private void WriteInternal(IExternalScopeProvider? scopeProvider, TextWriter textWriter, string message, LogLevel logLevel, int eventId, string? exception, string category, DateTimeOffset stamp) {
		var logLevelColors = this.GetLogLevelConsoleColors(logLevel);
		var logLevelString = GetLogLevelString(logLevel);

		string? timestamp = null;
		var timestampFormat = this.FormatterOptions.TimestampFormat;
		if (timestampFormat != null)
			timestamp = stamp.ToString(timestampFormat, CultureInfo.InvariantCulture);

		if (timestamp != null)
			textWriter.Write(timestamp);

		textWriter.WriteColoredMessage(logLevelString, logLevelColors.Background, logLevelColors.Foreground);

		var singleLine = this.FormatterOptions.SingleLine;
		var includeCategory = this.FormatterOptions.IncludeCategory;

		if (includeCategory) {
			// Example:
			// info: ConsoleApp.Program[10]
			//       Request received

			// category and event id
			textWriter.Write(LOGLEVEL_PADDING);
			textWriter.Write(category);
			textWriter.Write('[');

			Span<char> span = stackalloc char[10];
			if (eventId.TryFormat(span, out var charsWritten, provider: CultureInfo.InvariantCulture))
				textWriter.Write(span[..charsWritten]);
			else
				textWriter.Write(eventId.ToString(CultureInfo.InvariantCulture));

			textWriter.Write(']');

			if (!singleLine)
				textWriter.Write(Environment.NewLine);
		}

		// scope information
		this.WriteScopeInformation(textWriter, scopeProvider, singleLine);
		WriteMessage(textWriter, message, singleLine);

		// Example:
		// System.InvalidOperationException
		//    at Namespace.Class.Function() in File:line X
		if (exception != null) {
			// exception message
			WriteMessage(textWriter, exception, singleLine);
		}

		if (singleLine)
			textWriter.Write(Environment.NewLine);
	}

	private static void WriteReplacing(TextWriter writer, string oldValue, string newValue, string message) {
		var newMessage = message.Replace(oldValue, newValue);
		writer.Write(newMessage);
	}

	private static void WriteMessage(TextWriter textWriter, string message, bool singleLine) {
		if (string.IsNullOrEmpty(message))
			return;

		if (singleLine) {
			textWriter.Write(' ');
			WriteReplacing(textWriter, Environment.NewLine, " ", message);
		} else {
			textWriter.Write(MESSAGE_PADDING);
			WriteReplacing(textWriter, Environment.NewLine, NEW_LINE_WITH_MESSAGE_PADDING, message);
			textWriter.Write(Environment.NewLine);
		}
	}

	private DateTimeOffset GetCurrentDateTime() {
		return this.FormatterOptions.TimestampFormat != null
			? this.FormatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now
			: DateTimeOffset.MinValue;
	}

	private static string GetLogLevelString(LogLevel logLevel) {
		var test = logLevel switch {
			LogLevel.Trace => "trce",
			LogLevel.Debug => "dbug",
			LogLevel.Information => "info",
			LogLevel.Warning => "warn",
			LogLevel.Error => "fail",
			LogLevel.Critical => "crit",
			_ => throw new ArgumentOutOfRangeException(nameof(logLevel)),
		};

		return test.ToUpperInvariant();
	}

	private ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel) {
		// We shouldn't be outputting color codes for Android/Apple mobile platforms,
		// they have no shell (adb shell is not meant for running apps) and all the output gets redirected to some log file.
		var disableColors = this.FormatterOptions.ColorBehavior == LoggerColorBehavior.Disabled || (this.FormatterOptions.ColorBehavior == LoggerColorBehavior.Default && !ConsoleUtils.EmitAnsiColorCodes);
		if (disableColors)
			return new ConsoleColors(null, null);

		// We must explicitly set the background color if we are setting the foreground color,
		// since just setting one can look bad on the users console.
		return logLevel switch {
			LogLevel.Trace => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
			LogLevel.Debug => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
			LogLevel.Information => new ConsoleColors(ConsoleColor.DarkGreen, ConsoleColor.Black),
			LogLevel.Warning => new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black),
			LogLevel.Error => new ConsoleColors(ConsoleColor.Black, ConsoleColor.DarkRed),
			LogLevel.Critical => new ConsoleColors(ConsoleColor.White, ConsoleColor.DarkRed),
			_ => new ConsoleColors(null, null),
		};
	}

	private void WriteScopeInformation(TextWriter textWriter, IExternalScopeProvider? scopeProvider, bool singleLine) {
		if (!this.FormatterOptions.IncludeScopes || scopeProvider == null)
			return;

		var paddingNeeded = !singleLine;
		scopeProvider.ForEachScope((scope, state) => {
			if (paddingNeeded) {
				paddingNeeded = false;
				state.Write(MESSAGE_PADDING);
				state.Write("=> ");
			} else
				state.Write(" => ");

			state.Write(scope);
		}, textWriter);

		if (!paddingNeeded && !singleLine)
			textWriter.Write(Environment.NewLine);
	}

	private readonly struct ConsoleColors(ConsoleColor? foreground, ConsoleColor? background) {
		public ConsoleColor? Foreground { get; } = foreground;

		public ConsoleColor? Background { get; } = background;
	}
}
