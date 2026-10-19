using Contoso.Billing;

var app = WorkerApp.CreateBuilder<Program>(args, options => options.EnvPrefix = "CONTOSOBILLING");
app.Commands.Add<SampleCommand>();
app.ConfigureHost(builder => builder.AddContosoBillingConnector());
return await app.RunAsync();
