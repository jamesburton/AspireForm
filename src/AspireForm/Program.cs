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

    config.AddCommand<PlanCommand>("plan")
        .WithDescription("Show the reconciliation diff between desired and current state.");

    config.AddCommand<ApplyCommand>("apply")
        .WithDescription("Execute the plan after an approval gate.");

    config.AddCommand<DestroyCommand>("destroy")
        .WithDescription("Destroy one block (or all blocks when no argument is supplied).");
});

return await app.RunAsync(args);
