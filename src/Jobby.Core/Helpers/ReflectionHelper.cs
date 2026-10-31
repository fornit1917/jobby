using Jobby.Core.Interfaces;
using System.Reflection;

namespace Jobby.Core.Helpers;

internal static class ReflectionHelper
{
    public static string? TryGetJobNameByType(Type t)
    {
        if (!t.IsAssignableTo(typeof(IJobCommand)))
        {
            return null;
        }

        var getJobNameMethod = t.GetMethod(nameof(IJobCommand.GetJobName),
            BindingFlags.Static | BindingFlags.Public, Array.Empty<Type>());

        return getJobNameMethod?.Invoke(null, null) as string;
    }

    public static IReadOnlyList<Type> TryGetCommandTypesFromHandlerType(Type t)
    {
        if (t.IsAbstract)
        {
            return Array.Empty<Type>();
        }

        // Type.GetInterfaces() returns the full interface closure including interfaces
        // inherited from base classes, so a single scan is enough.
        // A handler class may implement IJobCommandHandler<> for several commands - return all of them.
        return t.GetInterfaces()
            .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IJobCommandHandler<>))
            .Select(x => x.GetGenericArguments()[0])
            .Distinct()
            .ToArray();
    }
}
