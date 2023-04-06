using System;
using System.Collections.Generic;
using System.Text;

namespace Utils.Objects
{
	public static class SpanUtilities
	{
		/// <summary>
		/// Supprime du début et de la fin de <paramref name="s"/> tous les éléments correspondant au résultat de la fonction spécifiée
		/// </summary>
		/// <param name="s">Chaîne de référence</param>
		/// <param name="trimTester">Fonction de test (renvoi <see cref="true"/> s'il faut supprimer l'élément)</param>
		/// <returns>Chaîne expurgée des éléments à supprimer</returns>
		public static ReadOnlySpan<T> Trim<T>(this ReadOnlySpan<T> s, Func<T, bool> trimTester)
		{
			return s.TrimStart(trimTester).TrimEnd(trimTester);
		}

		/// <summary>
		/// Supprime du début de <paramref name="s"/> tous les éléments correspondant au résultat de la fonction spécifiée
		/// </summary>
		/// <param name="s">Chaîne de référence</param>
		/// <param name="trimTester">Fonction de test (renvoi <see cref="true"/> s'il faut supprimer l'élément)</param>
		/// <returns>Chaîne expurgée des éléments à supprimer</returns>
		public static ReadOnlySpan<T> TrimStart<T>(this ReadOnlySpan<T> s, Func<T, bool> trimTester)
		{
			int start, end = s.Length;
			for (start = 0; start < end; start++)
			{
				if (!trimTester(s[start])) break;
			}
			if (start >= end) return ReadOnlySpan<T>.Empty;
			return s.Slice(start, end - start);
		}

		/// <summary>
		/// Supprime de la fin de <paramref name="s"/> tous les éléments correspondant au résultat de la fonction spécifiée
		/// </summary>
		/// <param name="s">Chaîne de référence</param>
		/// <param name="trimTester">Fonction de test (renvoi <see cref="true"/> s'il faut supprimer l'élément)</param>
		/// <returns>Chaîne expurgée des éléments à supprimer</returns>
		public static ReadOnlySpan<T> TrimEnd<T>(this ReadOnlySpan<T> s, Func<T, bool> trimTester)
		{
			int start = 0, end;
			for (end = s.Length - 1; end > start; end--)
			{
				if (!trimTester(s[end])) break;
			}
			if (start >= end) return ReadOnlySpan<T>.Empty;
			return s.Slice(start, end - start + 1);
		}

		/// <summary>
		/// Récupère un sous-<see cref="ReadOnlySpan<T>"/> de cette instance. Le sous-<see cref="ReadOnlySpan<T>"/> démarre à une position de caractère spécifiée et a une longueur définie.
		/// </summary>
		/// <param name="s">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="start">Position de caractère de départ de base zéro d'une sous-chaîne dans String</param>
		/// <param name="length">Nombre de caractères dans la sous-chaîne</param>
		/// <returns>
		/// Un <see cref="ReadOnlySpan<T>"/> équivalent au sous-<see cref="ReadOnlySpan<T>"/> de longueur <paramref name="length"/> qui commence
		/// à <paramref name="start"/> dans cette instance, ou <see cref="ReadOnlySpan<T>"/>.Empty si <paramref name="start"/> est
		/// égal à la longueur de cette instance et length est égal à zéro.
		/// </returns>
		public static ReadOnlySpan<T> Mid<T>(this ReadOnlySpan<T> s, int start, int length)
		{
			if (length < 0)
			{
				if (start > 0 && -length > start)
				{
					length = start + 1;
					start = 0;
				}
				else
				{
					start += length + 1;
					length = -length;
				}
			}
			if (start < 0) start = s.Length + start;
			if (start <= -length) return ReadOnlySpan<T>.Empty;
			if (start < 0) return s.Slice(0, length + start);
			if (start > s.Length) return ReadOnlySpan<T>.Empty;
			if (start + length > s.Length) return s.Slice(start);
			return s.Slice(start, length);
		}

		/// <summary>
		/// Récupère un sous-<see cref="ReadOnlySpan<T>"/> de cette instance. Le sous-<see cref="ReadOnlySpan<T>"/> démarre à une position de caractère spécifiée et a une longueur définie.
		/// </summary>
		/// <param name="s">Chaîne dont on veut extraire la sous-chaîne</param>
		/// <param name="start">Position de caractère de départ de base zéro d'une sous-chaîne dans String</param>
		/// <returns>
		/// Un <see cref="ReadOnlySpan<T>"/> équivalent au sous-<see cref="ReadOnlySpan<T>"/> à partir de l'index <paramref name="start"/>
		/// </returns>
		public static ReadOnlySpan<T> Mid<T>(this ReadOnlySpan<T> s, int start)
			=> s.Mid(start, s.Length);

	}
}
