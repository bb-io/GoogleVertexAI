using System.Text;
using Apps.GoogleVertexAI.Models.Dto;

namespace Apps.GoogleVertexAI.Utils;

public static class XliffIssueFormatter
{
    public static string FormatIssues(List<XliffIssueDto> issues)
    {
        if (issues == null || !issues.Any())
        {
            return "No translation issues were identified.";
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("Here is an analysis of the provided translations:");
        sb.AppendLine();
        
        foreach (var issue in issues)
        {
            sb.AppendLine($"ID: {issue.Id}");
            sb.AppendLine($"Source: `{issue.Source}`");
            sb.AppendLine($"Target: `{issue.Target}`");
            sb.AppendLine($"Issue(s) identified:");
            
            var issueLines = issue.Issues.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in issueLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine($"* {line.Trim()}");
                }
            }
            
            if (issueLines.Length <= 1)
            {
                sb.AppendLine($"* {issue.Issues.Trim()}");
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}
