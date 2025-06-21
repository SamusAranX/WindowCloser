namespace WindowCloser.LogFormatting;

internal static class TextWriterExtensions {
	public static void WriteColoredMessage(this TextWriter textWriter, string message, ConsoleColor? background, ConsoleColor? foreground) {
		// Order: backgroundcolor, foregroundcolor, Message, reset foregroundcolor, reset backgroundcolor
		if (background.HasValue) {
			textWriter.Write(AnsiParser.GetBackgroundColorEscapeCode(background.Value));
		}

		if (foreground.HasValue) {
			textWriter.Write(AnsiParser.GetForegroundColorEscapeCode(foreground.Value));
		}

		textWriter.Write(message);
		if (foreground.HasValue) {
			textWriter.Write(AnsiParser.DEFAULT_FOREGROUND_COLOR); // reset to default foreground color
		}

		if (background.HasValue) {
			textWriter.Write(AnsiParser.DEFAULT_BACKGROUND_COLOR); // reset to the background color
		}
	}
}
