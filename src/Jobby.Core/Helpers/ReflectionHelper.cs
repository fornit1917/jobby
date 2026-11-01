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

        // An explicit interface implementation of the static abstract GetJobName
        // is a private method named after the fully qualified interface member
        // ('Namespace.IJobCommand.GetJobName'), so search for both the implicit and explicit forms.
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var getJobNameMethod = t.GetMethods(flags).FirstOrDefault(m =>
            m.Name == nameof(IJobCommand.GetJobName) ||
            m.Name.EndsWith($"IJobCommand.{nameof(IJobCommand.GetJobName)}", StringComparison.Ordinal));

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
