// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using AwesomeAssertions;

using Xunit;

using Squint.Uts39;

namespace Squint.Test;

/// <summary>
/// The three classes of confusable, UTS #39 section 4, on ICU4J's documentation examples.
/// </summary>
public class ConfusableClassTests
{
    private const string Latin = "desparejado";
    private const string Cyrillic = "ԁеѕрагејаԁо";
    private const string Mixed = "dеsраrејаdо";

    /// <summary>
    /// A word rewritten entirely in Cyrillic is a whole-script confusable of the Latin one.
    /// </summary>
    [Fact]
    public void WhollyCyrillicIsWholeScript()
    {
        Confusables.Classify(Latin, Cyrillic).Should().Be(ConfusableClass.WholeScript);
    }

    /// <summary>
    /// A word with some letters swapped for Cyrillic is a mixed-script confusable, and not
    /// whole-script because it is not itself single-script.
    /// </summary>
    [Fact]
    public void PartlyCyrillicIsMixedScript()
    {
        Confusables.Classify(Latin, Mixed).Should().Be(ConfusableClass.MixedScript);
    }

    /// <summary>
    /// A string is a single-script confusable of itself.
    /// </summary>
    [Fact]
    public void IdenticalIsSingleScript()
    {
        Confusables.Classify(Latin, Latin).Should().Be(ConfusableClass.SingleScript);
    }

    /// <summary>
    /// The specification's example: the Croatian digraph <c>ǉ</c> and the two letters it renders
    /// as, both Latin.
    /// </summary>
    [Fact]
    public void LjetoWithDigraphIsSingleScript()
    {
        Confusables.Classify("ǉeto", "ljeto").Should().Be(ConfusableClass.SingleScript);
    }

    /// <summary>
    /// Different skeletons are not confusable at all, whatever the scripts.
    /// </summary>
    [Fact]
    public void DifferentWordsAreNotConfusable()
    {
        Confusables.Classify("scope", "scopes").Should().Be(ConfusableClass.NotConfusable);
    }

    /// <summary>
    /// ICU4J's long-string case: <c>l</c> and <c>1</c> differ four times across two hundred
    /// characters, and the result is single-script.
    /// </summary>
    [Fact]
    public void LongStringsAreSingleScript()
    {
        const string first = "A long string that will overflow stack buffers.  A long string that will overflow stack buffers. "
                             + "A long string that will overflow stack buffers.  A long string that will overflow stack buffers. ";
        const string second = "A long string that wi11 overflow stack buffers.  A long string that will overflow stack buffers. "
                              + "A long string that wi11 overflow stack buffers.  A long string that will overflow stack buffers. ";

        Confusables.Classify(first, second).Should().Be(ConfusableClass.SingleScript);
    }
}
