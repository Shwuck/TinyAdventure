using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class CallTrace
{
    #region State
    private static readonly HashSet<string> present = new();
    private static readonly HashSet<string> touched = new();
    private static readonly Dictionary<string, int> hitCounts = new(); // script.method -> count
    #endregion

    #region Defaults (overridable by GameManager)
    public static bool Verbose = true;             // per-call logs
    public static bool LogFirstHitOnly = true;     // only log first time each method is hit
    public static bool EnableFullTrace = false;    // include condensed call chain
    public static int  MaxTraceDepth = 4;          // chain length when full trace is on
    #endregion

    #region Helpers
    private static bool GMEnabled
        => GameManager.Instance == null || GameManager.Instance.EnableCallTraceReports;

    private static void ResolveSettingsFromGM(
        out bool verbose, out bool firstHitOnly, out bool fullTrace, out int depth)
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            verbose      = Verbose;
            firstHitOnly = LogFirstHitOnly;
            fullTrace    = EnableFullTrace;
            depth        = MaxTraceDepth;
            return;
        }

        // Master switch gates everything
        if (!gm.EnableCallTraceReports)
        {
            verbose      = false;
            firstHitOnly = true;
            fullTrace    = false;
            depth        = MaxTraceDepth;
            return;
        }

        verbose      = gm.CallTraceVerbose;
        firstHitOnly = gm.CallTraceLogFirstHitOnly;
        fullTrace    = gm.EnableCallTraceFullTrace;
        depth        = Mathf.Max(1, gm.CallTraceDepth);
    }
    #endregion

    #region API
    // Paste anywhere: CallTrace.Mark(this);  or  CallTrace.Mark(this, step:"Step 2: Placed villagers");
    public static void Mark(
        MonoBehaviour mb,
        string step = null,
        [CallerMemberName] string method = "Unknown")
    {
        if (mb == null || !GMEnabled) return;

        ResolveSettingsFromGM(out bool verbose, out bool firstHitOnly, out bool fullTrace, out int depth);

        string script = mb.GetType().Name;
        string key = $"{script}.{method}";

        lock (touched) touched.Add(script);

        bool firstHit;
        lock (hitCounts)
        {
            if (!hitCounts.ContainsKey(key)) hitCounts[key] = 0;
            hitCounts[key]++;
            firstHit = hitCounts[key] == 1;
        }

        if (!verbose) return;
        if (firstHitOnly && !firstHit) return;

        var stepText = string.IsNullOrEmpty(step) ? "" : $" -> {step}";

		if (fullTrace)
		{
			var chain = BuildUserTrace(depth);
			if (!string.IsNullOrEmpty(chain))
			{
				GameDebugger.Instance.LogInfo($"[CallTrace] {script}.{method}(){stepText} | Trace: {chain}");
				return;
			}
		}

        GameDebugger.Instance.LogInfo($"[CallTrace] {script}.{method}(){stepText}");
    }

    internal static void RegisterSceneScripts(IEnumerable<MonoBehaviour> behaviours)
    {
        if (!GMEnabled) return;

        lock (present)
        {
            foreach (var b in behaviours)
            {
                if (b != null && b.isActiveAndEnabled)
                    present.Add(b.GetType().Name);
            }
        }
    }

    internal static void ReportUntouched()
    {
        if (!GMEnabled) return;

        HashSet<string> missing;
        lock (present)
        lock (touched)
            missing = present.Except(touched).OrderBy(n => n).ToHashSet();

        if (missing.Count == 0)
        {
            GameDebugger.Instance.LogInfo("[CallTrace] All present scripts were touched at least once.");
        }
        else
        {
            GameDebugger.Instance.LogWarning("[CallTrace] Untouched scripts this session:\n" + string.Join("\n", missing));
        }

        if (hitCounts.Count > 0)
        {
            var summary = string.Join("\n", hitCounts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key} -> {kv.Value} calls"));
            GameDebugger.Instance.LogInfo("[CallTrace] Method call counts:\n" + summary);
        }
    }
    #endregion
	
	private static string BuildUserTrace(int depth)
	{
		var st = new System.Diagnostics.StackTrace(1, false);
		var frames = st.GetFrames();
		if (frames == null || frames.Length == 0) return null;

		string[] ignoreNamespaces = {
			"UnityEngine", "UnityEngine.UI", "UnityEngine.EventSystems", "TMPro", "System"
		};
		string[] stopAtMethods = { "ToggleNestedArea", "Update", "Start", "Awake" };

		var names = new List<string>(depth);
		foreach (var f in frames)
		{
			var m = f.GetMethod();
			if (m == null) continue;

			var dt = m.DeclaringType;
			if (dt == null) continue;

			// Skip CallTrace frames (so "Mark" never appears)
			if (dt == typeof(CallTrace)) continue;

			string ns = dt.Namespace ?? string.Empty;
			bool isIgnoredNs = ignoreNamespaces.Any(prefix => ns.StartsWith(prefix, StringComparison.Ordinal));
			if (isIgnoredNs) continue;

			string methodName = m.Name;
			int tick = methodName.IndexOf('`'); if (tick >= 0) methodName = methodName[..tick];

			// Include class for clarity: Type.Method
			string label = $"{dt.Name}.{methodName}";
			names.Add(label);

			if (stopAtMethods.Contains(m.Name)) break;
			if (names.Count >= depth) break;
		}

		if (names.Count == 0) return null;

		// Optional: if you prefer root→leaf, reverse here:
		// names.Reverse();

		return string.Join(" → ", names);
	}
	
}

public sealed class TraceBootstrap : MonoBehaviour
{
    private void Awake()
    {
        var all = FindObjectsOfType<MonoBehaviour>(true);
        CallTrace.RegisterSceneScripts(all.Where(b => b != null));
        GameDebugger.Instance.LogInfo($"[CallTrace] Registered {all.Length} behaviours present in scene.");
    }

    private void OnApplicationQuit() => CallTrace.ReportUntouched();
    private void OnDisable()          => CallTrace.ReportUntouched();
}
