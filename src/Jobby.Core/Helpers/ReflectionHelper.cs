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

    public static Type? TryGetCommandTypeFromHandlerType(Type t)
    {
        if (t.IsAbstract)
        {
            return null;
        }

        for (var typeToCheck = t;  typeToCheck != null; typeToCheck = typeToCheck.BaseType)
        {
            var handlerInterface = typeToCheck
                .GetInterfaces()
                .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IJobCommandHandler<>));

            if (handlerInterface != null)
            {
                return handlerInterface.GetGenericArguments().First();
            }
        }

        return null;
    }
}
