using System.CommandLine;
using System.Globalization;
using System.Text;
using WindowCloser;
using WindowCloser.LogFormatting;
using Version = WindowCloser.Version;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Console.OutputEncoding = Encoding.UTF8;

const string TITLE = """
                      _ _ _ _       _           _____ _                 
                     | | | |_|___ _| |___ _ _ _|     | |___ ___ ___ ___ 
                     | | | | |   | . | . | | | |   --| | . |_ -| -_|  _|
                     |_____|_|_|_|___|___|_____|_____|_|___|___|___|_|
                     ---------------------------------------------------
                     """;
const string VERSION_STRING = $"{Version.GIT_VERSION} [{Version.GIT_BRANCH}, {Version.GIT_COMMIT_SHORT}]";
const string BUILDTIME_STRING = $"Build Time: {Version.BUILD_TIME}";

var startNowOption = new Option<bool>(
	"--start-now",
	"Starts the service after installing it."
);

var minimizeOption = new Option<bool>(
	"--minimize",
	"Minimizes the console window after starting, if possible."
);
minimizeOption.AddAlias("-M");

var installCommand = new Command("install", "Installs the service.");
installCommand.AddOption(startNowOption);
installCommand.SetHandler(ServiceUtils.InstallService, startNowOption);

var uninstallCommand = new Command("uninstall", "Stops the service and then uninstalls it.");
uninstallCommand.SetHandler(ServiceUtils.UninstallService);

var startCommand = new Command("start", "Starts the service.");
startCommand.SetHandler(ServiceUtils.StartService);

var stopCommand = new Command("stop", "Stops the service.");
stopCommand.SetHandler(ServiceUtils.StopService);

var runServiceCommand = new Command("run-service", "Runs the service worker. (This is what runs in the background)");
runServiceCommand.AddOption(minimizeOption);
runServiceCommand.SetHandler(RunWorker, minimizeOption);

var root = new RootCommand {
	Description = ServiceUtils.APP_DESCRIPTION,
};
root.SetHandler(() => root.Invoke("-h"));

root.Add(installCommand);
root.Add(uninstallCommand);
root.Add(startCommand);
root.Add(stopCommand);
root.Add(runServiceCommand);

return root.Invoke(args);

static void RunWorker(bool minimize) {
	var appSettingsPath = Path.Join(AppContext.BaseDirectory, "appsettings.json");
	var builder = Host.CreateApplicationBuilder();
	builder
		.Configuration
		.AddJsonFile(appSettingsPath, false, true);
	builder.Services.Configure<Settings>(builder.Configuration.GetSection("Settings"));
	builder.Services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);
	builder.Services.AddHostedService<Worker>();
	builder.Logging.ClearProviders();
	builder.Logging.AddSimplerConsole(o => {
		o.IncludeCategory = false;
		o.SingleLine = true;
		var currentCultureDateTimeFormat = CultureInfo.CurrentCulture.DateTimeFormat;
		var datePattern = currentCultureDateTimeFormat.ShortDatePattern;
		var timePattern = currentCultureDateTimeFormat.LongTimePattern;
		o.TimestampFormat = $"[{datePattern} {timePattern}] ";
	});

	var lastTitleLine = TITLE.TrimEnd().Split('\n').Last();
	Console.WriteLine(TITLE);
	Console.WriteLine(VERSION_STRING.PadBoth(lastTitleLine.Length, '-'));
	Console.WriteLine(BUILDTIME_STRING.PadBoth(lastTitleLine.Length, '-'));
	Console.WriteLine(lastTitleLine);

	if (minimize)
		WindowUtils.MinimizeConsole();

	var host = builder.Build();
	host.Run();
}
