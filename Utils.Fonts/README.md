# Utils.Fonts Library

**Utils.Fonts** handles font parsing and metrics extraction for TrueType and PostScript fonts.
It is used by the imaging library to render glyphs accurately.

## Features

- Parsing of TrueType font tables including glyph, cmap and kerning information
- Tools to inspect font flags, encoding records and glyph metrics
- Utilities to convert between different font encodings
- Designed with a data/processing split so rendering engines can implement their own logic
