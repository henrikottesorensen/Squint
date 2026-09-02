// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

using System;
using System.Text;

namespace Squint;

/// <summary>
/// Confusable detection, UTS #39 section 4: the skeleton, and the three classes of confusable.
/// </summary>
/// <remarks>
/// <para>
/// A skeleton is not for display and not for storage across Unicode versions. Two strings are
/// confusable when their skeletons are equal, and that is all a skeleton is for: compare them,
/// or index them, and recompute them when the data changes.
/// </para>
/// <para>
/// This is <c>internalSkeleton</c> in the specification's current terms, which is what the
/// specification's <c>skeleton(X)</c> was before revision 27 added bidirectional reordering, and
/// what every deployed implementation computes for left-to-right text with no right-to-left
/// characters. The bidirectional skeleton is not implemented.
/// </para>
/// </remarks>
public static class Confusables
{
    /// <summary>
    /// The skeleton of the text: NFD, then Default_Ignorable_Code_Point characters removed, then
    /// each character replaced by its prototype from confusables.txt, then NFD again.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static string Skeleton(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        string decomposed = Normalization.Nfd(text);
        StringBuilder output = new StringBuilder(decomposed.Length + 8);
        int index = 0;

        while (index < decomposed.Length)
        {
            int codePoint = CodePoints.Read(decomposed, ref index);

            if (IsDefaultIgnorable(codePoint))
            {
                continue;
            }

            AppendPrototype(codePoint, output);
        }

        return Normalization.Nfd(output.ToString());
    }

    /// <summary>
    /// Whether the two strings have the same skeleton.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either string is null.</exception>
    public static bool AreConfusable(string first, string second)
    {
        return string.Equals(Skeleton(first), Skeleton(second), StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether, and how, the two strings are confusable.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either string is null.</exception>
    public static ConfusableClass Classify(string first, string second)
    {
        if (!AreConfusable(first, second))
        {
            return ConfusableClass.NotConfusable;
        }

        ScriptSet firstScripts = Scripts.ResolvedSetOf(first);
        ScriptSet secondScripts = Scripts.ResolvedSetOf(second);

        if (firstScripts.Intersects(secondScripts))
        {
            return ConfusableClass.SingleScript;
        }

        if (!firstScripts.IsEmpty && !secondScripts.IsEmpty)
        {
            return ConfusableClass.WholeScript;
        }

        return ConfusableClass.MixedScript;
    }

    /// <summary>
    /// The prototype of one code point from confusables.txt, or null when the table has no
    /// entry and the code point is its own prototype.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a code point.</exception>
    public static string? Prototype(int codePoint)
    {
        CodePoints.Validate(codePoint, nameof(codePoint));

        int key = Tables.FindKey(ConfusableData.Keys, codePoint);

        if (key < 0)
        {
            return null;
        }

        int packed = ConfusableData.Values[key];
        return ConfusableData.Targets.Substring(packed >> 8, packed & 0xFF);
    }

    /// <summary>
    /// Whether the code point has the Default_Ignorable_Code_Point property, which the skeleton
    /// drops.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a code point.</exception>
    public static bool IsDefaultIgnorable(int codePoint)
    {
        CodePoints.Validate(codePoint, nameof(codePoint));
        return Tables.FindRange(CharacterData.DefaultIgnorableStarts, CharacterData.DefaultIgnorableEnds, codePoint) >= 0;
    }

    private static void AppendPrototype(int codePoint, StringBuilder output)
    {
        int key = Tables.FindKey(ConfusableData.Keys, codePoint);

        if (key < 0)
        {
            CodePoints.Append(output, codePoint);
            return;
        }

        int packed = ConfusableData.Values[key];
        output.Append(ConfusableData.Targets, packed >> 8, packed & 0xFF);
    }
}
