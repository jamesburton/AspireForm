using AspireForm.Cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("aspireform");

    config.AddCommand<ConfigCommand>("config")
        .WithAlias("show")
        .WithDescription("Print the fully merged and interpolated desired-state configuration.");

    config.AddCommand<DoctorCommand>("doctor")
        .WithDescription("Check that AspireForm's prerequisites are installed.");
});

return await app.RunAsync(args);
