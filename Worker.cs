using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace WindowCloser {
	public class Worker(IOptionsMonitor<Settings> settings, ILogger<Worker> logger) : BackgroundService {
		private readonly IOptionsMonitor<Settings> _settings = settings;
		private readonly ILogger<Worker> _logger = logger;

		private void LogSuccess(WindowInfo windowInfo) {
			this._logger.LogInformation("Closed window for \"{FancyName}\"!", windowInfo.FancyName);
		}

		private void LogLastError(WindowInfo windowInfo) {
			var lastError = Marshal.GetLastWin32Error();
			var lastErrorString = $"0x{lastError:08X}";
			var lastErrorMessage = WindowUtils.GetErrorMessageForWin32Code(lastError);
			this._logger.LogError("Error while closing window for \"{FancyName}\": {ErrorMessage} ({LastError})", windowInfo.FancyName, lastErrorMessage, lastErrorString);
		}

		private void DoThing(List<WindowInfo> windowInfos) {
			foreach (var info in windowInfos) {
				if (info.Multiple) {
					foreach (var windowHandle in WindowUtils.FindManyWindows(info)) {
						if (WindowUtils.CloseWindow(windowHandle))
							this.LogSuccess(info);
						else
							this.LogLastError(info);
					}
				} else {
					if (WindowUtils.FindOneWindow(info) is HWND windowHandle) {
						if (WindowUtils.CloseWindow(windowHandle))
							this.LogSuccess(info);
						else
							this.LogLastError(info);
					}
				}
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			while (!stoppingToken.IsCancellationRequested) {
				var settings = this._settings.CurrentValue;

				try {
					this._logger.LogInformation("Doing Thing at {Time}", DateTimeOffset.Now);
					this.DoThing(settings.Windows);

					var delay = (int)Math.Round(Math.Max(1, settings.Interval)) * 1000;
					this._logger.LogDebug("Waiting {Interval}s…", delay);
					await Task.Delay(delay, stoppingToken);
				} catch (TaskCanceledException) {
					Console.WriteLine("exception");
				}
				
			}
		}
	}
}
