// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Squint.Uts39;

/// <summary>
/// The two lookups every generated table is built for.
/// </summary>
internal static class Tables
{
    /// <summary>
    /// For a table of run starts covering the whole code space, the index of the run holding
    /// <paramref name="codePoint"/>: the last start that is not greater than it.
    /// </summary>
    internal static int FindRun(int[] starts, int codePoint)
    {
        int low = 0;
        int high = starts.Length - 1;
        int found = -1;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);

            if (starts[middle] <= codePoint)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }

    /// <summary>
    /// For parallel tables of sorted, non-overlapping ranges, the index of the range holding
    /// <paramref name="codePoint"/>, or -1.
    /// </summary>
    internal static int FindRange(int[] starts, int[] ends, int codePoint)
    {
        int index = FindRun(starts, codePoint);

        if (index >= 0 && codePoint <= ends[index])
        {
            return index;
        }

        return -1;
    }

    /// <summary>
    /// For a sorted table of keys, the index of <paramref name="codePoint"/>, or -1.
    /// </summary>
    internal static int FindKey(int[] keys, int codePoint)
    {
        int index = FindRun(keys, codePoint);

        if (index >= 0 && keys[index] == codePoint)
        {
            return index;
        }

        return -1;
    }
}
