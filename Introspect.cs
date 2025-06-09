using System.Diagnostics;

namespace WindowCloser {
	internal sealed class Introspect {
		public static readonly string AppPath = $"{Path.Join(AppContext.BaseDirectory, Path.GetFileName(Environment.GetCommandLineArgs()[0]))}";

		public static readonly string AppName = FileVersionInfo.GetVersionInfo(AppPath).FileDescription!;
	}

}
