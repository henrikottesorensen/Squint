// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;

using AwesomeAssertions;

using Xunit;

using Squint.Uts39;

namespace Squint.Test;

/// <summary>
/// Script properties and the resolved script set, UTS #39 section 5.1, on the specification's
/// Table 1a.
/// </summary>
public class ScriptTests
{
    /// <summary>
    /// Table 1a, row by row: the resolved script set of each example string.
    /// </summary>
    [Theory]
    [InlineData("Circle", "Latn")]
    [InlineData("СігсӀе", "Cyrl")]
    [InlineData("Сirсlе", "")]
    [InlineData("Circ1e", "Latn")]
    [InlineData("C\U0001D5C2\U0001D5CB\U0001D5BC\U0001D5C5\U0001D5BE", "Latn")]
    [InlineData("〆切", "Hani Hanb Jpan Kore")]
    [InlineData("ねガ", "Jpan")]
    public void ResolvedScriptSetsMatchTable1a(string text, string expected)
    {
        ScriptSet expectedSet = ScriptSet.Of(expected.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToArray());

        Scripts.ResolvedSetOf(text).ToString().Should().Be(expectedSet.ToString());
    }

    /// <summary>
    /// Table 1a's single-script column.
    /// </summary>
    [Theory]
    [InlineData("Circle", true)]
    [InlineData("Сirсlе", false)]
    [InlineData("Circ1e", true)]
    [InlineData("〆切", true)]
    public void SingleScriptMatchesTable1a(string text, bool expected)
    {
        Scripts.IsSingleScript(text).Should().Be(expected);
    }

    /// <summary>
    /// The five augmentation rules of section 5.1, one character each.
    /// </summary>
    [Fact]
    public void HanIsAugmentedWithAllThreeWritingSystems()
    {
        Scripts.AugmentedSetOf(0x5207).ToString().Should().Be(ScriptSet.Of(UnicodeScript.Han, UnicodeScript.HanWithBopomofo, UnicodeScript.Japanese, UnicodeScript.Korean).ToString());
    }

    /// <summary>
    /// Hiragana and Katakana become Japanese; Hangul becomes Korean; Bopomofo becomes Han with
    /// Bopomofo.
    /// </summary>
    [Theory]
    [InlineData(0x306D, "Hira Jpan")]
    [InlineData(0x30AC, "Kana Jpan")]
    [InlineData(0xAC00, "Hang Kore")]
    [InlineData(0x3105, "Bopo Hanb")]
    public void EastAsianScriptsAreAugmented(int codePoint, string expected)
    {
        Scripts.AugmentedSetOf(codePoint).ToString().Should().Be(ScriptSet.Of(expected.Split(' ').Select(Parse).ToArray()).ToString());
    }

    /// <summary>
    /// Common and Inherited characters count as every script: a digit, a hyphen, a variation
    /// selector.
    /// </summary>
    [Theory]
    [InlineData('1')]
    [InlineData('-')]
    [InlineData(0xFE00)]
    public void CommonAndInheritedAreEveryScript(int codePoint)
    {
        Scripts.AugmentedSetOf(codePoint).IsAll.Should().BeTrue();
    }

    /// <summary>
    /// The trap in the rule above: it is Script_Extensions that decides, not Script. The
    /// combining acute accent is Inherited by Script, yet since Unicode 16 ScriptExtensions.txt
    /// lists the eight scripts that use it, so it is not every script and a string of Latin
    /// letters plus one acute accent is single-script only because Latin is among the eight.
    /// </summary>
    [Fact]
    public void CombiningAcuteIsNotEveryScript()
    {
        Scripts.Of(0x0301).Should().Be(UnicodeScript.Inherited);
        Scripts.AugmentedSetOf(0x0301).IsAll.Should().BeFalse();
        Scripts.AugmentedSetOf(0x0301).Contains(UnicodeScript.Latin).Should().BeTrue();
        Scripts.AugmentedSetOf(0x0301).Contains(UnicodeScript.Arabic).Should().BeFalse();
        Scripts.ResolvedSetOf("á").ToString().Should().Be("{Latn}");
        Scripts.ResolvedSetOf("ا\u0301").IsEmpty.Should().BeTrue();
    }

    /// <summary>
    /// The Script property alone, without extensions, for a few characters whose value is well known.
    /// </summary>
    [Theory]
    [InlineData('a', UnicodeScript.Latin)]
    [InlineData(0x0430, UnicodeScript.Cyrillic)]
    [InlineData(0x03B1, UnicodeScript.Greek)]
    [InlineData('1', UnicodeScript.Common)]
    [InlineData(0x0301, UnicodeScript.Inherited)]
    [InlineData(0x0378, UnicodeScript.Unknown)]
    [InlineData(0x10FFFF, UnicodeScript.Unknown)]
    public void ScriptPropertyIsAsExpected(int codePoint, UnicodeScript expected)
    {
        Scripts.Of(codePoint).Should().Be(expected);
    }

    /// <summary>
    /// ICU4J's restriction-level tests lean on U+303C having Script_Extensions of exactly Han,
    /// Hiragana and Katakana, which is a useful example of a set that is not one script.
    /// </summary>
    [Fact]
    public void MasuMarkHasThreeScriptExtensions()
    {
        Scripts.ExtensionsOf(0x303C).ToString().Should().Be(ScriptSet.Of(UnicodeScript.Han, UnicodeScript.Hiragana, UnicodeScript.Katakana).ToString());
    }

    /// <summary>
    /// A character with no line in ScriptExtensions.txt has its Script as its only extension.
    /// </summary>
    [Fact]
    public void ExtensionsDefaultToTheScript()
    {
        Scripts.ExtensionsOf('a').ToString().Should().Be(ScriptSet.Of(UnicodeScript.Latin).ToString());
    }

    /// <summary>
    /// Codes and names round-trip through the enum, for a property value and for a writing system.
    /// </summary>
    [Theory]
    [InlineData(UnicodeScript.Latin, "Latn", "Latin")]
    [InlineData(UnicodeScript.OldItalic, "Ital", "Old_Italic")]
    [InlineData(UnicodeScript.Japanese, "Jpan", "Japanese")]
    [InlineData(UnicodeScript.Unknown, "Zzzz", "Unknown")]
    public void CodesAndNamesRoundTrip(UnicodeScript script, string code, string name)
    {
        Scripts.Code(script).Should().Be(code);
        Scripts.Name(script).Should().Be(name);
        Scripts.TryParse(code, out UnicodeScript fromCode).Should().BeTrue();
        fromCode.Should().Be(script);
        Scripts.TryParse(name, out UnicodeScript fromName).Should().BeTrue();
        fromName.Should().Be(script);
    }

    /// <summary>
    /// A code that is no script parses to the sentinel and false, not to Unknown-and-true:
    /// Unknown is a real script, the one every unassigned code point has.
    /// </summary>
    [Theory]
    [InlineData("Xxxx")]
    [InlineData("Undefined")]
    [InlineData("")]
    public void ACodeThatIsNoScriptDoesNotParse(string code)
    {
        Scripts.TryParse(code, out UnicodeScript script).Should().BeFalse();
        script.Should().Be(UnicodeScript.Undefined);
    }

    /// <summary>
    /// The sentinel is refused wherever a script is expected, like any other non-value.
    /// </summary>
    [Fact]
    public void TheSentinelIsNotAScript()
    {
        System.Action add = () => ScriptSet.Empty.Add(UnicodeScript.Undefined);
        System.Action code = () => Scripts.Code(UnicodeScript.Undefined);

        add.Should().Throw<System.ArgumentOutOfRangeException>();
        code.Should().Throw<System.ArgumentOutOfRangeException>();
        ScriptSet.All.Contains(UnicodeScript.Unknown).Should().BeTrue();
    }

    /// <summary>
    /// The set operations, on values large enough to sit in different words of the set.
    /// </summary>
    [Fact]
    public void ScriptSetOperationsBehave()
    {
        ScriptSet latinAndKorean = ScriptSet.Of(UnicodeScript.Latin, UnicodeScript.Korean);
        ScriptSet latinAndGreek = ScriptSet.Of(UnicodeScript.Latin, UnicodeScript.Greek);

        latinAndKorean.Count.Should().Be(2);
        latinAndKorean.Intersect(latinAndGreek).ToString().Should().Be("{Latn}");
        latinAndKorean.Union(latinAndGreek).Count.Should().Be(3);
        latinAndKorean.Intersects(latinAndGreek).Should().BeTrue();
        latinAndKorean.Remove(UnicodeScript.Latin).Intersects(latinAndGreek).Should().BeFalse();
        latinAndKorean.IsSubsetOf(ScriptSet.All).Should().BeTrue();
        ScriptSet.All.IsSubsetOf(latinAndKorean).Should().BeFalse();
        ScriptSet.Empty.IsEmpty.Should().BeTrue();
        ScriptSet.All.IsAll.Should().BeTrue();
        ScriptSet.All.Count.Should().Be(System.Enum.GetValues<UnicodeScript>().Length - 1, "every script but the Undefined sentinel");
        ScriptSet.All.Contains(UnicodeScript.Unknown).Should().BeTrue();
        latinAndKorean.ToString().Should().Be("{Latn, Kore}");
        ScriptSet.Empty.ToString().Should().Be("{}");
        ScriptSet.All.ToString().Should().Be("ALL");
        latinAndKorean.ToList().Should().Equal(UnicodeScript.Latin, UnicodeScript.Korean);
        (latinAndKorean == ScriptSet.Of(UnicodeScript.Korean, UnicodeScript.Latin)).Should().BeTrue();
        latinAndKorean.GetHashCode().Should().Be(ScriptSet.Of(UnicodeScript.Korean, UnicodeScript.Latin).GetHashCode());
    }

    /// <summary>
    /// The Recommended set is UAX #31 Table 5: thirty entries, the two special values included,
    /// and Cherokee and Bopomofo not among them.
    /// </summary>
    [Fact]
    public void RecommendedIsTable5()
    {
        Scripts.Recommended.Count.Should().Be(30);
        Scripts.Recommended.Contains(UnicodeScript.Common).Should().BeTrue();
        Scripts.Recommended.Contains(UnicodeScript.Tibetan).Should().BeTrue();
        Scripts.Recommended.Contains(UnicodeScript.Cherokee).Should().BeFalse();
        Scripts.Recommended.Contains(UnicodeScript.Bopomofo).Should().BeFalse();
    }

    private static UnicodeScript Parse(string code)
    {
        Scripts.TryParse(code, out UnicodeScript script).Should().BeTrue($"{code} should be a script code");
        return script;
    }
}
