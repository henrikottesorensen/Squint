// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace Squint;

/// <summary>
/// Script properties and mixed-script detection, UTS #39 section 5.1.
/// </summary>
public static class Scripts
{
    /// <summary>
    /// The Recommended scripts of UAX #31 Table 5, as of Unicode 17.0. Common and Inherited are in
    /// the table and so are here.
    /// </summary>
    /// <remarks>
    /// Not derived from a data file: the table is prose in UAX #31, so this list is maintained by
    /// hand against it. What it decides is the Moderately Restrictive level, which admits Latin
    /// plus one other Recommended script other than Cyrillic or Greek.
    /// </remarks>
    public static ScriptSet Recommended { get; } = ScriptSet.Of(
        UnicodeScript.Common,
        UnicodeScript.Inherited,
        UnicodeScript.Arabic,
        UnicodeScript.Armenian,
        UnicodeScript.Bengali,
        UnicodeScript.Cyrillic,
        UnicodeScript.Devanagari,
        UnicodeScript.Ethiopic,
        UnicodeScript.Georgian,
        UnicodeScript.Greek,
        UnicodeScript.Gujarati,
        UnicodeScript.Gurmukhi,
        UnicodeScript.Hangul,
        UnicodeScript.Han,
        UnicodeScript.Hebrew,
        UnicodeScript.Hiragana,
        UnicodeScript.Katakana,
        UnicodeScript.Kannada,
        UnicodeScript.Khmer,
        UnicodeScript.Lao,
        UnicodeScript.Latin,
        UnicodeScript.Malayalam,
        UnicodeScript.Myanmar,
        UnicodeScript.Oriya,
        UnicodeScript.Sinhala,
        UnicodeScript.Tamil,
        UnicodeScript.Telugu,
        UnicodeScript.Thaana,
        UnicodeScript.Thai,
        UnicodeScript.Tibetan);

    /// <summary>
    /// The Script property of a code point; <see cref="UnicodeScript.Unknown"/> for one that has
    /// no character.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a code point.</exception>
    public static UnicodeScript Of(int codePoint)
    {
        CodePoints.Validate(codePoint, nameof(codePoint));
        return (UnicodeScript)ScriptData.RunScripts[Tables.FindRun(ScriptData.RunStarts, codePoint)];
    }

    /// <summary>
    /// The Script_Extensions property of a code point, which is its Script alone when
    /// ScriptExtensions.txt says nothing more.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a code point.</exception>
    public static ScriptSet ExtensionsOf(int codePoint)
    {
        CodePoints.Validate(codePoint, nameof(codePoint));

        int range = Tables.FindRange(ScriptData.ExtensionStarts, ScriptData.ExtensionEnds, codePoint);

        if (range >= 0)
        {
            return ScriptSet.FromWords(ScriptData.SetWords, ScriptData.ExtensionSets[range] * 4);
        }

        return ScriptSet.Empty.Add(Of(codePoint));
    }

    /// <summary>
    /// The augmented script set of a code point: Script_Extensions, with Han also counting as
    /// Hanb, Jpan and Kore, Hiragana and Katakana as Jpan, Hangul as Kore and Bopomofo as Hanb;
    /// and any set holding Common or Inherited becoming <see cref="ScriptSet.All"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a code point.</exception>
    public static ScriptSet AugmentedSetOf(int codePoint)
    {
        ScriptSet set = ExtensionsOf(codePoint);

        if (set.Contains(UnicodeScript.Common) || set.Contains(UnicodeScript.Inherited))
        {
            return ScriptSet.All;
        }

        if (set.Contains(UnicodeScript.Han))
        {
            set = set.Add(UnicodeScript.HanWithBopomofo).Add(UnicodeScript.Japanese).Add(UnicodeScript.Korean);
        }

        if (set.Contains(UnicodeScript.Hiragana) || set.Contains(UnicodeScript.Katakana))
        {
            set = set.Add(UnicodeScript.Japanese);
        }

        if (set.Contains(UnicodeScript.Hangul))
        {
            set = set.Add(UnicodeScript.Korean);
        }

        if (set.Contains(UnicodeScript.Bopomofo))
        {
            set = set.Add(UnicodeScript.HanWithBopomofo);
        }

        return set;
    }

    /// <summary>
    /// The resolved script set of a string: the intersection of the augmented script sets of its
    /// characters. Empty for a mixed-script string; <see cref="ScriptSet.All"/> for an empty
    /// string or one of only Common and Inherited characters.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static ScriptSet ResolvedSetOf(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return ResolvedSetWithout(text, null);
    }

    /// <summary>
    /// Whether the string is single-script: its resolved script set is not empty. Note the
    /// specification's warning that the name means at least one script, not exactly one.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static bool IsSingleScript(string text)
    {
        return !ResolvedSetOf(text).IsEmpty;
    }

    /// <summary>
    /// The four-letter ISO 15924 code of a script, such as <c>Latn</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="UnicodeScript"/>.</exception>
    public static string Code(UnicodeScript script)
    {
        return ScriptData.Codes[Validate(script)];
    }

    /// <summary>
    /// The long property value name of a script, such as <c>Old_Italic</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="UnicodeScript"/>.</exception>
    public static string Name(UnicodeScript script)
    {
        return ScriptData.Names[Validate(script)];
    }

    /// <summary>
    /// The script with the given four-letter code or long name, compared ordinally;
    /// <see cref="UnicodeScript.Undefined"/> and false when there is none.
    /// </summary>
    public static bool TryParse(string codeOrName, out UnicodeScript script)
    {
        if (codeOrName is not null)
        {
            for (int i = 1; i < ScriptData.ScriptCount; i++)
            {
                if (string.Equals(ScriptData.Codes[i], codeOrName, StringComparison.Ordinal)
                    || string.Equals(ScriptData.Names[i], codeOrName, StringComparison.Ordinal))
                {
                    script = (UnicodeScript)i;
                    return true;
                }
            }
        }

        script = UnicodeScript.Undefined;
        return false;
    }

    /// <summary>
    /// The intersection of the augmented script sets of the characters whose augmented set does
    /// not contain <paramref name="excluded"/>; of every character when that is null. The
    /// restriction-level algorithm needs the string with its Latin entries removed.
    /// </summary>
    internal static ScriptSet ResolvedSetWithout(string text, UnicodeScript? excluded)
    {
        ScriptSet resolved = ScriptSet.All;
        int index = 0;

        while (index < text.Length)
        {
            ScriptSet augmented = AugmentedSetOf(CodePoints.Read(text, ref index));

            if (excluded is null || !augmented.Contains(excluded.Value))
            {
                resolved = resolved.Intersect(augmented);
            }
        }

        return resolved;
    }

    private static int Validate(UnicodeScript script)
    {
        int index = (int)script;

        if (index < 1 || index >= ScriptData.ScriptCount)
        {
            throw new ArgumentOutOfRangeException(nameof(script), script, "Not a script.");
        }

        return index;
    }
}
