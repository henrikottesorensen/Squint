// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using AwesomeAssertions;

using Xunit;

using Squint.Uts39;

namespace Squint.Test;

/// <summary>
/// The library's four normalization forms against the runtime's, which on this machine is ICU's.
/// </summary>
/// <remarks>
/// The runtime's normaliser is at whatever Unicode version its ICU has, and may trail the tables
/// here, so a code point younger than it is left out rather than counted as a disagreement; see
/// <see cref="RuntimeUnicode"/> for how that version is measured. The oracle fixtures from ICU
/// 78 cover the same ground at the tables' own version, on every platform.
/// </remarks>
public class NormalizationTests
{
    /// <summary>
    /// The four forms, as the runtime names them and as the library does.
    /// </summary>
    public static TheoryData<NormalizationForm> Forms => new TheoryData<NormalizationForm>
    {
        NormalizationForm.FormD,
        NormalizationForm.FormC,
        NormalizationForm.FormKD,
        NormalizationForm.FormKC,
    };

    /// <summary>
    /// Every code point the runtime knows, one at a time, in every form.
    /// </summary>
    [Theory]
    [MemberData(nameof(Forms))]
    public void EveryAssignedCodePointAgreesWithTheRuntime(NormalizationForm form)
    {
        List<string> disagreements = new List<string>();

        for (int codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            if (codePoint >= 0xD800 && codePoint <= 0xDFFF)
            {
                continue;
            }

            if (!RuntimeUnicode.NormalizerKnows(codePoint))
            {
                continue;
            }

            string text = char.ConvertFromUtf32(codePoint);
            string expected = text.Normalize(form);
            string actual = Apply(form, text);

            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                disagreements.Add($"U+{codePoint:X4}: expected {Hex(expected)}, got {Hex(actual)}");
            }
        }

        disagreements.Should().BeEmpty();
    }

    /// <summary>
    /// The cases UAX #15 singles out: a precomposed letter with extra marks in the wrong order,
    /// a composition exclusion that must stay decomposed under NFC, a singleton, a non-starter
    /// decomposition, a blocked composition, Hangul with and without a trailing consonant, and
    /// compatibility characters that fold under the K forms only.
    /// </summary>
    [Theory]
    [InlineData("ạ́")]
    [InlineData("ậ́́")]
    [InlineData("Ǻ̧̨")]
    [InlineData("ཱཱི")]
    [InlineData("각́")]
    [InlineData("ṩ̣̇")]
    [InlineData("q̣̣̇̇")]
    [InlineData("क़")]
    [InlineData("Ω")]
    [InlineData("ཱྀ")]
    [InlineData("ẹ́")]
    [InlineData("ẹ́")]
    [InlineData("각")]
    [InlineData("가")]
    [InlineData("ﬁ")]
    [InlineData("Ａ１")]
    [InlineData("²")]
    [InlineData("𝗉𝖺𝗒𝗉𝖺𝗅")]
    [InlineData("ẛ̣")]
    [InlineData("ﷺ")]
    [InlineData(" ")]
    public void TheHardCasesAgreeWithTheRuntimeInEveryForm(string text)
    {
        Assert.SkipWhen(RuntimeUnicode.NormalizerVersion < new Version(15, 1), $"the runtime's normaliser is Unicode {RuntimeUnicode.NormalizerVersion}");

        foreach (NormalizationForm form in new[] { NormalizationForm.FormD, NormalizationForm.FormC, NormalizationForm.FormKD, NormalizationForm.FormKC })
        {
            Apply(form, text).Should().Be(text.Normalize(form), $"{Hex(text)} under {form}");
        }
    }

    /// <summary>
    /// Random sequences of decomposable letters, compatibility characters and combining marks,
    /// from a fixed seed so a failure can be rerun, in every form.
    /// </summary>
    [Theory]
    [MemberData(nameof(Forms))]
    public void RandomMarkSequencesAgreeWithTheRuntime(NormalizationForm form)
    {
        Assert.SkipWhen(RuntimeUnicode.NormalizerVersion < new Version(15, 1), $"the runtime's normaliser is Unicode {RuntimeUnicode.NormalizerVersion}");

        int[] pool =
        [
            'a', 'e', 'q', 0x00E5, 0x1EA5, 0x1E69, 0x0F73, 0xAC01, 0xD4DB, 0x0344, 0x0301, 0x0323, 0x0327,
            0x0328, 0x0307, 0x031B, 0x05B0, 0x05B9, 0x0F71, 0x0F72, 0x093C, 0x0300, 0x0308, 0x0345, 0x1D165,
            0x1D15E, 0x2126, 0x212B, 0x0374, 0x0387, 0x0958, 0x0DDA, 0x2ADC, 0x1100, 0x1161, 0x11A8, 0x0915,
            0xFB01, 0xFF21, 0x00B2, 0x1D5C9, 0x1E9B, 0x00A0, 0x2460, 0x3300, 0xFDFA, 0x0F77, 0x1F100,
        ];
        uint state = 0x9E3779B9;
        List<string> disagreements = new List<string>();

        for (int i = 0; i < 20000; i++)
        {
            StringBuilder text = new StringBuilder();
            int length = 1 + (Next(ref state) % 8);

            for (int j = 0; j < length; j++)
            {
                text.Append(char.ConvertFromUtf32(pool[Next(ref state) % pool.Length]));
            }

            string input = text.ToString();
            string expected = input.Normalize(form);
            string actual = Apply(form, input);

            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                disagreements.Add($"{Hex(input)}: expected {Hex(expected)}, got {Hex(actual)}");
            }
        }

        disagreements.Should().BeEmpty();
    }

    /// <summary>
    /// What this machine's normaliser was measured as, printed so a run's log says which
    /// Unicode version the comparisons above were made against.
    /// </summary>
    [Fact]
    public void TheRuntimeNormalizerVersionIsMeasured()
    {
        RuntimeUnicode.NormalizerVersion.Should().BeGreaterThanOrEqualTo(new Version(13, 0));
        RuntimeUnicode.NormalizerVersion.Should().BeLessThanOrEqualTo(Version.Parse(UnicodeData.Version));
    }

    /// <summary>
    /// Text with nothing to normalise comes back as the same instance, and the threshold is
    /// different for the K forms, where a no-break space is the first character that changes.
    /// </summary>
    [Fact]
    public void PlainTextIsReturnedUnchanged()
    {
        const string canonicalPlain = "henrik © ¿";
        const string compatibilityPlain = "henrik #";

        ReferenceEquals(Normalization.Nfd(canonicalPlain), canonicalPlain).Should().BeTrue();
        ReferenceEquals(Normalization.Nfc(canonicalPlain), canonicalPlain).Should().BeTrue();
        ReferenceEquals(Normalization.Nfkc(compatibilityPlain), compatibilityPlain).Should().BeTrue();
        Normalization.Nfkc("a b").Should().Be("a b");
    }

    /// <summary>
    /// The NFKC fold does part of the skeleton's job, the visual variants, and none of the
    /// cross-script part.
    /// </summary>
    [Fact]
    public void NfkcFoldsVariantsButNotScripts()
    {
        Normalization.Nfkc("𝗉𝖺𝗒𝗉𝖺𝗅").Should().Be("paypal");
        Normalization.Nfkc("ﬁle").Should().Be("file");
        Normalization.Nfkc("pаypаl").Should().Be("pаypаl");
        Normalization.Nfkc("Ω").Should().Be("Ω");
    }

    /// <summary>
    /// Nulls are refused rather than treated as empty.
    /// </summary>
    [Fact]
    public void NullIsRefused()
    {
        Action nfkc = () => Normalization.Nfkc(null!);
        nfkc.Should().Throw<ArgumentNullException>();
    }

    private static string Apply(NormalizationForm form, string text)
    {
        return form switch
        {
            NormalizationForm.FormD => Normalization.Nfd(text),
            NormalizationForm.FormC => Normalization.Nfc(text),
            NormalizationForm.FormKD => Normalization.Nfkd(text),
            _ => Normalization.Nfkc(text),
        };
    }

    private static int Next(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (int)(state & 0x7FFFFFFF);
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
