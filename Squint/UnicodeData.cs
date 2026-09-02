// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

namespace Squint;

/// <summary>
/// The data this build of the library was generated from.
/// </summary>
public static class UnicodeData
{
    /// <summary>
    /// The Unicode version of every table: the security data, the script properties, the
    /// identifier properties and the normalization data all come from the same release.
    /// </summary>
    public static string Version => CharacterData.UnicodeVersion;
}
