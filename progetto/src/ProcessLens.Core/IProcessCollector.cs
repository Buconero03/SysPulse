namespace ProcessLens.Core;

public interface IProcessCollector
{
    IReadOnlyList<ProcessSnapshot> Capture();
}
