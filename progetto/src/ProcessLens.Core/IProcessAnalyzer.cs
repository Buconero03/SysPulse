namespace ProcessLens.Core;

public interface IProcessAnalyzer
{
    ProcessInsight Analyze(ProcessSnapshot process);
}
