#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using AbsoluteZero.Core.Player;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Build;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game
{
    // Opt-in observation only: never writes gameplay state or alters presentation targets.
    [DefaultExecutionOrder(20000)]
    public sealed class TemperatureDisplayProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public string mode;
            public int localSeat, frames, risingFrames, completedRecoveries, roundRecoveries, violations, captures;
            public float largestRise, largestAllowedRise;
        }
        readonly Report _report = new();
        readonly float[] _previous = new float[4], _start = new float[4], _end = new float[4];
        readonly bool[] _seen = new bool[4], _rising = new bool[4], _mid = new bool[4];
        IGameDataBridge _bridge;
        GameHudRefs _refs;
        StreamWriter _writer;
        string _prefix;
        bool _closed;

        public void Initialize(IGameDataBridge bridge, GameHudRefs refs, string directory)
        {
            _bridge = bridge; _refs = refs;
            Directory.CreateDirectory(directory);
            _prefix = Path.Combine(directory, System.Diagnostics.Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            _writer = new StreamWriter(_prefix + ".csv");
            _writer.WriteLine("frame,time,dt,mode,local,seat,round,phase,shown,target");
        }
        void LateUpdate()
        {
            if (_closed || _bridge == null || _bridge.LocalSeatIndex >= 4) return;
            _report.frames++; _report.mode = _bridge.CurrentMatch.Mode.ToString(); _report.localSeat = _bridge.LocalSeatIndex;
            for (byte seat = 0; seat < 4; seat++)
            {
                if (!_bridge.TryGetSeat(seat, out var snapshot)) continue;
                int slot = AZPlayerVisual.GetRemoteVisualSlot(seat, _bridge.LocalSeatIndex, _bridge.CurrentMatch.Mode == Core.Network.GameMode.Multi);
                Slider slider = seat == _bridge.LocalSeatIndex ? _refs.MyHpSlider
                    : slot >= 0 && slot < _refs.OppBars.Count ? _refs.OppBars[slot].HpSlider : null;
                if (slider == null) continue;
                float shown = slider.value * 37f;
                float target = _bridge.TryGetDisplayTemperature(seat, out var staged) ? staged : snapshot.Temperature;
                _writer.WriteLine(FormattableString.Invariant($"{Time.frameCount},{Time.time:F5},{Time.deltaTime:F5},{_report.mode},{_report.localSeat},{seat},{_bridge.CurrentMatch.RoundNumber},{_bridge.CurrentMatch.CurrentPhase},{shown:F5},{target:F5}"));
                if (_seen[seat])
                {
                    float change = shown - _previous[seat];
                    if (change > .0001f)
                    {
                        _report.risingFrames++;
                        if (change > _report.largestRise) { _report.largestRise = change; _report.largestAllowedRise = 15 * Time.deltaTime; }
                        if (change > 15 * Time.deltaTime + .01f)
                        {
                            _report.violations++;
                            Debug.LogError("[TEMP_UI] recovery jump seat=" + seat + " delta=" + change + " dt=" + Time.deltaTime);
                        }
                        if (!_rising[seat])
                        {
                            _rising[seat] = true; _start[seat] = _previous[seat]; _end[seat] = target; _mid[seat] = false;
                            if (_end[seat] - _start[seat] >= 5) Capture(seat, "start");
                        }
                        if (!_mid[seat] && _end[seat] - _start[seat] >= 5 && shown >= (_start[seat] + _end[seat]) / 2)
                        { _mid[seat] = true; Capture(seat, "mid"); }
                    }
                    if (_rising[seat] && Mathf.Abs(shown - target) < .001f)
                    {
                        _report.completedRecoveries++;
                        if (target >= 36.99f && _bridge.CurrentMatch.RoundNumber >= 2) _report.roundRecoveries++;
                        if (_end[seat] - _start[seat] >= 5) Capture(seat, "end");
                        _rising[seat] = false;
                    }
                    else if (change < -.0001f) _rising[seat] = false;
                }
                _previous[seat] = shown; _seen[seat] = true;
            }
        }
        void Capture(byte seat, string label)
        {
            if (_report.captures >= 24) return;
            ScreenCapture.CaptureScreenshot(_prefix + "-" + (++_report.captures).ToString("D2") + "-seat" + seat + "-" + label + ".png");
        }
        void Close()
        {
            if (_closed || _prefix == null) return;
            _closed = true; _writer?.Dispose();
            File.WriteAllText(_prefix + ".json", JsonUtility.ToJson(_report, true));
            Debug.Log("[TEMP_UI] " + (_report.violations == 0 ? "PASS" : "FAIL") + " mode=" + _report.mode + " seat=" + _report.localSeat
                + " rising=" + _report.risingFrames + " completed=" + _report.completedRecoveries + " round=" + _report.roundRecoveries);
        }
        void OnApplicationQuit() => Close();
        void OnDestroy() => Close();
    }
}
#endif
