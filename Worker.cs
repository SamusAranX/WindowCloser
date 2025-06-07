using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace dieCloud {
	public class Worker(ILogger<Worker> logger) : BackgroundService {
		private readonly ILogger<Worker> _logger = logger;

		private const string PROCESS_NAME = "iCloudHome";
		private const string WINDOW_TITLE = "iCloud";
		private const string WINDOW_CLASS = "WinUIDesktopWin32WindowClass";

		/// <summary>
		/// Takes a window handle and runs GetWindowThreadProcessId on it.
		/// </summary>
		/// <param name="handle">The window handle.</param>
		/// <returns>A tuple consisting of (Process ID, Thread ID).</returns>
		private static unsafe (uint, uint) GetWindowThreadProcessId(HWND handle) {
			uint processID = 0;
			var threadID = PInvoke.GetWindowThreadProcessId(handle, &processID);
			return (processID, threadID);
		}

		private void ListWindows() {
			var allProcesses = Process.GetProcesses();
			var _ = PInvoke.EnumWindows((handle, _) => {
				if (!PInvoke.IsWindowVisible(handle))
					return true;

				var (processID, _) = GetWindowThreadProcessId(handle);
				var proc = allProcesses.FirstOrDefault(p => p.Id == processID);

				if (proc == null || proc.ProcessName != PROCESS_NAME)
					return true;

				var processName = proc.ProcessName;

				Span<char> windowTitleSpan = stackalloc char[256];
				PInvoke.GetWindowText(handle, windowTitleSpan);

				Span<char> classNameSpan = stackalloc char[256];
				PInvoke.GetClassName(handle, classNameSpan);

				var windowTitle = windowTitleSpan.ToString().TrimEnd('\0');
				var className = classNameSpan.ToString().TrimEnd('\0');

				if (windowTitle == WINDOW_TITLE && className == WINDOW_CLASS) {
					this._logger.LogInformation("Found iCloud Window! ({ProcessID}) {ProcessName}", processID, processName);
					if (PInvoke.PostMessage(handle, PInvoke.WM_CLOSE, 0, 0)) {
						this._logger.LogInformation("Closed iCloud Window!");
					} else {
						var lastError = Marshal.GetLastWin32Error();
						var lastErrorString = $"0x{lastError:08X}";
						this._logger.LogError("Error while closing window: {LastError}", lastErrorString);
					}
					return false;
				}

				return true;
			},
			new LPARAM(0));
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			while (!stoppingToken.IsCancellationRequested) {
				if (this._logger.IsEnabled(LogLevel.Information)) {
					this._logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
					this.ListWindows();
				}
				await Task.Delay(1000, stoppingToken);
			}
		}
	}
}
