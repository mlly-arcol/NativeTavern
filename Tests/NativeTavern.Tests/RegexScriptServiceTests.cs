using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

public class RegexScriptServiceTests
{
    private static RegexScript Script(
        string pattern, string replacement,
        RegexScriptTarget target = RegexScriptTarget.AssistantOutput,
        RegexScriptMode mode = RegexScriptMode.Regex,
        bool enabled = true) => new()
    {
        Id = 1,
        Name = "test",
        Pattern = pattern,
        Replacement = replacement,
        Target = target,
        Mode = mode,
        IsEnabled = enabled,
    };

    private static string Apply(params RegexScript[] scripts) => RegexScriptService.Apply(
        "她 said （低声）：你好，世界。",
        scripts.Select(x => RegexScriptService.CompiledScript.Compile(x)),
        RegexScriptTarget.AssistantOutput);

    [Fact]
    public void RegexReplacementExpandsCaptureGroups()
    {
        var result = RegexScriptService.Apply(
            "Chapter 12: The Gate",
            [RegexScriptService.CompiledScript.Compile(Script(@"Chapter (\d+)", "第$1章"))],
            RegexScriptTarget.AssistantOutput);
        Assert.Equal("第12章: The Gate", result);
    }

    [Fact]
    public void LiteralModeDoesNotInterpretPatternAsRegex()
    {
        var result = RegexScriptService.Apply(
            "remove (parens) here",
            [RegexScriptService.CompiledScript.Compile(
                Script("(parens)", "[brackets]", mode: RegexScriptMode.Literal))],
            RegexScriptTarget.AssistantOutput);
        Assert.Equal("remove [brackets] here", result);
    }

    [Fact]
    public void RegexModeIgnoresLetterCase()
    {
        var result = Apply(Script("said", "低语"));
        Assert.Contains("她 低语 ", result);
    }

    [Fact]
    public void ScriptsForAnotherTargetAreNotApplied()
    {
        var result = Apply(Script("said", "低语", target: RegexScriptTarget.UserInput));
        Assert.Contains("said", result);
    }

    [Fact]
    public void DisabledScriptsAreSkipped()
    {
        var result = Apply(Script("said", "低语", enabled: false));
        Assert.Contains("said", result);
    }

    [Fact]
    public void InvalidPatternIsSkippedInsteadOfThrowing()
    {
        var compiled = RegexScriptService.CompiledScript.Compile(Script("([unclosed", "x"));
        Assert.Null(compiled.Regex);
        Assert.Equal("她 said （低声）：你好，世界。", Apply(Script("([unclosed", "x")));
    }

    [Fact]
    public void ValidatePatternReportsErrorsOnlyForRegexMode()
    {
        Assert.NotNull(RegexScriptService.ValidatePattern(RegexScriptMode.Regex, "([unclosed"));
        Assert.Null(RegexScriptService.ValidatePattern(RegexScriptMode.Regex, @"(\w+)"));
        Assert.Null(RegexScriptService.ValidatePattern(RegexScriptMode.Literal, "([unclosed"));
    }

    [Fact]
    public void ScriptsApplyInSequence()
    {
        var result = RegexScriptService.Apply(
            "alpha beta",
            [
                RegexScriptService.CompiledScript.Compile(Script("alpha", "one")),
                RegexScriptService.CompiledScript.Compile(Script("beta", "two")),
            ],
            RegexScriptTarget.AssistantOutput);
        Assert.Equal("one two", result);
    }
}
