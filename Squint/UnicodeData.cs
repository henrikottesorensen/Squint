// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

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
