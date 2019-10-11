using System;
using System.Collections.Generic;
using System.Text;
using Utils.Mathematics.Expressions.Parser.RulesImplementations;

namespace Utils.Mathematics.Expressions.Parser
{
	/// <summary>
	/// Rêgle de comparaison
	/// </summary>
	public abstract class Rule
	{
		/// <summary>
		/// Indique si la rêgle permet de continuer lorsqu'elle est vérifiée
		/// </summary>
		public bool CanContinue { get; protected set; }

		/// <summary>
		/// Résultat de la vérification
		/// </summary>
		public Result Result { get; protected set; }

		public Context Context { get; protected set; }

		/// <summary>
		/// Test du prochain caractère
		/// </summary>
		/// <param name="c">Caractère à tester</param>
		/// <param name="index">Index du caractère dans la chaîne</param>
		/// <returns><see cref="true"/>si la rêgle peut encore être utilisée</returns>
		protected internal abstract bool Next(char c, int index);

		/// <summary>
		/// Réinitialise les paramètres de la rêgle
		/// </summary>
		/// <param name="index">Index du caractère dans la chaîne</param>
		protected internal void Reset(int index, Context context)
		{
			Result = new Result(index);
			Context = context;
			OnReset(index, context);
		}

		protected internal abstract void OnReset(int index, Context context);

		/// <summary>
		/// Clone la rêgle
		/// </summary>
		/// <returns></returns>
		protected internal abstract Rule Clone();

		/// <summary>
		/// Ajoute une rêgle en séquence
		/// </summary>
		/// <param name="rule">Rêgle à ajouter</param>
		/// <returns>Séquence de rêgles</returns>
		protected virtual Rule Then(Rule rule)	=> new SequenceRule(this, rule);
		/// <summary>
		/// Ajoute une rêgle d'exclusion
		/// </summary>
		/// <returns></returns>
		protected virtual Rule Not() => new NotRule(this);

		/// <summary>
		/// Transforme la rêgle en rêgle d'exclusion
		/// </summary>
		/// <param name="rule"></param>
		/// <returns></returns>
		public static Rule operator !(Rule rule) => rule.Not();
		
		/// <summary>
		/// Créé une séquence de rêgle
		/// </summary>
		/// <param name="rule1"><see cref="Rule"/> à exécuter en premier</param>
		/// <param name="rule2"><see cref="Rule"/> à exécuter ensuite</param>
		/// <returns>Séquence de rêgles</returns>
		public static Rule operator +(Rule rule1, Rule rule2) => rule1.Then(rule2);
		
		/// <summary>
		/// Créer un groupe de rêgle dont l'une doit être vérifiée
		/// </summary>
		/// <param name="rule1"><see cref="Rule"/></param>
		/// <param name="rule2"><see cref="Rule"/></param>
		/// <returns>Rêgles en parallèle</returns>
		public static Rule operator |(Rule rule1, Rule rule2) => new OrRule(rule1, rule2);
		
		/// <summary>
		/// Créé une répétition fixe d'une rêgle
		/// </summary>
		/// <param name="rule"><see cref="Rule"/> à répéter</param>
		/// <param name="repetition">nombre de répétition à vérifier</param>
		/// <returns>Répétition de <see cref="Rule"/></returns>
		public static Rule operator *(Rule rule, int repetition) => Rules.Repeat(rule, repetition);
		
		/// <summary>
		/// Créé une répétition 
		/// </summary>
		/// <param name="rule"></param>
		/// <param name="repetitions"></param>
		/// <returns></returns>
		public static Rule operator *(Rule rule, (int? minimum, int? maximum) repetitions) => Rules.Repeat(rule, repetitions.minimum ?? 0, repetitions.maximum ?? int.MaxValue);
	}

	public static class Rules
	{
		public static Rule Chars(params char[] chars) => new IncludeCharRule(chars);
		public static Rule Chars(string chars) => new IncludeCharRule(chars);
		public static Rule ExcludeChars(params char[] chars) => new ExcludeCharRule(chars);
		public static Rule ExcludeChars(string chars) => new ExcludeCharRule(chars);
		public static Rule String(string @string) => new StringRule(@string);
		public static Rule Sequence(IEnumerable<Rule> rules) => new SequenceRule(rules);
		public static Rule Sequence(params Rule[] rules) => new SequenceRule(rules);
		public static Rule Or(IEnumerable<Rule> rules) => new OrRule(rules);
		public static Rule Or(params Rule[] rules) => new OrRule(rules);
		public static Rule Repeat(this Rule rule, int repetition) => new RepetitionRule(rule, repetition);
		public static Rule Repeat(this Rule rule, int minimum = 0, int maximum = int.MaxValue) => new RepetitionRule(rule, minimum, maximum);
		public static Rule Not(Rule rule) => !rule;
		public static Rule Group(string name, Rule rule) => new GroupRule(name, rule);
		public static Rule GroupReference(string name) => new GroupReference(name);
	}
}
