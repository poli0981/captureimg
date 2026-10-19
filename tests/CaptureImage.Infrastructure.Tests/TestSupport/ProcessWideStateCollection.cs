// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using Xunit;

namespace CaptureImage.Infrastructure.Tests.TestSupport;

/// <summary>
/// Serializes test classes that change process-wide or desktop-wide state: global hotkeys (a
/// combination can be registered only once per desktop, so a parallel test would see a
/// false conflict) and the process priority / power-throttling mode. xUnit runs this
/// collection on its own, after the parallel collections.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessWideStateCollection
{
    public const string Name = "Process-wide state (serialized: hotkeys, process priority)";
}
