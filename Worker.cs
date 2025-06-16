using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace WindowCloser;

public class Worker(IOptionsMonitor<Settings> settingsMonitor, ILogger<Worker> logger) : BackgroundService {
	private void LogSuccess(WindowInfo windowInfo) {
		logger.LogInformation("Closed window for \"{FancyName}\"!", windowInfo.FancyName);
	}

	private void LogLastError(WindowInfo windowInfo) {
		var lastError = Marshal.GetLastWin32Error();
		var lastErrorString = $"0x{lastError:08X}";
		var lastErrorMessage = WindowUtils.GetErrorMessageForWin32Code(lastError);
		logger.LogError("Error while closing window for \"{FancyName}\": {ErrorMessage} ({LastError})", windowInfo.FancyName, lastErrorMessage, lastErrorString);
	}

	private void DoThing(List<WindowInfo> windowInfos) {
		foreach (var info in windowInfos) {
			List<HWND> handles = [];
			if (info.Multiple)
				handles.AddRange(WindowUtils.FindManyWindows(info));
			else if (WindowUtils.FindOneWindow(info) is HWND handle)
				handles.Add(handle);

			foreach (var handle in handles) {
				if (WindowUtils.CloseWindow(handle))
					this.LogSuccess(info);
				else
					this.LogLastError(info);
			}
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		while (!stoppingToken.IsCancellationRequested) {
			var settings = settingsMonitor.CurrentValue;

			try {
				logger.LogDebug("Doing Thing at {Time}", DateTimeOffset.Now);
				this.DoThing(settings.Windows);

				var delay = (int)Math.Round(Math.Max(1, settings.Interval)) * 1000;
				logger.LogDebug("Waiting {Interval}s…", delay);
				await Task.Delay(delay, stoppingToken);
			} catch (TaskCanceledException) {
				Console.WriteLine("exception");
			}
		}
	}
}