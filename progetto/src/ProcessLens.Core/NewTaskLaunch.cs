[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ProcessLens.NewTaskTests")]

namespace ProcessLens.Core;

public sealed record NewTaskRequest(string Program, string Arguments, string WorkingDirectory)
{
    // Fields remain separate: no shell is added and arguments are not re-tokenized or expanded.
    public static bool TryCreate(string program, string arguments, string directory,
        out NewTaskRequest? request, out string error, bool expandEnvironment = true)
        => TryCreateCore(program, arguments, directory, out request, out error,
            expandEnvironment ? Environment.ExpandEnvironmentVariables : null);

    internal static bool TryCreateCore(string program, string arguments, string directory,
        out NewTaskRequest? request, out string error, Func<string, string>? expand)
    {
        request = null; error = "";
        // Win32 expansion consumes null-terminated strings. Reject raw controls before
        // trimming or expansion, otherwise a NUL suffix can disappear on Windows.
        if (InvalidControls(program, arguments, directory))
        { error = "I campi contengono caratteri non validi o interruzioni di riga."; return false; }
        program = program.Trim(); directory = directory.Trim();
        if (expand is not null)
        { program = expand(program); directory = expand(directory); }
        if (program.Length >= 2 && program[0] == '"' && program[^1] == '"') program = program[1..^1];
        if (directory.Length >= 2 && directory[0] == '"' && directory[^1] == '"') directory = directory[1..^1];
        if (string.IsNullOrWhiteSpace(program)) { error = "Inserisci il programma da avviare."; return false; }
        if (InvalidControls(program, arguments, directory) || program.Contains('"') || directory.Contains('"'))
        { error = "I campi contengono caratteri non validi o interruzioni di riga."; return false; }
        string name = program.Replace('\\', '/').Split('/')[^1];
        string extension = System.IO.Path.GetExtension(name);
        if (program.Contains("://", StringComparison.Ordinal) || name.Length == 0 ||
            (extension.Length > 0 && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".com", StringComparison.OrdinalIgnoreCase)))
        { error = "Scegli un eseguibile (.exe o .com). Per uno script usa il relativo interprete e il campo Argomenti."; return false; }
        if ((long)program.Length + arguments.Length + 4 > 32767 || directory.Length > 32760)
        { error = "Programma, argomenti o cartella oltre il limite di lunghezza."; return false; }
        request = new(program, arguments, directory); return true;
    }

    private static bool InvalidControls(string program, string arguments, string directory) =>
        program.Any(char.IsControl) || directory.Any(char.IsControl) ||
        arguments.Any(c => char.IsControl(c) && c != '\t');
}

public sealed record NewTaskResult(bool Started, int? Pid, string Message);
public interface INewTaskLauncher { NewTaskResult Start(NewTaskRequest request); }
