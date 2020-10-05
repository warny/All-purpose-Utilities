using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics
{
	public static class MathEx
	{
		public const double Deg2Rad = Math.PI / 180;
		public const double Rad2Deg = 180 / Math.PI;

		#region Modulus
		/// <summary>
		/// Calcul alternatif du modulo (différent de l'opérateur %)
		/// Le résultat est toujours compris entre 0 et y-1
		/// Exemple : 
		///   -1 % 3 = -1
		///	  Modulo(-1, 3) = 2
		/// </summary>
		public static long Mod( long a, long b )
		{
			return (a % b + b) % b;
		}

		/// <summary>
		/// Calcul alternatif du modulo (différent de l'opérateur %)
		/// Le résultat est toujours compris entre 0 et y-1
		/// Exemple : 
		///   -1 % 3 = -1
		///	  Modulo(-1, 3) = 2
		/// </summary>
		public static int Mod( int a, int b )
		{
			return (a % b + b) % b;
		}

		/// <summary>
		/// Calcul alternatif du modulo (différent de l'opérateur %)
		/// Le résultat est toujours compris entre 0 et y-1
		/// Exemple : 
		///   -1 % 3 = -1
		///	  Modulo(-1, 3) = 2
		/// </summary>
		public static short Mod( short a, short b )
		{
			return (short)((a % b + b) % b);
		}

		/// <summary>
		/// Calcul alternatif du modulo (différent de l'opérateur %)
		/// Le résultat est toujours compris entre 0 et y-1
		/// Exemple : 
		///   -1 % 3 = -1
		///	  Modulo(-1, 3) = 2
		/// </summary>
		public static decimal Mod( decimal a, decimal b )
		{
			return ((a % b + b) % b);
		}

		/// <summary>
		/// Calcul alternatif du modulo (différent de l'opérateur %)
		/// Le résultat est toujours compris entre 0 et y-1
		/// Exemple : 
		///   -1 % 3 = -1
		///	  Modulo(-1, 3) = 2
		/// </summary>
		public static float Mod(float a, float b )
		{
			return ((a % b + b) % b);
		}

		/// <summary>
		/// Calcul alternatif du modulo (différent de l'opérateur %)
		/// Le résultat est toujours compris entre 0 et y-1
		/// Exemple : 
		///   -1 % 3 = -1
		///	  Modulo(-1, 3) = 2
		/// </summary>
		public static double Mod( double a, double b )
		{
			return ((a % b + b) % b);
		}
		#endregion

		#region round

		public static float Round( float value, float @base )
		{
			return (float)Round((double)value, @base);
		}


		public static double Round( double value, double @base )
		{
			double middle = @base/2;
			double r = Math.IEEERemainder(value, @base);
			if (r < middle) {
				return value - r;
			} else {
				return value - r + @base;
			}
		}

		public static double Round( double value, int exponent = 0 )
		{
			double @base = Math.Pow(10, exponent);
			return Round (value, @base);
		}
		#endregion

		#region floor
		/// <summary>
		/// retrouve le multiple de la base inférieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple inférieur</returns>
		public static long Floor( long value, long @base )
		{
			return (value-1) - (Mod(value-1, @base));
		}
		/// <summary>
		/// retrouve le multiple de la base inférieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple inférieur</returns>
		public static int Floor( int value, int @base )
		{
			return (value-1) - (Mod(value-1, @base));
		}
		/// <summary>
		/// retrouve le multiple de la base inférieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple inférieur</returns>
		public static short Floor( short value, short @base )
		{
			return (short)((value-1) - (Mod(value-1, @base)));
		}

		/// <summary>
		/// retrouve le multiple de la base inférieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple inférieur</returns>
		public static decimal Floor( decimal value, decimal @base )
		{
			return ((value-1) - (Mod(value-1, @base)));
		}

		/// <summary>
		/// retrouve le multiple de la base inférieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple inférieur</returns>
		public static float Floor( float value, float @base )
		{
			return (float)((value-1) - (Math.IEEERemainder(value-1, @base)));
		}

		/// <summary>
		/// retrouve le multiple de la base inférieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple inférieur</returns>
		public static double Floor( double value, double @base )
		{
			return ((value-1) - (Math.IEEERemainder(value-1, @base)));
		}

		#endregion

		#region ceiling
		/// <summary>
		/// retrouve le multiple de la base supérieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple supérieur</returns>
		public static long Ceiling( long value, long @base )
		{
			return (value-1) + @base - (Mod(value-1, @base));
		}
		/// <summary>
		/// retrouve le multiple de la base supérieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple supérieur</returns>
		public static int Ceiling( int value, int @base )
		{
			return (value-1) + @base - (Mod(value-1, @base));
		}
		/// <summary>
		/// retrouve le multiple de la base supérieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple supérieur</returns>
		public static short Ceiling( short value, short @base )
		{
			return (short)((value-1) + @base - (Mod(value-1, @base)));
		}
		/// <summary>
		/// retrouve le multiple de la base supérieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple supérieur</returns>
		public static decimal Ceiling( decimal value, decimal @base )
		{
			return (value-1) + @base - (Mod(value-1, @base));
		}

		/// <summary>
		/// retrouve le multiple de la base supérieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple supérieur</returns>
		public static float Ceiling( float value, float @base )
		{
			return (float)((value-1) + @base - (Math.IEEERemainder(value-1, @base)));
		}

		/// <summary>
		/// retrouve le multiple de la base supérieur à value
		/// </summary>
		/// <param name="value">valeur</param>
		/// <param name="base">base</param>
		/// <returns>multiple supérieur</returns>
		public static double Ceiling( double value, double @base )
		{
			return (value-1) + @base - (Math.IEEERemainder(value-1, @base));
		}
		#endregion

		#region MinMax
		/// <summary>
		/// Retourne la valeur minimale de <paramref name="values"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values">Valeurs à comparer</param>
		/// <returns>Valeur minimale</returns>
		public static T Min<T>( params T[] values ) where T : IComparable<T>
		{
			T result = values[0];
			for (int i = 1; i<values.Length; i++) {
				T value = values[i];
				if (value.CompareTo(result) < 0) {
					result = value;
				}
			}
			return result;
		}

		/// <summary>
		/// Retourne la valeur minimale de <paramref name="values"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values">Valeurs à comparer</param>
		/// <param name="comparer">Comparateur</param>
		/// <returns>Valeur minimale</returns>
		public static T Min<T>(IComparer<T> comparer,  params T[] values )
		{
			T result = values[0];
			for (int i = 1 ; i<values.Length ; i++) {
				T value = values[i];
				if (comparer.Compare(value, result) < 0) {
					result = value;
				}
			}
			return result;
		}

		/// <summary>
		/// Retourne la valeur maximale de <paramref name="values"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values">Valeurs à comparer</param>
		/// <returns>Valeur maximale</returns>
		public static T Max<T>( params T[] values ) where T : IComparable<T>
		{
			T result = values[0];
			for (int i = 1 ; i<values.Length ; i++) {
				T value = values[i];
				if (value.CompareTo(result) > 0) {
					result = value;
				}
			}
			return result;
		}

		/// <summary>
		/// Retourne la valeur maximale de <paramref name="values"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values">Valeurs à comparer</param>
		/// <param name="comparer">Comparateur</param>
		/// <returns>Valeur maximale</returns>
		public static T Max<T>( IComparer<T> comparer, params T[] values )
		{
			T result = values[0];
			for (int i = 1 ; i<values.Length ; i++) {
				T value = values[i];
				if (comparer.Compare(value, result) > 0) {
					result = value;
				}
			}
			return result;
		}

		/// <summary>
		/// Retourne <paramref name="value"/> si elle est comprise entre <paramref name="min"/> et <paramref name="max"/>, <paramref name="min"/> si elle est inférieur et <paramref name="max"/> si elle est supérieure
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value">Valeur à comparer</param>
		/// <param name="min">Valeur minimale</param>
		/// <param name="max">Valeur Maximale</param>
		/// <returns></returns>
		public static T MinMax<T>(this T value, T min, T max) where T : IComparable<T>
		{
			if (min.CompareTo(max) > 0) throw new ArgumentException("min doit être inférieur à max");
			if (value.CompareTo(min) < 0) return min;
			if (value.CompareTo(max) > 0) return max;
			return value;
		}

		/// <summary>
		/// Retourne <paramref name="value"/> si elle est comprise entre <paramref name="min"/> et <paramref name="max"/>, <paramref name="min"/> si elle est inférieur et <paramref name="max"/> si elle est supérieure
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value">Valeur à comparer</param>
		/// <param name="min">Valeur minimale</param>
		/// <param name="max">Valeur Maximale</param>
		/// <param name="comparer">Comparateur</param>
		/// <returns></returns>
		public static T MinMax<T>(this T value, T min, T max, IComparer<T> comparer)
		{
			if (comparer.Compare(min, max) > 0) throw new ArgumentException("min doit être inférieur à max");
			if (comparer.Compare(value, min) < 0) return min;
			if (comparer.Compare(value, max) > 0) return max;
			return value;
		}

		#endregion

		#region comparaisons
		public static bool Between<T>( this T value, T lowerBound, T upperBound ) where T : IComparable<T>
		{
			return value.CompareTo(lowerBound) >= 0 && value.CompareTo(upperBound) <= 0;
		}

		public static bool Between<T>( this T value, IComparer<T> comparer, T lowerBound, T upperBound )
		{
			return comparer.Compare(value, lowerBound) >= 0 && comparer.Compare(value,upperBound) <= 0;
		}

		/// <summary>
		/// Renvoie si la valeur est dans le tableau
		/// </summary>
		/// <typeparam name="T">Type de la valeur</typeparam>
		/// <param name="value">Valeur à rechercher</param>
		/// <param name="objects">Liste de valeurs dans lesquelles la recherche s'applique</param>
		/// <returns></returns>
		public static bool In<T>( this T value, params T[] objects )
		{
			return Array.IndexOf<T>(objects, value) > -1;
		}

		/// <summary>
		/// Renvoie si la valeur n'est pas dans le tableau
		/// </summary>
		/// <typeparam name="T">Type de la valeur</typeparam>
		/// <param name="value">Valeur à rechercher</param>
		/// <param name="objects">Liste de valeurs dans lesquelles la recherche s'applique</param>
		/// <returns></returns>
		public static bool NotIn<T>( this T value, params T[] objects )
		{
			return Array.IndexOf<T>(objects, value) == -1;
		}

		/// <summary>
		/// Renvoie l'index de l'élément s'il est dans l'énumération sinon -1
		/// </summary>
		/// <typeparam name="T">Type des éléments dans le tableau</typeparam>
		/// <param name="enumeration">Enumération dans laquelle faire la recherche</param>
		/// <param name="toFind">Element à retrouver</param>
		/// <returns>index de l'élément s'il est trouvé, sinon -1</returns>
		public static int IndexOf<T>( this IEnumerable<T> enumeration, T toFind )
		{
			int i = 0;
			foreach (var element in enumeration) {
				if (element.Equals(toFind)) return i;
				i++;
			}
			return -1;
		}

		/// <summary>
		/// Renvoie l'index de l'élément s'il est dans l'énumération sinon -1
		/// </summary>
		/// <typeparam name="T">Type des éléments dans le tableau</typeparam>
		/// <param name="enumeration">Enumération dans laquelle faire la recherche</param>
		/// <param name="toFind">Element à retrouver</param>
		/// <param name="comparer">Comparateur</param>
		/// <returns>index de l'élément s'il est trouvé, sinon -1</returns>
		public static int IndexOf<T>( this IEnumerable<T> enumeration, T toFind, IEqualityComparer<T> comparer )
		{
			int i = 0;
			foreach (var element in enumeration) {
				if (comparer.Equals(element, toFind)) return i;
				i++;
			}
			return -1;
		}

		/// <summary>
		/// Renvoie l'index de l'élément s'il est dans l'énumération sinon -1
		/// </summary>
		/// <typeparam name="T">Type des éléments dans le tableau</typeparam>
		/// <param name="enumeration">Enumération dans laquelle faire la recherche</param>
		/// <param name="func">Fonction de recherche</param>
		/// <returns>index de l'élément s'il est trouvé, sinon -1</returns>
		public static int IndexOf<T>( this IEnumerable<T> enumeration, Func<T, bool> func )
		{
			int i = 0;
			foreach (var element in enumeration) {
				if (func(element)) return i;
				i++;
			}
			return -1;
		}

		#endregion

		#region calculs
		/// <summary>
		/// Renvoie la puissance d'un nombre
		/// </summary>
		/// <param name="value">Valeur</param>
		/// <param name="exponent">Exposant</param>
		/// <returns></returns>
		public static long Pow( long value, long exponent )
		{
			long result = 1;
			long temp = value;
			for (long i = exponent ; i > 0 ; i>>=1) {
				if ((i & 1) == 1) {
					result *= temp;
				}
				temp = temp * temp;
			}
			return result;
		}

		/// <summary>
		/// Renvoie la racine carrée du nombre en paramètre
		/// </summary>
		/// <param name="value">Valeur</param>
		/// <returns>Racine carrée</returns>
		public static byte Sqrt( byte value )
		{
			return (byte)Math.Sqrt(value);
		}

		/// <summary>
		/// Renvoie la racine carrée du nombre en paramètre
		/// </summary>
		/// <param name="value">Valeur</param>
		/// <returns>Racine carrée</returns>
		public static short Sqrt( short value )
		{
			return (short)Math.Sqrt(value);
		}

		/// <summary>
		/// Renvoie la racine carrée du nombre en paramètre
		/// </summary>
		/// <param name="value">Valeur</param>
		/// <returns>Racine carrée</returns>
		public static int Sqrt( int value )
		{
			return (int)Math.Sqrt(value);
		}

		/// <summary>
		/// Renvoie la racine carrée du nombre en paramètre
		/// </summary>
		/// <param name="value">Valeur</param>
		/// <returns>Racine carrée</returns>
		public static long Sqrt( long value )
		{
			return (long)Math.Sqrt(value);
		}

		#endregion
	}
}
