using Jobby.Core.Interfaces;

namespace Jobby.Tests.Dashboard;

// A serializer of the application's own with a format that is not JSON: the whole text is the recipient.
internal sealed class RecipientTextSerializer : IJobParamSerializer
{
    public string SerializeJobParam<T>(T command) where T : IJobCommand =>
        ((SendReportCommand)(object)command).Recipient;

    public object? DeserializeJobParam(string? json, Type jobParamType) =>
        json is null ? null : new SendReportCommand { Recipient = json };
}
