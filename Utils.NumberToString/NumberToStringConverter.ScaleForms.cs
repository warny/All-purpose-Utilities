namespace Utils.NumberToString
{
    public partial class NumberToStringConverter
    {
        /// <summary>
        /// Immutable, resolved lexical configuration of one scale noun: its effective forms and the
        /// selector choosing which form a group multiplier governs.
        /// </summary>
        /// <param name="Forms">The effective forms (synthesized singular/plural merged with the configured overrides).</param>
        /// <param name="Selector">The selector, <see cref="DefaultLexicalFormSelector"/> unless configured.</param>
        private sealed record ScaleFormDefinition(LexicalFormSet Forms, ILexicalFormSelector Selector);
    }
}
