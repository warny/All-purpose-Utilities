using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Expressions;

/// <summary>
/// Defines a contract for compilers that turn source text into an executable delegate.
/// </summary>
public interface IDelegateCompiler
{
    /// <summary>
    /// Compiles source text directly to an executable <see cref="Delegate"/>.
    /// </summary>
    /// <param name="content">Source text to compile.</param>
    /// <returns>The compiled delegate.</returns>
    Delegate Compile(string content);

    /// <summary>
    /// Compiles source text directly to an executable delegate of type <typeparamref name="TDelegate"/>.
    /// </summary>
    /// <typeparam name="TDelegate">The delegate type to compile to.</typeparam>
    /// <param name="content">Source text to compile.</param>
    /// <returns>The compiled delegate.</returns>
    TDelegate Compile<TDelegate>(string content) where TDelegate : Delegate;
}

/// <summary>
/// Defines a contract for expression compilers that turn source text into LINQ expression trees, and,
/// since a lambda expression tree can always be compiled to a delegate, also exposes delegate compilation.
/// </summary>
public interface IExpressionCompiler : IDelegateCompiler
{
    /// <summary>
    /// Compiles source text to an expression tree.
    /// </summary>
    /// <param name="content">Source text to compile.</param>
    /// <param name="symbols">Optional symbol table used for identifier resolution.</param>
    /// <returns>The compiled expression.</returns>
    Expression CompileExpression(string content, IReadOnlyDictionary<string, Expression>? symbols = null);

    /// <summary>
    /// Compiles source text to a typed lambda expression tree.
    /// </summary>
    /// <typeparam name="TDelegate">The delegate type the compiled lambda expression must match.</typeparam>
    /// <param name="content">Source text to compile.</param>
    /// <returns>The compiled, typed lambda expression tree.</returns>
    Expression<TDelegate> CompileExpression<TDelegate>(string content) where TDelegate : Delegate;
}

/// <summary>
/// Defines a contract for compilers that turn source text into a compiled <see cref="Type"/>.
/// </summary>
public interface ITypeCompiler
{
    /// <summary>
    /// Compiles source text to a <see cref="Type"/>.
    /// </summary>
    /// <param name="content">Source text to compile.</param>
    /// <returns>The compiled type.</returns>
    Type CompileType(string content);

    /// <summary>
    /// Compiles source text to a type assignable to <typeparamref name="T"/> and returns a factory that
    /// constructs new instances of it.
    /// </summary>
    /// <typeparam name="T">The type or interface the compiled type must be assignable to.</typeparam>
    /// <param name="content">Source text to compile.</param>
    /// <returns>A factory that constructs a new instance of the compiled type on each call.</returns>
    Func<T> CompileType<T>(string content);
}

/// <summary>
/// Defines a contract for compilers that turn source text into a compiled <see cref="Assembly"/>.
/// </summary>
public interface IAssemblyCompiler
{
    /// <summary>
    /// Compiles source text to an <see cref="Assembly"/>.
    /// </summary>
    /// <param name="content">Source text to compile.</param>
    /// <returns>The compiled assembly.</returns>
    Assembly CompileAssembly(string content);
}
