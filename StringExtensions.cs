namespace WindowCloser;

public static class StringExtensions {
	public static string PadBoth(this string str, int length, char? paddingChar = null) {
		var spaces = length - str.Length;
		var padLeft = spaces / 2 + str.Length;

		if (paddingChar is char pad)
			return str.PadLeft(padLeft, pad).PadRight(length, pad);

		return str.PadLeft(padLeft).PadRight(length);
	}
}
