namespace InfTimestamper.Core.Recognition;

public static class LevenshteinDistance
{
    public static int Compute(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var cols = b.Length + 1;
        var prev = new int[cols];
        var curr = new int[cols];

        for (int j = 0; j < cols; j++) prev[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j < cols; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(prev[j] + 1, curr[j - 1] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[cols - 1];
    }
}
