// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Xunit;

namespace Squint.Test;

/// <summary>
/// The library against ICU 78, which implements the same specification from the same Unicode
/// 17.0 data, twice over: ICU4C through <c>tools/icu-oracle.py</c>, and ICU4J, the code the
/// script-set and restriction-level logic was ported from, through <c>tools/Icu4jOracle.java</c>.
/// Both write a fixture in the same layout, so the tests run anywhere without ICU installed and
/// a disagreement is between implementations, not between the library and one reading of the
/// text.
/// </summary>
/// <remarks>
/// Each test collects every disagreement before failing, so one run says how wrong something
/// is and where, rather than stopping at the first case.
/// </remarks>
public class IcuOracleTests
{
    private static readonly Dictionary<string, JsonDocument> Fixtures = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);

    /// <summary>
    /// The two fixtures, by file name.
    /// </summary>
    public static TheoryData<string> Oracles => new TheoryData<string> { "icu4c-oracle.json", "icu4j-oracle.json" };

    /// <summary>
    /// The fixture was written from the same Unicode version the tables came from.
    /// </summary>
    [Theory]
    [MemberData(nameof(Oracles))]
    public void FixtureIsTheSameUnicodeVersion(string oracle)
    {
        Fixture(oracle).RootElement.GetProperty("unicodeVersion").GetString().Should().Be(UnicodeData.Version);
    }

    /// <summary>
    /// The skeleton of every string in the fixture: every confusable key on its own, the
    /// curated strings, and the random ones.
    /// </summary>
    [Theory]
    [MemberData(nameof(Oracles))]
    public void SkeletonsAgree(string oracle)
    {
        List<string> disagreements = new List<string>();

        foreach (JsonElement entry in Fixture(oracle).RootElement.GetProperty("skeleton").EnumerateArray())
        {
            string input = entry[0].GetString()!;
            string expected = entry[1].GetString()!;
            string actual = Confusables.Skeleton(input);

            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                disagreements.Add($"{Hex(input)}: ICU {Hex(expected)}, library {Hex(actual)}");
            }
        }

        disagreements.Should().BeEmpty();
    }

    /// <summary>
    /// The confusable class of every pair. ICU reports flags; the library reports the most
    /// specific class, so whole-script is ICU's mixed plus whole.
    /// </summary>
    [Theory]
    [MemberData(nameof(Oracles))]
    public void ConfusableClassesAgree(string oracle)
    {
        List<string> disagreements = new List<string>();

        foreach (JsonElement entry in Fixture(oracle).RootElement.GetProperty("confusable").EnumerateArray())
        {
            string first = entry[0].GetString()!;
            string second = entry[1].GetString()!;
            int flags = entry[2].GetInt32();
            ConfusableClass expected = flags switch
            {
                0 => ConfusableClass.NotConfusable,
                1 => ConfusableClass.SingleScript,
                2 => ConfusableClass.MixedScript,
                6 => ConfusableClass.WholeScript,
                _ => throw new InvalidDataException($"Unexpected ICU flags {flags}."),
            };
            ConfusableClass actual = Confusables.Classify(first, second);

            if (expected != actual)
            {
                disagreements.Add($"{Hex(first)} vs {Hex(second)}: ICU {expected}, library {actual}");
            }
        }

        disagreements.Should().BeEmpty();
    }

    /// <summary>
    /// The restriction level of every string under the General Security Profile, which is the
    /// library's default and ICU's Recommended plus Inclusion sets.
    /// </summary>
    [Theory]
    [MemberData(nameof(Oracles))]
    public void RestrictionLevelsAgreeUnderTheProfile(string oracle)
    {
        List<string> disagreements = new List<string>();

        foreach (JsonElement entry in Fixture(oracle).RootElement.GetProperty("restrictionLevel").EnumerateArray())
        {
            string input = entry[0].GetString()!;
            RestrictionLevel expected = Enum.Parse<RestrictionLevel>(entry[1].GetString()!);
            RestrictionLevel actual = Identifiers.RestrictionLevelOf(input);

            if (expected != actual)
            {
                disagreements.Add($"{Hex(input)}: ICU {expected}, library {actual}");
            }
        }

        disagreements.Should().BeEmpty();
    }

    /// <summary>
    /// The restriction level of every string when the profile admits everything, which
    /// exercises the script logic on strings the profile would otherwise stop at step one.
    /// </summary>
    /// <remarks>
    /// One documented divergence is allowed: ICU returns Moderately Restrictive for Latin plus
    /// any script but Cyrillic, Greek and Cherokee, where the text requires a Recommended script
    /// and the library returns Minimally Restrictive. Such a case is accepted only when the
    /// non-Latin part of the string resolves to no Recommended script, which is the exact
    /// condition under which the two readings differ.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Oracles))]
    public void RestrictionLevelsAgreeUnderAnOpenProfile(string oracle)
    {
        List<string> disagreements = new List<string>();
        int documentedDivergences = 0;

        foreach (JsonElement entry in Fixture(oracle).RootElement.GetProperty("restrictionLevel").EnumerateArray())
        {
            string input = entry[0].GetString()!;
            RestrictionLevel expected = Enum.Parse<RestrictionLevel>(entry[2].GetString()!);
            RestrictionLevel actual = Identifiers.RestrictionLevelOf(input, cp => true);

            if (expected == actual)
            {
                continue;
            }

            if (expected == RestrictionLevel.ModeratelyRestrictive
                && actual == RestrictionLevel.MinimallyRestrictive
                && !Scripts.ResolvedSetWithout(input, UnicodeScript.Latin).Intersects(Scripts.Recommended))
            {
                documentedDivergences++;
                continue;
            }

            disagreements.Add($"{Hex(input)}: ICU {expected}, library {actual}");
        }

        disagreements.Should().BeEmpty();
        documentedDivergences.Should().BeGreaterThan(0, "the fixture should exercise the documented divergence");
    }

    /// <summary>
    /// The number systems of every string.
    /// </summary>
    [Theory]
    [MemberData(nameof(Oracles))]
    public void NumberSystemsAgree(string oracle)
    {
        List<string> disagreements = new List<string>();

        foreach (JsonElement entry in Fixture(oracle).RootElement.GetProperty("numerics").EnumerateArray())
        {
            string input = entry[0].GetString()!;
            List<int> expected = entry[1].EnumerateArray().Select(e => e.GetInt32()).ToList();
            IReadOnlyList<int> actual = Identifiers.NumberSystemsOf(input);

            if (!expected.SequenceEqual(actual))
            {
                disagreements.Add($"{Hex(input)}: ICU [{string.Join(" ", expected.Select(z => z.ToString("X4", CultureInfo.InvariantCulture)))}], library [{string.Join(" ", actual.Select(z => z.ToString("X4", CultureInfo.InvariantCulture)))}]");
            }
        }

        disagreements.Should().BeEmpty();
    }

    /// <summary>
    /// The four normalization forms: every code point that changes under the form, a sample of
    /// Hangul syllables, and random sequences of letters, compatibility characters and marks.
    /// </summary>
    [Theory]
    [MemberData(nameof(Oracles))]
    public void NormalizationAgrees(string oracle)
    {
        List<string> disagreements = new List<string>();
        (string section, Func<string, string> form)[] forms =
        [
            ("nfd", Normalization.Nfd),
            ("nfc", Normalization.Nfc),
            ("nfkd", Normalization.Nfkd),
            ("nfkc", Normalization.Nfkc),
        ];

        foreach ((string section, Func<string, string> form) in forms)
        {
            foreach (JsonElement entry in Fixture(oracle).RootElement.GetProperty(section).EnumerateArray())
            {
                string input = entry[0].GetString()!;
                string expected = entry[1].GetString()!;
                string actual = form(input);

                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    disagreements.Add($"{section} {Hex(input)}: ICU {Hex(expected)}, library {Hex(actual)}");
                }
            }
        }

        disagreements.Should().BeEmpty();
    }

    /// <summary>
    /// Every code point's Script, Script_Extensions, Identifier_Type, Identifier_Status,
    /// Default_Ignorable_Code_Point and decimal digit value, from ICU's runs over the whole code
    /// space. Each run is checked at both ends and in the middle.
    /// </summary>
    [Theory]
    [MemberData(nameof(Oracles))]
    public void PropertiesAgreeOverTheWholeCodeSpace(string oracle)
    {
        List<string> disagreements = new List<string>();
        int runs = 0;

        foreach (JsonElement run in Fixture(oracle).RootElement.GetProperty("properties").EnumerateArray())
        {
            runs++;
            int start = run[0].GetInt32();
            int end = run[1].GetInt32();
            string script = run[2].GetString()!;
            string extensions = run[3].GetString()!;
            string types = run[4].GetString()!;
            string status = run[5].GetString()!;
            bool ignorable = run[6].GetBoolean();
            int digit = run[7].GetInt32();

            foreach (int codePoint in new[] { start, (start + end) / 2, end })
            {
                Compare(disagreements, codePoint, "Script", script, Scripts.Code(Scripts.Of(codePoint)));
                Compare(disagreements, codePoint, "Script_Extensions", extensions, string.Join(" ", Scripts.ExtensionsOf(codePoint).Select(Scripts.Code).OrderBy(c => c, StringComparer.Ordinal)));
                Compare(disagreements, codePoint, "Identifier_Type", types, TypeNames(Identifiers.TypeOf(codePoint)));
                Compare(disagreements, codePoint, "Identifier_Status", status, Identifiers.StatusOf(codePoint).ToString());
                Compare(disagreements, codePoint, "Default_Ignorable", ignorable.ToString(), Confusables.IsDefaultIgnorable(codePoint).ToString());
                Compare(disagreements, codePoint, "digit", digit.ToString(CultureInfo.InvariantCulture), (Identifiers.DecimalDigitValue(codePoint) ?? -1).ToString(CultureInfo.InvariantCulture));
            }
        }

        runs.Should().BeGreaterThan(1000);
        disagreements.Should().BeEmpty();
    }

    private static void Compare(List<string> disagreements, int codePoint, string property, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            disagreements.Add($"U+{codePoint:X4} {property}: ICU '{expected}', library '{actual}'");
        }
    }

    private static string TypeNames(IdentifierType type)
    {
        return string.Join(" ", Enum.GetValues<IdentifierType>()
                                    .Where(t => t != IdentifierType.None && type.HasFlag(t))
                                    .Select(t => t.ToString())
                                    .OrderBy(n => n, StringComparer.Ordinal));
    }

    private static JsonDocument Fixture(string oracle)
    {
        lock (Fixtures)
        {
            if (!Fixtures.TryGetValue(oracle, out JsonDocument? document))
            {
                document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", oracle)));
                Fixtures[oracle] = document;
            }

            return document;
        }
    }

    private static string Hex(string text)
    {
        StringBuilder hex = new StringBuilder();

        foreach (char c in text)
        {
            if (hex.Length > 0)
            {
                hex.Append(' ');
            }

            hex.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }
}
