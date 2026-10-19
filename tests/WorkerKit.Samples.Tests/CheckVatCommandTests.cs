using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkerKit.Hosting.Cli;
using WorkerKit.Samples.Vies;
using WorkerKit.Testing;

namespace WorkerKit.Samples.Tests;

public sealed class CheckVatCommandTests
{
    [Fact]
    public async Task Command_args_return_success_when_the_number_is_valid()
    {
        var checker = new ScriptChecker(valid: true);
        var command = new CheckVatCommand(checker);
        var args = TestCommandArgs.With(CheckVatCommand.CountryCode, "de").Set(CheckVatCommand.VatNumber, "DE123456789");

        var code = await command.ExecuteAsync(args, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(("DE", "123456789"), checker.Calls[0]);
    }

    [Fact]
    public async Task Hosted_command_returns_an_error_when_the_service_says_invalid()
    {
        await using var tester = WorkerAppTester.Create<CheckVatCommand>()
            .ConfigureApp(app => app.AddVies())
            .ConfigureServices(services =>
            {
                services.RemoveAll<IViesVatChecker>();
                services.AddSingleton<IViesVatChecker>(new ScriptChecker(valid: false));
            })
            .Build();

        var result = await tester.RunCommandAsync("check-vat", "--country-code", "FR", "--vat-number", "12345678901");

        Assert.Equal(ExitCodes.Error, result.ExitCode);
    }

    [Fact]
    public async Task Missing_country_is_a_usage_error_and_does_not_call_vies()
    {
        var checker = new ScriptChecker(valid: true);
        await using var tester = WorkerAppTester.Create<CheckVatCommand>()
            .ConfigureApp(app => app.AddVies())
            .ConfigureServices(services =>
            {
                services.RemoveAll<IViesVatChecker>();
                services.AddSingleton<IViesVatChecker>(checker);
            })
            .Build();

        var result = await tester.RunCommandAsync("check-vat", "--vat-number", "123456789");

        Assert.NotEqual(ExitCodes.Success, result.ExitCode);
        Assert.Empty(checker.Calls);
    }

    private sealed class ScriptChecker(bool valid) : IViesVatChecker
    {
        public List<(string Country, string Number)> Calls { get; } = [];

        public Task<ViesVatResult> CheckAsync(string countryCode, string vatNumber, CancellationToken cancellationToken)
        {
            var country = ViesVatNumber.NormalizeCountry(countryCode);
            var number = ViesVatNumber.NormalizeNumber(country, vatNumber);
            Calls.Add((country, number));
            return Task.FromResult(new ViesVatResult(country, number, valid, "Example", "Street", DateTime.UtcNow));
        }
    }
}
