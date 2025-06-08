using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace WindowCloser {
	public class Worker(ILogger<Worker> logger) : BackgroundService {
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
			var builder = new ConfigurationBuilder();
			builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
			var config = builder.Build();

			var settings = new Settings();
			config.Bind(settings);

			this._logger.LogInformation("Config: {DebugView}", config.GetDebugView());
			this._logger.LogInformation("Interval: {Interval}", settings.Interval);
			this._logger.LogInformation("Windows: {Windows}", settings.Windows);

			var delay = (int)Math.Round(settings.Interval * 1000);
			while (!stoppingToken.IsCancellationRequested) {
				if (this._logger.IsEnabled(LogLevel.Information)) {
					this._logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
					this.DoThing(settings.Windows);
				}
				await Task.Delay(delay, stoppingToken);
			}
		}
	}
}
