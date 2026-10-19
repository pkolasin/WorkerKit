namespace Contoso.Billing;

public sealed class SampleHandler(ICommandOutput output)
{
    public Task<int> ExecuteAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(id))
        {
            output.WriteError("--id is required.");
            return Task.FromResult(1);
        }

        output.WriteLine(id.Trim());
        return Task.FromResult(0);
    }
}

public sealed class SampleCommand(SampleHandler handler) : IWorkerCommand
{
    public static CommandOption<string> ItemId { get; } = new("--id", "Id of the sample item.")
    {
        Required = true,
    };

    public static CommandSpec Spec { get; } = new("sample", "Print one sample item id.")
    {
        ItemId,
    };

    public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
    {
        return handler.ExecuteAsync(args.Get(ItemId) ?? "", cancellationToken);
    }
}
