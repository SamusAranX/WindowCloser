using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Windows.Win32.Foundation;

namespace WindowCloser;

public class Worker(IOptionsMonitor<Settings> settingsMonitor, ILogger<Worker> logger) : BackgroundService {
	private readonly ConcurrentDictionary<(uint processID, uint threadID, HWND windowHandle), Task> _windowClosingTasks = [];

	private void DoThing(List<WindowInfo> windowInfos) {
		var settings = settingsMonitor.CurrentValue;
		var closeTimeout = (int)Math.Round(Math.Max(0, settings.CloseTimeout)) * 1000;
		var killWait = (int)Math.Round(Math.Max(0, settings.KillWait)) * 1000;

		foreach (var info in windowInfos) {
			List<HWND> handles = [];

			if (info.Multiple)
				handles.AddRange(WindowUtils.FindManyWindows(info));
			else if (WindowUtils.FindOneWindow(info) is HWND handle)
				handles.Add(handle);

			logger.LogDebug("Found {Num} handles for \"{FancyName}\"", handles.Count, info.FancyName);

			// task time
			foreach (var handle in handles) {
				if (!WindowUtils.GetWindowThreadProcessId(handle, out var ids)) {
					logger.LogError("Hiccup while processing window for \"{FancyName}\": Invalid window handle {Handle}", info.FancyName, handle);
					continue;
				}

				var dictKey = (ids.processID, ids.threadID, handle);
				if (this._windowClosingTasks.ContainsKey(dictKey)) {
					logger.LogDebug("Found existing Task for \"{FancyName}\", {DictKey}", info.FancyName, dictKey);
					continue; // window is already being worked on
				}

				var task = new Task(() => {
					logger.LogDebug("Started Task for \"{FancyName}\", {DictKey}", info.FancyName, dictKey);

					try {
						WindowUtils.CloseWindowEx(handle, closeTimeout, killWait, logger);
						logger.LogInformation("Closed window for \"{FancyName}\"!", info.FancyName);
					} catch (CloseWindowException e) {
						logger.LogError("Error closing window for \"{FancyName}\": {Message}", info.FancyName, e.Message);
					}

					if (this._windowClosingTasks.TryRemove(dictKey, out _)) {
						logger.LogDebug("Removed task entry for \"{FancyName}\", {DictKey}", info.FancyName, dictKey);
						return;
					}

					logger.LogCritical("Couldn't remove task entry for \"{FancyName}\", {DictKey}", info.FancyName, dictKey);
					throw new InvalidOperationException("critical program error");
				});

				if (!this._windowClosingTasks.TryAdd(dictKey, task)) {
					logger.LogCritical("Couldn't add task entry for \"{FancyName}\", {DictKey}", info.FancyName, dictKey);
					throw new InvalidOperationException("critical program error");
				}

				logger.LogDebug("Added task entry for \"{FancyName}\", {DictKey}", info.FancyName, dictKey);
				task.Start();
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