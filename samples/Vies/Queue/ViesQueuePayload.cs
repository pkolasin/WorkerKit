namespace WorkerKit.Samples.Vies.Queue;

/// <summary>One VIES lookup queued for <see cref="ViesQueueWorker"/>.</summary>
public sealed record ViesQueuePayload(string CountryCode, string VatNumber);
