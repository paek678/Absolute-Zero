using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Behavior;
using UnityEngine;

namespace AbsoluteZero.Validation.Behavior
{
    // Runs only when its isolated fixture scene is explicitly loaded.
    public sealed class Plan036BehaviorProbe : MonoBehaviour
    {
        public BehaviorGraph SourceGraph;
        [NonSerialized] public bool AllowCompletion;
        [NonSerialized] public int Setups, Starts, Updates, Completions, Ends, Teardowns;

        private BehaviorGraphAgent _agent;
        private bool _finished;
        private float _startedAt;
        private readonly List<string> _checks = new();

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public string unity, environment, failure, utc;
            public string[] checks;
            public int setups, starts, updates, completions, ends, teardowns;
        }

        private IEnumerator Start()
        {
            _startedAt = Time.realtimeSinceStartup;
            if (!Check(SourceGraph != null, "serialized graph reference loaded")) yield break;
            _agent = gameObject.AddComponent<BehaviorGraphAgent>();
            _agent.NetcodeRunOnlyOnOwner = false;
            _agent.Graph = SourceGraph;
            _agent.Init();
            if (!Check(_agent.Graph != SourceGraph && Setups == 1, "isolated runtime instance initialized")) yield break;

            AllowCompletion = true;
            _agent.Start();
            yield return WaitUntilStopped();
            if (_finished || !Check(Starts == 1 && Completions == 1 && Ends == 1,
                    "custom node starts updates and completes once")) yield break;

            AllowCompletion = false;
            _agent.Restart();
            yield return null;
            yield return null;
            if (!Check(Starts == 2 && _agent.Graph.IsRunning, "second run remains pending")) yield break;
            _agent.End();
            int updatesAtCancel = Updates;
            _agent.End();
            yield return null;
            yield return null;
            if (!Check(!_agent.Graph.IsRunning && Ends == 2 && Completions == 1 && Updates == updatesAtCancel,
                    "cancel is idempotent with no late updates or completion")) yield break;

            _agent.enabled = false;
            yield return null;
            _agent.enabled = true;
            yield return null;
            if (!Check(Starts == 2 && !_agent.Graph.IsRunning, "re-enable does not revive cancelled run")) yield break;

            AllowCompletion = true;
            _agent.Restart();
            yield return WaitUntilStopped();
            if (_finished || !Check(Starts == 3 && Completions == 2 && Ends == 3,
                    "explicit restart completes after cancellation")) yield break;

            AllowCompletion = false;
            _agent.Restart();
            yield return null;
            Destroy(_agent);
            yield return null;
            if (!Check(Starts == 4 && Completions == 2 && Ends == 4 && Teardowns == 1,
                    "destroy ends pending node and tears down instance once")) yield break;
            if (!Check(!SourceGraph.IsRunning, "source asset remains unstarted")) yield break;
            Finish(true, null);
        }

        private IEnumerator WaitUntilStopped()
        {
            while (!_finished && _agent != null && _agent.Graph.IsRunning) yield return null;
        }

        private void Update()
        {
            if (!_finished && _startedAt > 0 && Time.realtimeSinceStartup - _startedAt > 10)
                Finish(false, "fixture watchdog expired");
        }

        private bool Check(bool passed, string assertion)
        {
            if (!passed) { Finish(false, assertion); return false; }
            _checks.Add(assertion);
            return true;
        }

        private void Finish(bool passed, string failure)
        {
            if (_finished) return;
            _finished = true;
            if (!passed && _agent != null) { _agent.End(); _agent.enabled = false; }
            var report = new Report
            {
                passed = passed, failure = failure, unity = Application.unityVersion,
                environment = Application.isEditor ? "Editor PlayMode" : "Windows Player",
                utc = DateTime.UtcNow.ToString("O"), checks = _checks.ToArray(),
                setups = Setups, starts = Starts, updates = Updates,
                completions = Completions, ends = Ends, teardowns = Teardowns
            };
            string path = GetReportPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            if (passed) Debug.Log("[PLAN036-T00] PASS " + path);
            else Debug.LogError("[PLAN036-T00] FAIL " + failure + " " + path);
            if (!Application.isEditor) Application.Quit(passed ? 0 : 1);
        }

        private static string GetReportPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--plan036-result");
            if (index >= 0 && index + 1 < args.Length) return Path.GetFullPath(args[index + 1]);
            if (Application.isEditor)
                return Path.GetFullPath("output/validation/plan036/t00_20260928/behavior-editor.json");
            return Path.Combine(Application.persistentDataPath, "plan036-behavior-player.json");
        }

        private void OnDestroy()
        {
            if (_agent != null) _agent.End();
        }
    }
}
