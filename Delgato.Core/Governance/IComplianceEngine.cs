namespace Delgato.Core.Governance;

/// <summary>
/// Enterprise compliance engine for policy enforcement.
/// Validates operations against configurable compliance rules.
/// </summary>
public interface IComplianceEngine
{
    /// <summary>
    /// Validates an operation against compliance policies.
    /// </summary>
    Task<ComplianceResult> ValidateAsync(ComplianceContext context, CancellationToken ct = default);

    /// <summary>
    /// Gets all active compliance policies.
    /// </summary>
    Task<IReadOnlyList<CompliancePolicy>> GetPoliciesAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds or updates a compliance policy.
    /// </summary>
    Task<CompliancePolicy> UpsertPolicyAsync(CompliancePolicy policy, CancellationToken ct = default);

    /// <summary>
    /// Removes a compliance policy.
    /// </summary>
    Task<bool> RemovePolicyAsync(string policyId, CancellationToken ct = default);

    /// <summary>
    /// Gets compliance report for a time range.
    /// </summary>
    Task<ComplianceReport> GetReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

/// <summary>
/// Context for compliance validation.
/// </summary>
public sealed record ComplianceContext
{
    public required string OperationType { get; init; }
    public required string ActorId { get; init; }
    public required ActorType ActorType { get; init; }
    public required string ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public IReadOnlyDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Result of compliance validation.
/// </summary>
public sealed record ComplianceResult
{
    public required bool IsCompliant { get; init; }
    public required IReadOnlyList<ComplianceViolation> Violations { get; init; }
    public required IReadOnlyList<ComplianceWarning> Warnings { get; init; }
    public required IReadOnlyList<string> AppliedPolicies { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// A compliance violation.
/// </summary>
public sealed record ComplianceViolation
{
    public required string PolicyId { get; init; }
    public required string PolicyName { get; init; }
    public required string RuleId { get; init; }
    public required string Message { get; init; }
    public required ComplianceViolationSeverity Severity { get; init; }
    public string? RemediationAdvice { get; init; }
}

/// <summary>
/// A compliance warning (non-blocking).
/// </summary>
public sealed record ComplianceWarning
{
    public required string PolicyId { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Severity of compliance violations.
/// </summary>
public enum ComplianceViolationSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Compliance policy definition.
/// </summary>
public sealed record CompliancePolicy
{
    public required string PolicyId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required bool IsEnabled { get; init; }
    public required int Priority { get; init; }
    public required IReadOnlyList<ComplianceRule> Rules { get; init; }
    public IReadOnlyList<string> ApplicableResourceTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ApplicableOperations { get; init; } = Array.Empty<string>();
    public DateTimeOffset? EffectiveFrom { get; init; }
    public DateTimeOffset? EffectiveTo { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Individual compliance rule within a policy.
/// </summary>
public sealed record ComplianceRule
{
    public required string RuleId { get; init; }
    public required string Name { get; init; }
    public required string Condition { get; init; } // Expression to evaluate
    public required string ViolationMessage { get; init; }
    public required ComplianceViolationSeverity Severity { get; init; }
    public string? RemediationAdvice { get; init; }
    public bool IsBlocking { get; init; } = true;
}

/// <summary>
/// Compliance report for a time period.
/// </summary>
public sealed record ComplianceReport
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required long TotalOperations { get; init; }
    public required long CompliantOperations { get; init; }
    public required long NonCompliantOperations { get; init; }
    public required double ComplianceRate { get; init; }
    public required IReadOnlyList<PolicyViolationSummary> ViolationsByPolicy { get; init; }
    public required IReadOnlyList<ResourceViolationSummary> ViolationsByResource { get; init; }
    public required IReadOnlyList<ComplianceViolation> TopViolations { get; init; }
}

/// <summary>
/// Summary of violations by policy.
/// </summary>
public sealed record PolicyViolationSummary
{
    public required string PolicyId { get; init; }
    public required string PolicyName { get; init; }
    public required long ViolationCount { get; init; }
    public required IReadOnlyDictionary<ComplianceViolationSeverity, long> BySeverity { get; init; }
}

/// <summary>
/// Summary of violations by resource.
/// </summary>
public sealed record ResourceViolationSummary
{
    public required string ResourceType { get; init; }
    public required long ViolationCount { get; init; }
    public required IReadOnlyList<string> TopResourceIds { get; init; }
}
