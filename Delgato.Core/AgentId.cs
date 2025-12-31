namespace Delgato.Core;

/// <summary>
/// Strongly-typed identifier for an agent.
/// </summary>
public readonly record struct AgentId
{
    public string Value { get; }

    public AgentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("AgentId cannot be null or whitespace.", nameof(value));
        Value = value;
    }

    public static implicit operator string(AgentId id) => id.Value;
    public static explicit operator AgentId(string value) => new(value);

    public override string ToString() => Value;
}

