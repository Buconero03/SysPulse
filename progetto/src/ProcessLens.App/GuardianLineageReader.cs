using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ProcessLens.App;

// The collector records PID, PPID and second-granularity timestamps, NOT a cryptographic
// process identity. A compatible PID/time relationship is never proof of parenthood.
public sealed record GuardianLineageAncestor(int Level, int Pid, string Name, string StartedLocal,
    string Evidence, string LinkStatus);
public sealed record GuardianLineageChild(int Pid, int ParentPid, string Name, string StartLocal,
    string Source, string Evidence, string LinkStatus, long StartUnix, long EventId);
public sealed record GuardianLineageReport(int FocusPid, string FocusName, string StartedLocal,
    string ExitedLocal, string FocusEvidence, int WindowMinutes, DateTime ReadUtc,
    int OtherStartsForPid, long TotalChildrenInWindow, bool Truncated,
    IReadOnlyList<GuardianLineageAncestor> Ancestors, IReadOnlyList<GuardianLineageChild> Children);

public static class GuardianLineageReader
{
    private sealed record Event(long Id, long Ts, string Kind, int Pid, int ParentPid,
        string Name, long Started, string Source);

    public static GuardianLineageReport Read(string databasePath, int focusPid, int windowMinutes = 30,
        DateTime? referenceUtc = null, int maxChildren = 200, long? focusStartEventId = null)
    {
        if (focusPid <= 0) throw new ArgumentOutOfRangeException(nameof(focusPid));
        if (windowMinutes is < 1 or > 1440) throw new ArgumentOutOfRangeException(nameof(windowMinutes));
        if (maxChildren is < 1 or > 250) throw new ArgumentOutOfRangeException(nameof(maxChildren));
        if (!File.Exists(databasePath)) throw new FileNotFoundException("Database Guardian non trovato.", databasePath);
        var now = (referenceUtc ?? DateTime.UtcNow).ToUniversalTime();
        long end = new DateTimeOffset(now).ToUnixTimeSeconds();
        long since = end - windowMinutes * 60L;

        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly,
            Pooling = false, DefaultTimeout = 2
        }.ToString());
        db.Open();
        using (var setup = db.CreateCommand())
        {
            setup.CommandTimeout = 2;
            setup.CommandText = "PRAGMA query_only=ON; PRAGMA busy_timeout=1500;";
            setup.ExecuteNonQuery();
        }
        SqliteCommand Query(string sql)
        {
            var cmd = db.CreateCommand();
            cmd.CommandTimeout = 2;
            cmd.CommandText = sql;
            return cmd;
        }
        static Event ReadEvent(SqliteDataReader r) => new(
            r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4),
            r.GetString(5), r.GetInt64(6), r.GetString(7));
        const string Fields = "id,ts,kind,pid,ppid,name,started,source";
        static string Local(long? ts) => ts is null ? "Non osservato" :
            DateTimeOffset.FromUnixTimeSeconds(ts.Value).ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);

        Event? LastStart(int pid, long at)
        {
            using var cmd = Query($"SELECT {Fields} FROM process_events WHERE pid=$pid AND ts<=$at " +
                "AND kind IN ('ProcessStarted','ProcessObservedAtStartup') ORDER BY ts DESC,id DESC LIMIT 1");
            cmd.Parameters.AddWithValue("$pid", pid);
            cmd.Parameters.AddWithValue("$at", at);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadEvent(reader) : null;
        }
        Event? LastLifecycle(int pid, long at)
        {
            using var cmd = Query($"SELECT {Fields} FROM process_events WHERE pid=$pid AND ts<=$at " +
                "AND kind IN ('ProcessStarted','ProcessObservedAtStartup','ProcessExited') ORDER BY ts DESC,id DESC LIMIT 1");
            cmd.Parameters.AddWithValue("$pid", pid);
            cmd.Parameters.AddWithValue("$at", at);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadEvent(reader) : null;
        }
        Event? FirstBoundaryAfter(Event start)
        {
            using var cmd = Query($"SELECT {Fields} FROM process_events WHERE pid=$pid AND " +
                "kind IN ('ProcessExited','ProcessStarted','ProcessObservedAtStartup') " +
                "AND (ts>$ts OR (ts=$ts AND id>$id)) AND ts<=$now " +
                "ORDER BY ts,id LIMIT 1");
            cmd.Parameters.AddWithValue("$pid", start.Pid);
            cmd.Parameters.AddWithValue("$ts", start.Ts);
            cmd.Parameters.AddWithValue("$id", start.Id);
            cmd.Parameters.AddWithValue("$now", end);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadEvent(reader) : null;
        }

        Event? root = LastStart(focusPid, end);
        if (focusStartEventId is > 0)
        {
            // Navigate a specific historical start record, not the latest reused PID.
            using var pinned = Query($"SELECT {Fields} FROM process_events WHERE id=$id AND pid=$pid " +
                "AND kind IN ('ProcessStarted','ProcessObservedAtStartup') AND ts<=$now LIMIT 1");
            pinned.Parameters.AddWithValue("$id", focusStartEventId.Value);
            pinned.Parameters.AddWithValue("$pid", focusPid);
            pinned.Parameters.AddWithValue("$now", end);
            using var pinnedReader = pinned.ExecuteReader();
            root = pinnedReader.Read() ? ReadEvent(pinnedReader) : null;
            if (root is null) throw new InvalidOperationException("Istanza storica non presente: evento eliminato dalla retention o PID diverso.");
        }
        // A later start of the same PID also ends any safe identification of this instance,
        // even when ETW missed the corresponding exit event.
        Event? rootBoundary = root is null ? null : FirstBoundaryAfter(root);
        Event? rootExit = rootBoundary?.Kind == "ProcessExited" ? rootBoundary : null;
        bool supersededWithoutExit = rootBoundary is not null && rootExit is null;
        int otherStarts;
        using (var cmd = Query("SELECT COUNT(*) FROM process_events WHERE pid=$pid AND ts >= $since " +
            "AND ts <= $now AND kind IN ('ProcessStarted','ProcessObservedAtStartup')"))
        {
            cmd.Parameters.AddWithValue("$pid", focusPid);
            cmd.Parameters.AddWithValue("$since", since);
            cmd.Parameters.AddWithValue("$now", end);
            otherStarts = checked((int)Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture));
        }

        var ancestors = new List<GuardianLineageAncestor>();
        var visited = new HashSet<int> { focusPid };
        Event? current = root;
        if (root is not null)
        {
            ancestors.Add(new GuardianLineageAncestor(0, root.Pid, root.Name, Local(root.Ts),
                root.Kind == "ProcessObservedAtStartup" ? "Inventario iniziale: avvio reale non osservato" :
                "Avvio registrato da " + root.Source + "; identita' limitata da timestamp/PID", "Nodo selezionato"));
        }
        else
        {
            ancestors.Add(new GuardianLineageAncestor(0, focusPid, "Non identificato", "Non osservato",
                "Nessun avvio disponibile nello storico conservato", "Identita' ignota"));
        }
        for (int level = 1; level <= 6 && current is not null && current.ParentPid > 0; level++)
        {
            int parentPid = current.ParentPid;
            if (!visited.Add(parentPid))
            {
                ancestors.Add(new GuardianLineageAncestor(level, parentPid, "—", "—",
                    "Ciclo o riutilizzo PID: catena interrotta", "Ambigua"));
                break;
            }
            Event? parent = LastLifecycle(parentPid, current.Ts);
            if (parent is null || parent.Kind == "ProcessExited")
            {
                ancestors.Add(new GuardianLineageAncestor(level, parentPid, "Non identificato", "Non osservato",
                    parent is null ? "Nessuna istanza del padre presente nello storico" :
                    "Ultimo evento del PID padre: terminazione", "Non verificabile"));
                break;
            }
            string confidence = parent.Ts == current.Ts ? "Ambigua" : "Compatibile";
            string evidence = parent.Kind == "ProcessObservedAtStartup"
                ? "Inventario iniziale; PPID compatibile, nascita non osservata"
                : "PPID e sequenza temporale compatibili; nessuna prova definitiva";
            if (parent.Ts == current.Ts)
                evidence = "Eventi nello stesso secondo: ordine causale non determinabile";
            ancestors.Add(new GuardianLineageAncestor(level, parent.Pid, parent.Name,
                Local(parent.Ts), evidence, confidence));
            current = parent;
        }

        // Direct children are bounded and associated only with the selected observed lifecycle
        // of the PID (latest by default, or historical event pinned by ID). Older generations with the same PID must not be mixed in.
        long lower = Math.Max(since, root?.Ts ?? since);
        long upper = Math.Min(end, rootBoundary?.Ts ?? end);
        long totalChildren = 0;
        var children = new List<GuardianLineageChild>();
        if (lower <= upper)
        {
            using (var countCmd = Query("SELECT COUNT(*) FROM process_events WHERE ppid=$pid " +
                "AND ts >= $since AND ts <= $until AND kind='ProcessStarted'"))
            {
                countCmd.Parameters.AddWithValue("$pid", focusPid);
                countCmd.Parameters.AddWithValue("$since", lower);
                countCmd.Parameters.AddWithValue("$until", upper);
                totalChildren = Convert.ToInt64(countCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            using var cmd = Query($"SELECT {Fields} FROM process_events WHERE ppid=$pid " +
                "AND ts >= $since AND ts <= $until AND kind='ProcessStarted' " +
                "ORDER BY ts DESC,id DESC LIMIT $limit");
            cmd.Parameters.AddWithValue("$pid", focusPid);
            cmd.Parameters.AddWithValue("$since", lower);
            cmd.Parameters.AddWithValue("$until", upper);
            cmd.Parameters.AddWithValue("$limit", maxChildren);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var child = ReadEvent(reader);
                bool boundary = root is not null && (child.Ts == root.Ts ||
                    rootBoundary is not null && child.Ts == rootBoundary.Ts);
                string status = root is null ? "Non verificabile" :
                    boundary ? "Ambigua" : "Compatibile";
                string evidence = root is null
                    ? "PPID uguale, ma nessun avvio padre osservato nello storico"
                    : boundary ? "Avvio figlio e confine istanza padre nello stesso secondo"
                    : root.Kind == "ProcessObservedAtStartup"
                        ? "Padre da inventario iniziale; compatibilita' temporale non probante"
                        : "PPID e durata dell'istanza compatibili; non prova certa";
                children.Add(new GuardianLineageChild(child.Pid, child.ParentPid,
                    child.Name, Local(child.Ts), child.Source, evidence, status, child.Ts, child.Id));
            }
        }
        string rootState = root is null ? "Nessun avvio osservato: identita' non verificabile" :
            root.Kind == "ProcessObservedAtStartup" ? "Presente all'inventario iniziale; nascita non osservata" :
            "Ultimo avvio osservato (" + root.Source + "); PID riutilizzabile";
        if (supersededWithoutExit) rootState += "; nuova istanza stesso PID osservata senza uscita precedente";
        if (focusStartEventId is > 0) rootState += "; istanza storica selezionata tramite ID evento (non ultimo PID)";
        if (root is not null && root.Ts < since)
            rootState += "; l'istanza precede la finestra selezionata";
        if (root is not null && otherStarts > 1)
            rootState += "; piu' avvii dello stesso PID nella finestra";
        return new GuardianLineageReport(focusPid, root?.Name ?? "Non identificato",
            Local(root?.Ts), Local(rootExit?.Ts), rootState, windowMinutes, now,
            otherStarts, totalChildren, totalChildren > children.Count, ancestors, children);
    }
}
