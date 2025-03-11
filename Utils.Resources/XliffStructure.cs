using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Resources;

using System.Collections.Generic;

/// <summary>
/// Represents a complete XLIFF document (version 1.2 by default).
/// Typically contains one or more <see cref="XliffFile"/> elements.
/// </summary>
public sealed class XliffDocument
{
	/// <summary>
	/// Gets or sets the XLIFF version.
	/// The default is "1.2" for XLIFF 1.2 documents.
	/// </summary>
	public string Version { get; set; } = "1.2";

	/// <summary>
	/// Gets the list of <see cref="XliffFile"/> elements contained in this document.
	/// XLIFF allows multiple &lt;file&gt; blocks in a single document.
	/// </summary>
	public List<XliffFile> Files { get; } = new List<XliffFile>();
}

/// <summary>
/// Represents one &lt;file&gt; element in an XLIFF 1.2 document, 
/// which typically defines a source language, a target language,
/// and a collection of translation units or groups.
/// </summary>
public sealed class XliffFile
{
	/// <summary>
	/// Gets or sets the source language code (e.g. "en", "en-US").
	/// Corresponds to the "source-language" attribute in &lt;file&gt;.
	/// </summary>
	public string SourceLanguage { get; set; }

	/// <summary>
	/// Gets or sets the target language code (e.g. "fr", "fr-FR").
	/// Corresponds to the "target-language" attribute in &lt;file&gt;.
	/// </summary>
	public string TargetLanguage { get; set; }

	/// <summary>
	/// Gets or sets the data type associated with this file (e.g. "plaintext", "xml").
	/// Corresponds to the "datatype" attribute in &lt;file&gt;.
	/// </summary>
	public string Datatype { get; set; }

	/// <summary>
	/// Gets or sets the "original" attribute in &lt;file&gt;,
	/// which sometimes indicates the original file name or path.
	/// </summary>
	public string Original { get; set; }

	/// <summary>
	/// Optional tool identifier or other metadata (e.g., "tool-id").
	/// Can be used to track the translation tool used.
	/// </summary>
	public string ToolId { get; set; }

	/// <summary>
	/// Gets the list of top-level &lt;trans-unit&gt; elements in this file.
	/// These contain source/target text pairs for translation.
	/// In XLIFF 1.2, &lt;file&gt; &lt;body&gt; can have &lt;trans-unit&gt; or &lt;group&gt;.
	/// </summary>
	public List<XliffTransUnit> TransUnits { get; } = new List<XliffTransUnit>();

	/// <summary>
	/// Gets the list of top-level &lt;group&gt; elements in this file,
	/// each of which can itself contain nested groups or trans-units.
	/// </summary>
	public List<XliffGroup> Groups { get; } = new List<XliffGroup>();
}

/// <summary>
/// Represents a &lt;group&gt; element, which can contain nested 
/// &lt;trans-unit&gt; and/or &lt;group&gt; elements, allowing for hierarchical 
/// organization of translation units.
/// </summary>
public sealed class XliffGroup
{
	/// <summary>
	/// Gets or sets the ID for this &lt;group&gt; (e.g., "group1").
	/// </summary>
	public string Id { get; set; }

	/// <summary>
	/// Optional name or descriptive label for this group.
	/// Corresponds to a "restype" or "resname" attribute in some cases,
	/// or you can store your own labeling logic here.
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Gets a list of nested &lt;trans-unit&gt; within this group.
	/// </summary>
	public List<XliffTransUnit> TransUnits { get; } = new List<XliffTransUnit>();

	/// <summary>
	/// Gets a list of nested &lt;group&gt; elements.
	/// Groups can be nested arbitrarily in XLIFF 1.2.
	/// </summary>
	public List<XliffGroup> SubGroups { get; } = new List<XliffGroup>();
}

/// <summary>
/// Represents a single &lt;trans-unit&gt; element, the core translation unit
/// in XLIFF containing source and target text, among other metadata.
/// </summary>
public sealed class XliffTransUnit
{
	/// <summary>
	/// Gets or sets the unique identifier for this translation unit
	/// (e.g., "TU001"). This corresponds to the "id" attribute in &lt;trans-unit&gt;.
	/// </summary>
	public string Id { get; set; }

	/// <summary>
	/// Gets or sets an optional friendly name or resource name 
	/// (often "resname" in XLIFF).
	/// </summary>
	public string ResName { get; set; }

	/// <summary>
	/// Gets or sets the source text (the original text to be translated).
	/// </summary>
	public string Source { get; set; }

	/// <summary>
	/// Gets or sets the target text (the translation).
	/// </summary>
	public string Target { get; set; }

	/// <summary>
	/// Gets or sets an optional note or description of this trans-unit.
	/// In XLIFF 1.2, there can be one or more &lt;note&gt; elements.
	/// If you expect multiple notes, store them in <see cref="Notes"/>.
	/// </summary>
	public string Note { get; set; }

	/// <summary>
	/// Optional collection of multiple &lt;note&gt; elements
	/// if you need to track different notes (e.g., developer note, context, etc.).
	/// This can supplement or replace the single <see cref="Note"/> string property.
	/// </summary>
	public List<XliffNote> Notes { get; } = new List<XliffNote>();

	/// <summary>
	/// Gets a list of &lt;context-group&gt; elements that provide additional 
	/// context about this trans-unit (e.g., location in a file, meaning, etc.).
	/// </summary>
	public List<XliffContextGroup> ContextGroups { get; } = new List<XliffContextGroup>();

	/// <summary>
	/// Gets a list of &lt;alt-trans&gt; elements, which can contain 
	/// alternative translations or suggestions (e.g., fuzzy matches).
	/// </summary>
	public List<XliffAltTrans> AltTrans { get; } = new List<XliffAltTrans>();
}

/// <summary>
/// Represents a &lt;note&gt; element within a &lt;trans-unit&gt; or &lt;alt-trans&gt;,
/// often used to provide translator instructions or context.
/// </summary>
public sealed class XliffNote
{
	/// <summary>
	/// The optional "from" attribute, indicating who wrote the note
	/// or which system it originates from.
	/// </summary>
	public string From { get; set; }

	/// <summary>
	/// The optional "annotates" attribute, specifying what is being annotated
	/// (e.g., "source" or "target").
	/// </summary>
	public string Annotates { get; set; }

	/// <summary>
	/// The optional "priority" attribute (e.g., "1" or "10"),
	/// used to indicate how important the note is.
	/// </summary>
	public string Priority { get; set; }

	/// <summary>
	/// The actual text content of the note.
	/// </summary>
	public string Text { get; set; }
}

/// <summary>
/// Represents a &lt;context-group&gt; element, containing one or more &lt;context&gt; 
/// elements. This is used in XLIFF 1.2 to provide additional contextual metadata
/// about a translation unit (e.g., file location, meaning, usage).
/// </summary>
public sealed class XliffContextGroup
{
	/// <summary>
	/// The optional "purpose" attribute, describing the purpose of the context 
	/// (e.g., "location", "info", etc.).
	/// </summary>
	public string Purpose { get; set; }

	/// <summary>
	/// Gets the list of &lt;context&gt; children in this context group.
	/// </summary>
	public List<XliffContext> Contexts { get; } = new List<XliffContext>();
}

/// <summary>
/// Represents a &lt;context&gt; element, typically a piece of contextual information 
/// about the translation (e.g., a filename, location in code, a reference, etc.).
/// </summary>
public sealed class XliffContext
{
	/// <summary>
	/// The optional "context-type" attribute, indicating the nature of this context
	/// (e.g., "x-file", "x-location", "x-description").
	/// </summary>
	public string ContextType { get; set; }

	/// <summary>
	/// The textual content of the context element.
	/// </summary>
	public string Text { get; set; }
}

/// <summary>
/// Represents an &lt;alt-trans&gt; element, which provides an alternate translation, 
/// such as a fuzzy match from a Translation Memory or a machine translation suggestion.
/// </summary>
public sealed class XliffAltTrans
{
	/// <summary>
	/// The optional "match-quality" attribute (e.g., "80" or "100"), 
	/// indicating how close this alternate translation is to a perfect match.
	/// </summary>
	public string MatchQuality { get; set; }

	/// <summary>
	/// The optional "origin" attribute (e.g., "TM", "MT"), showing 
	/// where the alternate translation came from.
	/// </summary>
	public string Origin { get; set; }

	/// <summary>
	/// The &lt;source&gt; text for this alternate translation. 
	/// This is often the same or slightly modified from the main &lt;trans-unit&gt;'s source.
	/// </summary>
	public string Source { get; set; }

	/// <summary>
	/// The &lt;target&gt; text for this alternate translation.
	/// </summary>
	public string Target { get; set; }

	/// <summary>
	/// Any &lt;note&gt; elements associated with this alternate translation.
	/// </summary>
	public List<XliffNote> Notes { get; } = new List<XliffNote>();
}
