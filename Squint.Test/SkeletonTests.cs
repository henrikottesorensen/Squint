// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using AwesomeAssertions;

using Xunit;

using Squint.Uts39;

namespace Squint.Test;

/// <summary>
/// The skeleton, UTS #39 section 4, by example.
/// </summary>
/// <remarks>
/// The vectors are ICU4J's <c>SpoofCheckerTest.checkSkeleton</c> and the specification's own
/// examples, so a disagreement here is a disagreement with a second implementation, not with
/// this author's reading.
/// </remarks>
public class SkeletonTests
{
    /// <summary>
    /// ICU4J's spot checks: substitutions of different lengths, from different parts of the table.
    /// </summary>
    [Theory]
    [InlineData("nochange", "nochange")]
    [InlineData("love", "love")]
    [InlineData("1ove", "love")]
    [InlineData("OOPS", "OOPS")]
    [InlineData("00PS", "OOPS")]
    [InlineData("ʹidentifier'", "'identifier'")]
    [InlineData("֜", "́")]
    [InlineData("⩴", "::=")]
    [InlineData("⑾", "(ll)")]
    [InlineData("ﷻ", "جل جلlلo")]
    [InlineData("ಃ", "ঃ")]
    [InlineData("Α", "A")]
    [InlineData("Ꮟ", "b")]
    [InlineData("\"", "''")]
    public void MatchesIcu4jsSpotChecks(string input, string expected)
    {
        Confusables.Skeleton(input).Should().Be(expected);
    }

    /// <summary>
    /// The specification's headline example: Cyrillic <c>а</c> for Latin <c>a</c>.
    /// </summary>
    [Fact]
    public void PaypalWithCyrillicAsIsConfusableWithPaypal()
    {
        Confusables.AreConfusable("paypal", "pаypаl").Should().BeTrue();
        Confusables.Skeleton("pаypаl").Should().Be("paypal");
    }

    /// <summary>
    /// Letters above U+FFFF, which the mathematical alphanumerics are, take two UTF-16 code
    /// units each. A check that walks <c>char</c>s never sees them; this one walks code points.
    /// </summary>
    [Theory]
    [InlineData("\U0001D5C9\U0001D5BA\U0001D5D2\U0001D5C9\U0001D5BA\U0001D5C5", "paypal")]
    [InlineData("hello \U0001D429\U0001D41A\U0001D432\U0001D429\U0001D41A\U0001D425 world", "hello paypal world")]
    [InlineData("\U0001D7D9\U0001D7D8", "lO")]
    public void AstralLettersAreMapped(string input, string expected)
    {
        Confusables.Skeleton(input).Should().Be(expected);
        Confusables.AreConfusable(input, expected).Should().BeTrue();
    }

    /// <summary>
    /// A zero-width space is Default_Ignorable, so it cannot split one name into two.
    /// </summary>
    [Fact]
    public void ZeroWidthSpaceIsDropped()
    {
        Confusables.Skeleton("pay\u200Bpal").Should().Be("paypal");
        Confusables.AreConfusable("pay\u200Bpal", "paypal").Should().BeTrue();
    }

    /// <summary>
    /// <c>rn</c> and <c>m</c> are confusable in plain ASCII, which is the standing reminder that
    /// the skeleton is not a cross-script check but a visual one.
    /// </summary>
    [Fact]
    public void RnAndMAreConfusable()
    {
        Confusables.AreConfusable("modern", "rnodern").Should().BeTrue();
    }

    /// <summary>
    /// Default ignorable code points vanish from the skeleton, so a zero-width joiner cannot
    /// make a second identifier.
    /// </summary>
    [Theory]
    [InlineData("hen‍rik")]
    [InlineData("hen​rik")]
    [InlineData("­henrik")]
    [InlineData("henrik﻿")]
    public void DefaultIgnorablesAreDropped(string input)
    {
        Confusables.Skeleton(input).Should().Be("henrik");
    }

    /// <summary>
    /// The skeleton works on NFD, so a precomposed letter and its decomposed form agree, and the
    /// combining mark itself is mapped: a caron becomes a breve.
    /// </summary>
    [Fact]
    public void PrecomposedAndDecomposedAgree()
    {
        Confusables.Skeleton("ž").Should().Be(Confusables.Skeleton("ž"));
        Confusables.Skeleton("ž").Should().Be("z̆");
    }

    /// <summary>
    /// The trap the table sets. <c>ǆ</c> maps to <c>d</c> + <c>ž</c>, and the final NFD leaves
    /// that as <c>d z caron</c>; but a caron typed by hand maps to a breve. So the two are not
    /// confusable by skeleton, although the text they render is the same, and the table is not
    /// idempotent here despite section 4's promise. Pinned so that nobody "fixes" it into
    /// disagreeing with every other implementation.
    /// </summary>
    [Fact]
    public void DzCaronDigraphIsNotIdempotentInTheTable()
    {
        string once = Confusables.Skeleton("ǆ");
        string twice = Confusables.Skeleton(once);

        once.Should().Be("dž");
        twice.Should().Be("dz̆");
    }

    /// <summary>
    /// The one-off <see cref="Confusables.Prototype"/> lookup agrees with the table and returns
    /// null for a character that is its own prototype.
    /// </summary>
    [Fact]
    public void PrototypeIsTheRawTableEntry()
    {
        Confusables.Prototype(0x0430).Should().Be("a");
        Confusables.Prototype('m').Should().Be("rn");
        Confusables.Prototype('a').Should().BeNull();
        Confusables.Prototype(0x10FFFF).Should().BeNull();
    }

    /// <summary>
    /// Lone surrogates are not an error: they have no prototype and pass through, so that a
    /// string anybody can type has a skeleton.
    /// </summary>
    [Fact]
    public void LoneSurrogatesPassThrough()
    {
        Confusables.Skeleton("a\uD800b").Should().Be("a\uD800b");
    }

    /// <summary>
    /// Nulls are refused rather than treated as empty.
    /// </summary>
    [Fact]
    public void NullIsRefused()
    {
        System.Action skeleton = () => Confusables.Skeleton(null!);
        skeleton.Should().Throw<System.ArgumentNullException>();
    }
}
