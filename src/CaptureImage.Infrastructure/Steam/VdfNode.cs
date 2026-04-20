namespace CaptureImage.Infrastructure.Steam;

/// <summary>
/// A single node in a parsed Valve KeyValues (VDF) document. A node is either:
/// <list type="bullet">
///   <item>a <b>leaf</b> with <see cref="Value"/> set and no children, or</item>
///   <item>a <b>branch</b> with children keyed by string (case-insensitive).</item>
/// </list>
/// </summary>
public sealed class VdfNode
{
    private static readonly IReadOnlyDictionary<string, VdfNode> EmptyChildren =
        new Dictionary<string, VdfNode>(0);

    public string Key { get; }
    public string? Value { get; }
    public IReadOnlyDictionary<string, VdfNode> Children { get; }

    /// <summary>Create a leaf node (has a value, no children).</summary>
    public static VdfNode Leaf(string key, string value) => new(key, value, EmptyChildren);

    /// <summary>Create a branch node (has children, no scalar value).</summary>
    public static VdfNode Branch(string key, IReadOnlyDictionary<string, VdfNode> children)
        => new(key, null, children);

    private VdfNode(string key, string? value, IReadOnlyDictionary<string, VdfNode> children)
    {
        Key = key;
        Value = value;
        Children = children;
    }

    public bool IsLeaf => Value is not null;

    /// <summary>Convenience indexer; returns <c>null</c> if the child does not exist.</summary>
    public VdfNode? this[string key] =>
        Children.TryGetValue(key, out var child) ? child : null;

    /// <summary>
    /// Return the scalar value of a direct child, or <c>null</c> if the child is missing
    /// or is not a leaf.
    /// </summary>
    public string? ValueOf(string key) => this[key]?.Value;

    /// <summary>Iterate every direct child that is itself a branch (not a leaf).</summary>
    public IEnumerable<VdfNode> BranchChildren()
    {
        foreach (var child in Children.Values)
        {
            if (!child.IsLeaf)
            {
                yield return child;
            }
        }
    }
}
