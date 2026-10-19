// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CaptureImage.Infrastructure.Tests")]

// This assembly is the native interop layer: the CsWin32 Win32 bindings, Direct3D 11 / DXGI and
// the WinRT capture interop take raw pointers, so unsafe blocks are how it talks to Windows. They
// are kept to this assembly and covered by its tests. SonarQube's "unsafe code is
// security-sensitive" rule is acknowledged once here rather than at every call site.
[assembly: SuppressMessage(
    "Security",
    "S6640:Using unsafe code blocks is security-sensitive",
    Justification = "Native Win32/Direct3D/WinRT interop layer; reviewed and tested.",
    Scope = "namespaceanddescendants",
    Target = "~N:CaptureImage.Infrastructure")]
