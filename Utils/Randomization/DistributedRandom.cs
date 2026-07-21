using System;
using Utils.Objects;

namespace Utils.Randomization;

/// <summary>
/// Generates random numbers according to a specified distribution function (repartition function).
/// </summary>
/// <remarks>
/// The distribution function must be monotonically non-decreasing and must produce a finite,
/// non-zero interval between <c>f(0)</c> and <c>f(1)</c>.  A constant, infinite, or NaN-valued
/// function is rejected at construction time (#18).
/// </remarks>
public class DistributedRandom
{
    private readonly Random randomGenerator;
    private readonly Func<double, double> distributionFunction;
    private readonly double minValue;
    private readonly double maxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedRandom"/> class with a specified distribution function and a random number generator.
    /// </summary>
    /// <param name="distributionFunction">
    /// The distribution function to map uniformly distributed random numbers to the desired distribution.
    /// Must produce finite values at 0 and 1, and the two endpoint values must differ.
    /// </param>
    /// <param name="randomGenerator">The random number generator to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="distributionFunction"/> or <paramref name="randomGenerator"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the distribution function returns a non-finite value at 0 or 1, or when the
    /// interval between the two endpoint values is zero (constant function). (#18)
    /// </exception>
    public DistributedRandom(Func<double, double> distributionFunction, Random randomGenerator)
    {
        this.randomGenerator = randomGenerator ?? throw new ArgumentNullException(nameof(randomGenerator));
        this.distributionFunction = distributionFunction ?? throw new ArgumentNullException(nameof(distributionFunction));

        double f0 = distributionFunction(0);
        double f1 = distributionFunction(1);

        if (!double.IsFinite(f0))
            throw new ArgumentException(
                $"The distribution function must return a finite value at 0, but returned {f0}.",
                nameof(distributionFunction));

        if (!double.IsFinite(f1))
            throw new ArgumentException(
                $"The distribution function must return a finite value at 1, but returned {f1}.",
                nameof(distributionFunction));

        double interval = f1 - f0;
        if (interval == 0.0)
            throw new ArgumentException(
                "The distribution function is constant (f(0) == f(1)). The normalization interval must be non-zero.",
                nameof(distributionFunction));

        minValue = f0;
        maxValue = f1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedRandom"/> class with a specified distribution function and a default random number generator.
    /// </summary>
    /// <param name="distributionFunction">The distribution function to map uniformly distributed random numbers to the desired distribution.</param>
    public DistributedRandom(Func<double, double> distributionFunction)
        : this(distributionFunction, new Random()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedRandom"/> class with a specified distribution function and a random seed.
    /// </summary>
    /// <param name="distributionFunction">The distribution function to map uniformly distributed random numbers to the desired distribution.</param>
    /// <param name="seed">The seed for the random number generator.</param>
    public DistributedRandom(Func<double, double> distributionFunction, int seed)
        : this(distributionFunction, new Random(seed)) { }

    /// <summary>
    /// Generates a random double number according to the specified distribution function.
    /// </summary>
    /// <returns>A double value in [0, 1] that follows the specified distribution.</returns>
    public double NextDouble()
    {
        // Generate a uniform random number in the range [0, 1]
        double uniformRandom = randomGenerator.NextDouble();

        // Apply the distribution function and normalize the result to [0, 1].
        double distributedValue = distributionFunction(uniformRandom);
        distributedValue = (distributedValue - minValue) / (maxValue - minValue);

        return distributedValue;
    }

    /// <summary>
    /// Generates a random double number within a specified range according to the specified distribution function.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the random number.</param>
    /// <param name="max">The exclusive upper bound of the random number.</param>
    /// <returns>A double value that follows the specified distribution within the specified range.</returns>
    public double NextDouble(double min, double max)
    {
        min.ArgMustBeLesserThan(max);

        double distributedValue = NextDouble();
        return distributedValue * (max - min) + min;
    }

    /// <summary>
    /// Generates a random integer number within a specified range according to the specified distribution function.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the random number.</param>
    /// <param name="max">The exclusive upper bound of the random number.</param>
    /// <returns>
    /// An integer value in [<paramref name="min"/>, <paramref name="max"/>) that follows the
    /// specified distribution.  The result is guaranteed to be strictly less than
    /// <paramref name="max"/> and never overflows (#19).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="max"/> is not strictly greater than <paramref name="min"/>.
    /// </exception>
    public int NextInt(int min, int max)
    {
        // Widen to long to avoid int overflow when computing the range (#19).
        long range = (long)max - (long)min;
        if (range <= 0)
            throw new ArgumentOutOfRangeException(nameof(max), max, "max must be strictly greater than min.");

        double sample = NextDouble(); // result in [0, 1)

        // Clamp to [0, 1) defensively in case the distribution function produces values
        // slightly outside that range due to floating-point rounding (#19).
        if (sample < 0.0) sample = 0.0;
        if (sample >= 1.0) sample = Math.BitDecrement(1.0); // largest double < 1

        // Scale into [min, max) using long arithmetic to prevent overflow.
        long result = (long)(sample * range) + min;

        // Final clamp to guarantee the documented [min, max) contract.
        if (result < min) result = min;
        if (result >= max) result = max - 1;

        return (int)result;
    }
}
