// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Linq;

using AwesomeAssertions;

using Xunit;

using Squint.Uts39;

namespace Squint.Test;

/// <summary>
/// The plain-language layer, on the names a sign-up form meets.
/// </summary>
public class NamesTests
{
    /// <summary>
    /// Names people actually have, accepted by the default policy with nothing found.
    /// </summary>
    [Theory]
    [InlineData("henrik")]
    [InlineData("søren")]
    [InlineData("Müller")]
    [InlineData("Yıldız")]
    [InlineData("Ægir")]
    [InlineData("東京太郎")]
    [InlineData("Иван")]
    [InlineData("محمد")]
    [InlineData("aアー")]
    [InlineData("user_42")]
    [InlineData("")]
    public void RealNamesAreAcceptable(string name)
    {
        Inspection inspection = Names.Inspect(name);

        inspection.IsAcceptable.Should().BeTrue();
        inspection.Findings.Should().BeEmpty();
        inspection.CleanForm.Should().Be(name);
        Names.IsAcceptable(name).Should().BeTrue();
    }

    /// <summary>
    /// The finding for the case the library is named after: which character, where, which
    /// script, and what it looks like.
    /// </summary>
    [Fact]
    public void ACyrillicLetterAmongLatinIsNamedAndPlaced()
    {
        Inspection inspection = Names.Inspect("hеnrik");

        inspection.IsAcceptable.Should().BeFalse();
        Finding finding = inspection.Findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(FindingKind.MixedScripts);
        finding.Position.Should().Be(1);
        finding.Length.Should().Be(1);
        finding.Text.Should().Be("е");
        finding.Message.Should().Be("'е' (U+0435, Cyrillic) at position 1 is Cyrillic among Latin letters and looks like 'e'");
        inspection.LookalikeKey.Should().Be("henrik");
        inspection.Level.Should().Be(RestrictionLevel.MinimallyRestrictive);
    }

    /// <summary>
    /// Several foreign letters are each reported, in order.
    /// </summary>
    [Fact]
    public void EveryForeignLetterIsReported()
    {
        Inspection inspection = Names.Inspect("pаypаl");

        inspection.Findings.Select(f => f.Position).Should().Equal(1, 4);
        inspection.Findings.Should().OnlyContain(f => f.Kind == FindingKind.MixedScripts);
    }

    /// <summary>
    /// A letter above U+FFFF is two code units long, and the position after it counts both.
    /// </summary>
    [Fact]
    public void PositionsAreUtf16Indices()
    {
        Inspection inspection = Names.Inspect("a\U0001D5C9b☃");

        inspection.Findings.Select(f => (f.Kind, f.Position, f.Length)).Should().Equal((FindingKind.CompatibilityForm, 1, 2),
                                                                                       (FindingKind.NotAllowed, 4, 1));
        inspection.Findings[0].Message.Should().Be("'\U0001D5C9' (U+1D5C9, Common) at position 1 is a compatibility form of 'p'");
    }

    /// <summary>
    /// The four kinds a character can be reported as, one example each, and each with a
    /// message a form can show.
    /// </summary>
    [Theory]
    [InlineData("hen‍rik", FindingKind.Invisible, "An invisible character (U+200D, Inherited) at position 3")]
    [InlineData("ﬁle", FindingKind.CompatibilityForm, "'ﬁ' (U+FB01, Latin) at position 0 is a compatibility form of 'fi'")]
    [InlineData("henrik☃", FindingKind.NotAllowed, "'☃' (U+2603, Common) at position 6 is not allowed in a name")]
    [InlineData("a1١", FindingKind.MixedDigits, "'١' (U+0661, Arabic) at position 2 is a digit 1 from a different number system than the '0' digits before it")]
    public void EachKindHasAMessage(string name, FindingKind kind, string message)
    {
        Inspection inspection = Names.Inspect(name, NamePolicy.Anything);

        inspection.IsAcceptable.Should().BeFalse();
        inspection.Findings.Should().Contain(f => f.Kind == kind).Which.Message.Should().Be(message);
    }

    /// <summary>
    /// The policies, loosest to strictest, on one name each that sits on the boundary.
    /// </summary>
    [Theory]
    [InlineData("søren", NamePolicy.Ascii, false)]
    [InlineData("søren", NamePolicy.OneScript, true)]
    [InlineData("Amitअमित", NamePolicy.OneScript, false)]
    [InlineData("Amitअमित", NamePolicy.Relaxed, true)]
    [InlineData("Toys-Я-Us", NamePolicy.Relaxed, false)]
    [InlineData("Toys-Я-Us", NamePolicy.Anything, true)]
    [InlineData("henrik☃", NamePolicy.Anything, false)]
    public void PoliciesDrawTheLineWhereTheySay(string name, NamePolicy policy, bool acceptable)
    {
        Names.IsAcceptable(name, policy).Should().Be(acceptable);
    }

    /// <summary>
    /// Under the Ascii policy a Danish letter is reported as what it is, not ASCII, without any
    /// pretence that it is foreign to the name or looks like anything.
    /// </summary>
    [Fact]
    public void AsciiPolicyReportsEachNonAsciiLetter()
    {
        Inspection inspection = Names.Inspect("søren", NamePolicy.Ascii);

        Finding finding = inspection.Findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(FindingKind.NotAscii);
        finding.Position.Should().Be(1);
        finding.Message.Should().Be("'ø' (U+00F8, Latin) at position 1 is not ASCII");
    }

    /// <summary>
    /// A name with as many letters of one script as of another is judged by whichever came
    /// first, and every letter of the other is reported.
    /// </summary>
    [Fact]
    public void ATieGoesToTheFirstScript()
    {
        Inspection inspection = Names.Inspect("Amitअमित");

        inspection.Findings.Should().HaveCount(4);
        inspection.Findings.Should().OnlyContain(f => f.Kind == FindingKind.MixedScripts && f.Message.Contains("Devanagari among Latin letters"));
    }

    /// <summary>
    /// A space is not an identifier character in any policy: the profile is about what may be
    /// in a name, and "Amit अमित" fails on the space before anything about scripts is asked.
    /// </summary>
    [Fact]
    public void ASpaceIsNotAllowed()
    {
        Inspection inspection = Names.Inspect("Amit अमित", NamePolicy.Anything);

        Finding finding = inspection.Findings.Should().ContainSingle().Subject;
        finding.Kind.Should().Be(FindingKind.NotAllowed);
        finding.Position.Should().Be(4);
    }

    /// <summary>
    /// Acceptable exactly when nothing was found, whatever the policy: the two views cannot
    /// disagree.
    /// </summary>
    [Theory]
    [InlineData("hеnrik")]
    [InlineData("pаypаl")]
    [InlineData("a1١")]
    [InlineData("hen‍rik")]
    [InlineData("ﬁle")]
    [InlineData("Toys-Я-Us")]
    [InlineData("Amitअमित")]
    [InlineData("a\U0001D5C9b☃")]
    [InlineData("søren")]
    [InlineData("東京太郎")]
    public void AcceptableMeansNoFindings(string name)
    {
        foreach (NamePolicy policy in new[] { NamePolicy.Ascii, NamePolicy.OneScript, NamePolicy.Relaxed, NamePolicy.Anything })
        {
            Inspection inspection = Names.Inspect(name, policy);
            Identifiers.Check(name, Level(policy)).IsAccepted.Should().Be(inspection.Findings.Count == 0, $"{name} under {policy}");
        }
    }

    /// <summary>
    /// A decomposed accent is cleaned to the composed letter, and the clean form is what is
    /// stored.
    /// </summary>
    [Fact]
    public void CleanFormComposesAccents()
    {
        Inspection inspection = Names.Inspect("sébastien");

        inspection.IsAcceptable.Should().BeTrue();
        inspection.CleanForm.Should().Be("sébastien");
        Names.Clean("ﬁle").Should().Be("file");
    }

    /// <summary>
    /// Null and the sentinel are refused.
    /// </summary>
    [Fact]
    public void NullAndUndefinedAreRefused()
    {
        Action nullName = () => Names.Inspect(null!);
        Action undefined = () => Names.Inspect("henrik", NamePolicy.Undefined);

        nullName.Should().Throw<ArgumentNullException>();
        undefined.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static RestrictionLevel Level(NamePolicy policy)
    {
        return policy switch
        {
            NamePolicy.Ascii => RestrictionLevel.AsciiOnly,
            NamePolicy.OneScript => RestrictionLevel.HighlyRestrictive,
            NamePolicy.Relaxed => RestrictionLevel.ModeratelyRestrictive,
            _ => RestrictionLevel.MinimallyRestrictive,
        };
    }
}
