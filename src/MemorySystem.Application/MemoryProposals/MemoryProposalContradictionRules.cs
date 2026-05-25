namespace MemorySystem.Application.MemoryProposals;

internal static class MemoryProposalContradictionRules
{
    private static readonly IReadOnlySet<(string First, string Second)> OppositeObjectTerms =
        new HashSet<(string First, string Second)>
        {
            ("accepted", "rejected"),
            ("allowed", "blocked"),
            ("allowed", "disallowed"),
            ("approved", "rejected"),
            ("can", "cannot"),
            ("enabled", "disabled"),
            ("must", "must not"),
            ("on", "off"),
            ("required", "not required"),
            ("should", "should not"),
            ("true", "false"),
            ("use", "do not use"),
            ("yes", "no")
        };

    public static bool AreContradictory(string existingObject, string proposedObject)
    {
        var existing = Normalize(existingObject);
        var proposed = Normalize(proposedObject);

        if (string.IsNullOrWhiteSpace(existing)
            || string.IsNullOrWhiteSpace(proposed)
            || string.Equals(existing, proposed, StringComparison.Ordinal))
        {
            return false;
        }

        return HasOppositeTerms(existing, proposed)
            || HasDirectNegation(existing, proposed)
            || HasUseNegation(existing, proposed);
    }

    private static bool HasOppositeTerms(string left, string right)
    {
        foreach (var (first, second) in OppositeObjectTerms)
        {
            if (HasMatchingSuffix(left, right, first, second)
                || HasMatchingSuffix(left, right, second, first))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMatchingSuffix(string left, string right, string leftTerm, string rightTerm)
    {
        return TryRemoveLeadingTerm(left, leftTerm, out var leftSuffix)
            && TryRemoveLeadingTerm(right, rightTerm, out var rightSuffix)
            && string.Equals(leftSuffix, rightSuffix, StringComparison.Ordinal);
    }

    private static bool TryRemoveLeadingTerm(string value, string term, out string suffix)
    {
        if (string.Equals(value, term, StringComparison.Ordinal))
        {
            suffix = string.Empty;
            return true;
        }

        var termWithSpace = term + " ";
        if (value.StartsWith(termWithSpace, StringComparison.Ordinal))
        {
            suffix = value[termWithSpace.Length..];
            return true;
        }

        suffix = string.Empty;
        return false;
    }

    private static bool HasDirectNegation(string left, string right)
    {
        return HasNegationPrefix(left, right) || HasNegationPrefix(right, left);
    }

    private static bool HasNegationPrefix(string value, string other)
    {
        return string.Equals(value, "not " + other, StringComparison.Ordinal)
            || string.Equals(value, "no " + other, StringComparison.Ordinal)
            || string.Equals(value, "never " + other, StringComparison.Ordinal)
            || string.Equals(value, "do not " + other, StringComparison.Ordinal)
            || string.Equals(value, "don't " + other, StringComparison.Ordinal)
            || string.Equals(value, "does not " + other, StringComparison.Ordinal);
    }

    private static bool HasUseNegation(string left, string right)
    {
        return HasMatchingSuffix(left, right, "use", "do not use")
            || HasMatchingSuffix(left, right, "use", "don't use")
            || HasMatchingSuffix(left, right, "use", "never use")
            || HasMatchingSuffix(left, right, "uses", "does not use")
            || HasMatchingSuffix(right, left, "use", "do not use")
            || HasMatchingSuffix(right, left, "use", "don't use")
            || HasMatchingSuffix(right, left, "use", "never use")
            || HasMatchingSuffix(right, left, "uses", "does not use");
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().TrimEnd('.', '!', '?').ToLowerInvariant();
        return string.Join(' ', trimmed.Split(
            new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries));
    }
}
