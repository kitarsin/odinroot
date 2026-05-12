namespace ODIN.Api.Models.Enums;

/// <summary>
/// Confidence level in behavior detection classification.
/// Used by Learned Helplessness and other subjective behavior states
/// to indicate the certainty of the detection.
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>Definitive evidence: multiple indicators or explicit timestamps confirm detection</summary>
    High,

    /// <summary>Probable evidence: one strong indicator or combined weaker indicators</summary>
    Moderate,

    /// <summary>Weak evidence: single indicator without corroboration; needs monitoring</summary>
    Low,

    /// <summary>Insufficient data to classify confidence</summary>
    Unknown = 0
}
