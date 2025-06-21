using System.ComponentModel;
using System.Diagnostics;

namespace WindowCloser;

internal sealed class ServiceUtils {
	private const int WAIT_FOR_EXIT = 5000;

	public const string APP_DESCRIPTION = "Periodically closes windows based on configurable conditions.";

	private static readonly string APP_NAME = $"{Introspect.AppName}";
	private static readonly string APP_PATH = $"{Introspect.AppPath}";
	private static readonly string SERVICE_NAME = $"{APP_NAME}Service";

	// TODO: interactive services are verboten according to microsoft. see if this still works without type=interact
	private static readonly string[] INSTALL_ARGS = [
		"create",
		$"{SERVICE_NAME}",
		$"binpath=\"{APP_PATH}\" run-service",
		$"displayname={APP_NAME}",
		"type=interact",
		"type=own",
		"start=auto",
	];

	private static readonly string[] DESCRIPTION_ARGS = [
		"description",
		$"{SERVICE_NAME}",
		$"{APP_DESCRIPTION}",
	];

	private static readonly string[] UNINSTALL_ARGS = [
		"delete",
		$"{SERVICE_NAME}",
	];

	private static readonly string[] START_ARGS = [
		"start",
		$"{SERVICE_NAME}",
	];

	private static readonly string[] STOP_ARGS = [
		"stop",
		$"{SERVICE_NAME}",
		"4:5:256",
		"Manual stop.",
	];

	/// <summary>
	/// Starts sc.exe with arguments.
	/// </summary>
	/// <param name="args">The arguments to start sc.exe with.</param>
	/// <returns>True if sc.exe was started successfully, false otherwise.</returns>
	public static bool ServiceControl(string[] args) {
		var processInfo = new ProcessStartInfo("sc.exe", args) {
			UseShellExecute = true,
			Verb = "runas",
		};

		try {
			if (Process.Start(processInfo) is Process proc)
				proc.WaitForExit(WAIT_FOR_EXIT);
		} catch (Win32Exception e) {
			if (WindowUtils.GetErrorMessageForWin32Code(e.NativeErrorCode) is string errorMessage)
				Console.WriteLine($"{errorMessage}");
			else
				Console.WriteLine($"{e.Message}");

			return false;
		}

		return true;
	}

	private static void PrintArgsMessage(string[] args, bool start = false, bool stop = false) {
		if (!Environment.UserInteractive)
			return;

		var pluralS = start || stop || args == INSTALL_ARGS ? "s" : "";
		var message = $"This will try to run the following command{pluralS} with elevated privileges.\nYou may see multiple UAC prompts.";
		var index = 0;

		if (stop)
			message += $"\n{++index}) sc.exe {string.Join(" ", STOP_ARGS)}";

		message += $"\n{++index}) sc.exe {string.Join(" ", args)}";
		if (args == INSTALL_ARGS)
			message += $"\n{++index}) sc.exe {string.Join(" ", DESCRIPTION_ARGS)}";

		if (start)
			message += $"\n{++index}) sc.exe {string.Join(" ", START_ARGS)}";

		message += "\nPress any key to continue or press Ctrl+C to exit.";
		Console.WriteLine(message);
		Console.ReadKey(true);
	}

	public static void InstallService(bool startNow) {
		PrintArgsMessage(INSTALL_ARGS, startNow);
		if (!ServiceControl(INSTALL_ARGS))
			return;

		if (!ServiceControl(DESCRIPTION_ARGS))
			return;

		if (startNow)
			StartService();
	}

	public static void UninstallService() {
		PrintArgsMessage(UNINSTALL_ARGS, stop: true);
		if (!ServiceControl(STOP_ARGS))
			return;

		ServiceControl(UNINSTALL_ARGS);
	}

	public static void StartService() {
		PrintArgsMessage(START_ARGS);
		ServiceControl(START_ARGS);
	}

	public static void StopService() {
		PrintArgsMessage(STOP_ARGS);
		ServiceControl(STOP_ARGS);
	}
}
