// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Squint;

/// <summary>
/// An immutable set of <see cref="UnicodeScript"/> values: a character's Script_Extensions, its
/// augmented script set, or a string's resolved script set (UTS #39 section 5.1).
/// </summary>
/// <remarks>
/// Four 64-bit words, so a set fits in a register file and the set operations are a handful of
/// instructions. <see cref="All"/> holds exactly the scripts the library knows, not every bit,
/// so <see cref="Count"/> and <see cref="IsAll"/> mean what they say.
/// </remarks>
public readonly struct ScriptSet : IEquatable<ScriptSet>, IEnumerable<UnicodeScript>
{
    private readonly ulong _word0;
    private readonly ulong _word1;
    private readonly ulong _word2;
    private readonly ulong _word3;

    private ScriptSet(ulong word0, ulong word1, ulong word2, ulong word3)
    {
        _word0 = word0;
        _word1 = word1;
        _word2 = word2;
        _word3 = word3;
    }

    /// <summary>
    /// The empty set. The resolved script set of a mixed-script string.
    /// </summary>
    public static ScriptSet Empty => default;

    /// <summary>
    /// Every script, which is the augmented script set of any Common or Inherited character.
    /// </summary>
    public static ScriptSet All { get; } = BuildAll();

    /// <summary>
    /// Whether the set has no scripts.
    /// </summary>
    public bool IsEmpty => (_word0 | _word1 | _word2 | _word3) == 0;

    /// <summary>
    /// Whether the set is <see cref="All"/>.
    /// </summary>
    public bool IsAll => Equals(All);

    /// <summary>
    /// The number of scripts in the set.
    /// </summary>
    public int Count => PopCount(_word0) + PopCount(_word1) + PopCount(_word2) + PopCount(_word3);

    /// <summary>
    /// Whether two sets hold the same scripts.
    /// </summary>
    public static bool operator ==(ScriptSet left, ScriptSet right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Whether two sets differ.
    /// </summary>
    public static bool operator !=(ScriptSet left, ScriptSet right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// A set of the given scripts.
    /// </summary>
    /// <exception cref="ArgumentNullException">The array is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A value is not a <see cref="UnicodeScript"/>.</exception>
    public static ScriptSet Of(params UnicodeScript[] scripts)
    {
        if (scripts is null)
        {
            throw new ArgumentNullException(nameof(scripts));
        }

        ScriptSet set = Empty;

        foreach (UnicodeScript script in scripts)
        {
            set = set.Add(script);
        }

        return set;
    }

    /// <summary>
    /// Whether the set contains the script.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="UnicodeScript"/>.</exception>
    public bool Contains(UnicodeScript script)
    {
        int bit = Validate(script);
        return (Word(bit / 64) & (1UL << (bit % 64))) != 0;
    }

    /// <summary>
    /// The set with the script added.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="UnicodeScript"/>.</exception>
    public ScriptSet Add(UnicodeScript script)
    {
        int bit = Validate(script);
        ulong mask = 1UL << (bit % 64);

        return (bit / 64) switch
        {
            0 => new ScriptSet(_word0 | mask, _word1, _word2, _word3),
            1 => new ScriptSet(_word0, _word1 | mask, _word2, _word3),
            2 => new ScriptSet(_word0, _word1, _word2 | mask, _word3),
            _ => new ScriptSet(_word0, _word1, _word2, _word3 | mask),
        };
    }

    /// <summary>
    /// The set with the script removed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a <see cref="UnicodeScript"/>.</exception>
    public ScriptSet Remove(UnicodeScript script)
    {
        int bit = Validate(script);
        ulong mask = ~(1UL << (bit % 64));

        return (bit / 64) switch
        {
            0 => new ScriptSet(_word0 & mask, _word1, _word2, _word3),
            1 => new ScriptSet(_word0, _word1 & mask, _word2, _word3),
            2 => new ScriptSet(_word0, _word1, _word2 & mask, _word3),
            _ => new ScriptSet(_word0, _word1, _word2, _word3 & mask),
        };
    }

    /// <summary>
    /// The scripts in both sets.
    /// </summary>
    public ScriptSet Intersect(ScriptSet other)
    {
        return new ScriptSet(_word0 & other._word0, _word1 & other._word1, _word2 & other._word2, _word3 & other._word3);
    }

    /// <summary>
    /// The scripts in either set.
    /// </summary>
    public ScriptSet Union(ScriptSet other)
    {
        return new ScriptSet(_word0 | other._word0, _word1 | other._word1, _word2 | other._word2, _word3 | other._word3);
    }

    /// <summary>
    /// Whether the sets share at least one script.
    /// </summary>
    public bool Intersects(ScriptSet other)
    {
        return !Intersect(other).IsEmpty;
    }

    /// <summary>
    /// Whether every script in this set is in <paramref name="other"/>.
    /// </summary>
    public bool IsSubsetOf(ScriptSet other)
    {
        return Intersect(other).Equals(this);
    }

    /// <inheritdoc/>
    public bool Equals(ScriptSet other)
    {
        return _word0 == other._word0 && _word1 == other._word1 && _word2 == other._word2 && _word3 == other._word3;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ScriptSet other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        ulong mixed = _word0;
        mixed = (mixed * 31) ^ _word1;
        mixed = (mixed * 31) ^ _word2;
        mixed = (mixed * 31) ^ _word3;
        return (int)mixed ^ (int)(mixed >> 32);
    }

    /// <summary>
    /// The scripts by four-letter code, as <c>{Latn, Cyrl}</c>; <c>{}</c> when empty; <c>ALL</c>
    /// when every script is present.
    /// </summary>
    public override string ToString()
    {
        if (IsAll)
        {
            return "ALL";
        }

        StringBuilder text = new StringBuilder("{");

        foreach (UnicodeScript script in this)
        {
            if (text.Length > 1)
            {
                text.Append(", ");
            }

            text.Append(Scripts.Code(script));
        }

        return text.Append('}').ToString();
    }

    /// <summary>
    /// The scripts in the set, in enum order.
    /// </summary>
    public IEnumerator<UnicodeScript> GetEnumerator()
    {
        for (int bit = 1; bit < ScriptData.ScriptCount; bit++)
        {
            if ((Word(bit / 64) & (1UL << (bit % 64))) != 0)
            {
                yield return (UnicodeScript)bit;
            }
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// A set from four consecutive words of a generated table.
    /// </summary>
    internal static ScriptSet FromWords(ulong[] words, int offset)
    {
        return new ScriptSet(words[offset], words[offset + 1], words[offset + 2], words[offset + 3]);
    }

    private static ScriptSet BuildAll()
    {
        ulong[] words = new ulong[4];

        for (int bit = 1; bit < ScriptData.ScriptCount; bit++)
        {
            words[bit / 64] |= 1UL << (bit % 64);
        }

        return new ScriptSet(words[0], words[1], words[2], words[3]);
    }

    private static int Validate(UnicodeScript script)
    {
        int bit = (int)script;

        if (bit < 1 || bit >= ScriptData.ScriptCount)
        {
            throw new ArgumentOutOfRangeException(nameof(script), script, "Not a script.");
        }

        return bit;
    }

    private static int PopCount(ulong value)
    {
        int count = 0;

        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }

    private ulong Word(int index)
    {
        return index switch
        {
            0 => _word0,
            1 => _word1,
            2 => _word2,
            _ => _word3,
        };
    }
}
