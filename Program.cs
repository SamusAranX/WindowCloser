using System.CommandLine;
using System.Text;
using WindowCloser;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Console.OutputEncoding = Encoding.UTF8;

static void RunWorker(string[] args) {
	var builder = Host.CreateApplicationBuilder(args);
	builder
		.Configuration
		.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
	builder.Services.Configure<Settings>(builder.Configuration.GetSection("Settings"));
	builder.Services.AddHostedService<Worker>();

	var host = builder.Build();
	host.Run();
}

var startNowOption = new Option<bool>(
	"--start-now",
	description: "Starts the service after installing it.",
	getDefaultValue: () => false
);

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
runServiceCommand.SetHandler(() => RunWorker(args));

var root = new RootCommand {
	Description = ServiceUtils.APP_DESCRIPTION
};
root.SetHandler(() => root.Invoke("-h"));

root.Add(installCommand);
root.Add(uninstallCommand);
root.Add(startCommand);
root.Add(stopCommand);
root.Add(runServiceCommand);

return root.Invoke(args);