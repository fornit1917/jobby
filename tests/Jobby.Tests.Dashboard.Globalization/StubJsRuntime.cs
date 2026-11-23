using Microsoft.JSInterop;

namespace Jobby.Tests.Dashboard.Globalization;

// Answers JS calls with the value registered for their identifier (any other call returns the default)
// and remembers every call it received.
internal sealed class StubJsRuntime : IJSRuntime
{
    private readonly Dictionary<string, string?> _results = new();

    public List<(string Identifier, object?[]? Args)> Calls { get; } = [];

    public string? this[string identifier]
    {
        set => _results[identifier] = value;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Calls.Add((identifier, args));
        return new ValueTask<TValue>(
            _results.TryGetValue(identifier, out var result) ? (TValue)(object?)result! : default!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken,
        object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
