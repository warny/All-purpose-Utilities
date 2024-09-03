using System;

namespace Utils.Objects;

/// <summary>
/// Generates random numbers according to a specified distribution function (repartition function).
/// </summary>
public class DistributedRandom
{
	private readonly Random randomGenerator;
	private readonly Func<double, double> distributionFunction;
	private readonly double minValue;
	private readonly double maxValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="DistributedRandom"/> class with a specified distribution function and a random number generator.
	/// </summary>
	/// <param name="distributionFunction">The distribution function to map uniformly distributed random numbers to the desired distribution.</param>
	/// <param name="randomGenerator">The random number generator to use.</param>
	public DistributedRandom(Func<double, double> distributionFunction, Random randomGenerator)
	{
		this.randomGenerator = randomGenerator ?? throw new ArgumentNullException(nameof(randomGenerator));
		this.distributionFunction = distributionFunction ?? throw new ArgumentNullException(nameof(distributionFunction));
		minValue = distributionFunction(0);
		maxValue = distributionFunction(1);
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
	/// <returns>A double value that follows the specified distribution.</returns>
	public double NextDouble()
	{
		// Generate a uniform random number in the range [0, 1]
		double uniformRandom = randomGenerator.NextDouble();

		// Apply the distribution function and normalize the result
		double distributedValue = distributionFunction(uniformRandom);
		distributedValue = (distributedValue - minValue) / (maxValue - minValue);

		return distributedValue;
	}

	/// <summary>
	/// Generates a random double number within a specified range according to the specified distribution function.
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>An integer value that follows the specified distribution within the specified range.</returns>
	public double NextDouble(double min, double max)
	{
		min.ArgMustBeLesserThan(max);

		double distributedValue = NextDouble();
		return double.Floor(distributedValue * (max - min)) + min;
	}


	/// <summary>
	/// Generates a random integer number within a specified range according to the specified distribution function.
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>An integer value that follows the specified distribution within the specified range.</returns>
	public int NextInt(int min, int max) => (int)NextDouble(min, max);
}
