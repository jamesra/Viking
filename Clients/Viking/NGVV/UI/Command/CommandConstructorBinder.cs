using System;
using System.Linq;
using System.Reflection;

namespace Viking.UI.Commands
{
    /// <summary>
    /// Instantiates command types from a constructor-argument array.
    /// <see cref="Activator.CreateInstance(Type, object[])"/> matches by exact arity and
    /// runtime type, so it rejects optional trailing parameters, interface parameters given
    /// a concrete array, and boxed values destined for <c>T?</c>. The command queue uses
    /// this binder instead.
    /// </summary>
    public static class CommandConstructorBinder
    {
        /// <summary>
        /// Creates an instance of <paramref name="type"/> using the most specific constructor
        /// compatible with <paramref name="args"/>, filling omitted optional parameters from
        /// their defaults.
        /// </summary>
        /// <exception cref="MissingMethodException">No public or non-public instance constructor is compatible with the argument list.</exception>
        public static object Create(Type type, object[]? args)
        {
            args ??= [];
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var candidates = type.GetConstructors(flags)
                .Select(ctor => (ctor, parameters: ctor.GetParameters()))
                .Where(x => ConstructorCompatible(x.parameters, args))
                .OrderByDescending(x => CountExactTypeMatches(x.parameters, args))
                .ThenBy(x => x.parameters.Length)
                .ToList();

            if (candidates.Count == 0)
                throw new MissingMethodException($"Constructor on type '{type.FullName}' not found.");

            var (ctor, parameters) = candidates[0];
            return ctor.Invoke(InvokeArguments(parameters, args));
        }

        private static bool ConstructorCompatible(ParameterInfo[] parameters, object[] args)
        {
            int required = parameters.Count(p => !p.IsOptional && !p.HasDefaultValue);
            if (args.Length < required || args.Length > parameters.Length)
                return false;

            for (int i = 0; i < args.Length; i++)
            {
                if (!ArgumentCompatible(parameters[i].ParameterType, args[i]))
                    return false;
            }

            for (int i = args.Length; i < parameters.Length; i++)
            {
                if (!parameters[i].IsOptional && !parameters[i].HasDefaultValue)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// True when <paramref name="arg"/> can be passed to a parameter of
        /// <paramref name="paramType"/>, including null for reference/nullable types and a
        /// boxed non-nullable value for <c>T?</c> (object arrays box <c>long?</c> as <c>long</c>).
        /// </summary>
        private static bool ArgumentCompatible(Type paramType, object? arg)
        {
            if (arg is null)
                return !paramType.IsValueType || Nullable.GetUnderlyingType(paramType) is not null;

            if (paramType.IsInstanceOfType(arg))
                return true;

            Type? underlying = Nullable.GetUnderlyingType(paramType);
            return underlying is not null && underlying.IsInstanceOfType(arg);
        }

        private static int CountExactTypeMatches(ParameterInfo[] parameters, object[] args)
        {
            int count = 0;
            int n = Math.Min(parameters.Length, args.Length);
            for (int i = 0; i < n; i++)
            {
                if (args[i] is not null && parameters[i].ParameterType == args[i].GetType())
                    count++;
            }
            return count;
        }

        private static object?[] InvokeArguments(ParameterInfo[] parameters, object[] args)
        {
            if (args.Length == parameters.Length)
                return args;

            var invokeArgs = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < args.Length)
                    invokeArgs[i] = args[i];
                else if (parameters[i].HasDefaultValue)
                    invokeArgs[i] = parameters[i].DefaultValue;
                else
                    invokeArgs[i] = Type.Missing;
            }
            return invokeArgs;
        }
    }
}
