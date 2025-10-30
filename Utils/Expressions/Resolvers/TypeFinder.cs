using System.Reflection;
using System.Runtime.CompilerServices;
using Utils.Mathematics;

namespace Utils.Expressions.Resolvers
{
    /// <summary>
    /// Default implementation of <see cref="ITypeFinder"/> that scans assemblies for types and extension methods.
    /// </summary>
    public class TypeFinder : ITypeFinder
    {
        private ParserOptions Options { get; }
        private Assembly[] Assemblies { get; }
        private IDictionary<(string name, Type[] genericArguments), Type> Cache { get; } = new Dictionary<(string name, Type[] genericArguments), Type> { };

        private IDictionary<string, Type> Types { get; } = new Dictionary<string, Type>();
        private IDictionary<Type, IList<MethodInfo>> ExtensionMethods = new Dictionary<Type, IList<MethodInfo>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeFinder"/> class.
        /// </summary>
        /// <param name="options">Parser options providing default type mappings.</param>
        /// <param name="namespaces">Namespaces whose public types should be indexed by simple name.</param>
        /// <param name="assemblies">Assemblies to scan; if <see langword="null"/> or empty the current domain assemblies are used.</param>
        public TypeFinder(ParserOptions options, string[] namespaces, Assembly[] assemblies)
        {
            namespaces ??= [];

            Options = options;
            this.Assemblies = (assemblies?.Length ?? 0) == 0
                ? ((AppDomain)typeof(string).GetTypeInfo().Assembly.GetType("System.AppDomain").GetRuntimeProperty("CurrentDomain").GetMethod.Invoke(null, [])).GetAssemblies()
                : assemblies;

            foreach (Type type in Assemblies.SelectMany(a => a.GetTypes().Where(t => t.IsPublic)))
            {
                if (type.Namespace is null) continue;
                Types[type.FullName] = type;
                if (namespaces.Contains(type.Namespace))
                {
                    Types[type.Name] = type;
                    if (type.IsSealed && type.IsAbstract)
                    {
                        foreach (MethodInfo m in type.GetRuntimeMethods().Where(m => m.IsStatic && m.GetParameters().Length > 0))
                        {
                            var param0 = m.GetParameters()[0];
                            if (param0.GetCustomAttribute<ExtensionAttribute>() is null) continue;
                            if (!ExtensionMethods.TryGetValue(param0.ParameterType, out var extensionMethodList))
                            {
                                extensionMethodList = new List<MethodInfo>();
                                ExtensionMethods.Add(param0.ParameterType.IsGenericType ? typeof(object) : param0.ParameterType, extensionMethodList);
                            }
                            extensionMethodList.Add(m);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public Type FindType(string name, Type[] genericArguments)
        {
            if (Options.DefaultTypes.TryGetValue(name, out Type defaultType)) return defaultType;
            if (Cache.TryGetValue((name, genericArguments), out var type)) return type;

            var genericPosition = name.LastIndexOf('<');
            if (genericPosition < 0) genericPosition = name.Length;

            var typeName = (genericArguments?.Length ?? 0) > 0
                ? name[..genericPosition] + "`" + genericArguments.Length
                : name;

            var result = InnerFindType(typeName, genericArguments);
            if (result is null) return null;
            if ((genericArguments?.Length ?? 0) > 0) result = result.MakeGenericType(genericArguments);
            Cache[(name, genericArguments)] = result;
            return result;
        }

        private Type InnerFindType(string name, Type[] genericArguments)
        {
            if (Types.TryGetValue(name, out var type))
            {
                if (CheckGenericArguments(type, genericArguments) >= 0) return type;
            }

            return null;
        }

        private static int CheckGenericArguments(Type t, Type[] genericArguments)
        {
            genericArguments ??= [];
            if (t.IsGenericTypeDefinition && !genericArguments.Any()) return -1;
            var typeGenericArguments = t.GetGenericArguments();
            if (genericArguments.Length != typeGenericArguments.Length) return -1;
            int result = 0;
            foreach (var arg in typeGenericArguments.Select((Argument, Index) => new { Argument, Index }))
            {
                var typeArgument = arg.Argument;
                var argument = genericArguments[arg.Index];
                if (typeArgument.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) && argument.GetConstructor(new Type[] { }) == null) return -1;
                if (typeArgument.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && !argument.IsClass) return -1;
                if (typeArgument.GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint) && argument.IsClass) return -1;
                var parameterDistance = 0;
                foreach (var parameter in typeArgument.GetGenericParameterConstraints())
                {
                    if (!parameter.IsAssignableFrom(argument)) continue;
                    parameterDistance = MathEx.Max(parameterDistance, ParserExtensions.GetTypeDistance(parameter, argument));
                }
                result += parameterDistance;

                if (!typeArgument.GetGenericParameterConstraints().All(c => c.IsAssignableFrom(argument))) return -1;
            }
            return result;
        }

        /// <inheritdoc />
        public MethodInfo[] FindExtensionMethods(Type extendedType, string name)
        {
            List<MethodInfo> result = new List<MethodInfo>();

            // récupère toutes les extensions qui peuvent s'appliquer sur le type ou les types de base
            for (Type type = extendedType; type != null; type = type.BaseType)
            {
                if (ExtensionMethods.TryGetValue(type, out var extensionMethods))
                {
                    result.AddRange(extensionMethods.Where(m => m.Name == name));
                }
            }
            //récupère toutes les extensions qui peuvent s'appliquer sur les interfaces
            foreach (var @interface in extendedType.GetInterfaces())
            {
                if (ExtensionMethods.TryGetValue(@interface, out var extensionMethods))
                {
                    result.AddRange(extensionMethods.Where(m => m.Name == name));
                }
            }

            return [.. result];
        }
    }
}
