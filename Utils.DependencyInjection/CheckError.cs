using System;

namespace Utils.DependencyInjection;

/// <summary>
/// Represents a validation error produced by a specific check.
/// </summary>
/// <typeparam name="E">Type of error value produced during validation.</typeparam>
public readonly record struct CheckError<E>(Type Source, E Error);

