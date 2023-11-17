using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Expressions.Resolvers;

public class DefaultResolver : IResolver
{
    protected ITypeFinder TypeFinder { get; }

    public Markers GenericMarkers = new('<', '>');
    public Markers ArrayMarkers = new('[', ']');

    public char NullableMarkChar { get; } = '?';

    public DefaultResolver(ITypeFinder typeFinder)
    {
        this.TypeFinder = typeFinder;
    }

    public Type ResolveType(string name) => ResolveType(name, null);

    public Type ResolveType(string name, Type[] genericParameters)
    {
        name = name.Trim();
        if (string.IsNullOrEmpty(name)) return null;

        // Handle nullable types (e.g., int?)
        if (name.EndsWith(NullableMarkChar))
        {
            var result = ResolveType(name[..^1]);
            name = name[..^1];
            return typeof(Nullable<>).MakeGenericType(result);
        }

        // handle arrays
        if (name.EndsWith(ArrayMarkers.End))
        {
            var arrayStartIndex = name.LastIndexOf(ArrayMarkers.Start);
            var result = ResolveType(name[..arrayStartIndex]);
            var arrayMarker = name[arrayStartIndex..];
            var rank = arrayMarker.Count(c => c == ',') + 1;
            return rank == 1
                ? result.MakeArrayType()
                : result.MakeArrayType(rank);
        }

        //gère le cas des types génériques
        if (name.EndsWith(GenericMarkers.End))
        {
            if (genericParameters != null) throw new Exception("Unexpected generic type definition");

            var genericStartIndex = name.LastIndexOf(GenericMarkers.Start);
            var genericMarker = name[genericStartIndex..];
            genericParameters = Utils.SplitCommaSeparatedList(genericMarker.Trim(' ', '<', '>'), ',', [GenericMarkers, ArrayMarkers]).Select(t => ResolveType(t)).ToArray();
        }

        var type = TypeFinder.FindType(name, genericParameters);
        if (type != null) return type;
        return null;
    }

    public ConstructorInfo[] GetConstructors(Type type)
    {
        return type.GetTypeInfo().GetConstructors();
    }

    public MethodInfo[] GetInstanceMethods(Type type, string name)
    {
        return type.GetRuntimeMethods().Where(m => !m.IsStatic && m.Name == name)
            .Union(TypeFinder.FindExtensionMethods(type, name)).ToArray();
    }

    public MethodInfo[] GetStaticMethods(Type type, string name)
    {
        return type.GetRuntimeMethods().Where(m => m.IsStatic && m.Name == name).ToArray();
    }

    public (ConstructorInfo Method, Expression[] Parameters)? SelectConstructor(IEnumerable<ConstructorInfo> constructors, Expression[] arguments)
    {
        var argumentsTypes = arguments.Select(a => a.Type).ToArray();

        return constructors
            .Select(c => new DistanceValue<ConstructorInfo>(c.CompareParametersAndTypes(argumentsTypes), c))
            .Where(c => c.Distance >= 0)
            .OrderBy(c => c.Distance)
            .Select(c => (c.Value, c.Value.AdjustParameters(arguments)))
            .FirstOrDefault();
    }

    public (MethodInfo Method, Expression[] Parameters)? SelectMethod(IEnumerable<MethodInfo> methods, Expression obj, Type[] genericParameters, Expression[] arguments)
    {
        var argumentsTypes = arguments.Select (a=>a.Type).ToArray();

        return methods
            .Select(m => (genericParameters is null || !m.IsGenericMethodDefinition) ? m.InferGenericMethod(obj?.Type, argumentsTypes) : m.MakeGenericMethod(genericParameters))
            .Where(m => m is not null)
            .Select(m => new DistanceValue<MethodInfo>(m.CompareParametersAndTypes(obj, argumentsTypes), m))
            .Where(m => m.Distance >= 0)
            .OrderBy(m => m.Distance)
            .Select(m=>(m.Value, m.Value.AdjustParameters(obj, arguments)))
            .FirstOrDefault();
    }

    public MemberInfo GetStaticPropertyOrField(Type type, string name) 
        => (MemberInfo)type.GetProperty(name, BindingFlags.Public | BindingFlags.Static) ?? (MemberInfo)type.GetField(name, BindingFlags.Public | BindingFlags.Static);

    public MemberInfo GetInstancePropertyOrField(Type type, string name) 
        => (MemberInfo)type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) ?? (MemberInfo)type.GetField(name, BindingFlags.Public | BindingFlags.Instance);

    private class DistanceValue<T>(int distance, T value) : IDistanceValue<T>
    {
        public int Distance => distance;
        public T Value => value;
    }
}

