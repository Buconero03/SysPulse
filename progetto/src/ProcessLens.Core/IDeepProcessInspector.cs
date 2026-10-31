namespace ProcessLens.Core;

public interface IDeepProcessInspector
{
    ProcessSnapshot Inspect(ProcessSnapshot process);
}
