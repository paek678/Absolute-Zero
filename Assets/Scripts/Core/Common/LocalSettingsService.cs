using System;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    public readonly struct LocalSettingsSnapshot
    {
        public readonly float Master, Bgm, Sfx;
        public readonly bool Shake;
        // -1 preserves the process's launch display mode until the user changes it.
        public readonly int Fullscreen;
        public LocalSettingsSnapshot(float master, float bgm = 1, float sfx = 1, bool shake = true, int fullscreen = -1)
        {
            Master = Normalize(master); Bgm = Normalize(bgm); Sfx = Normalize(sfx);
            Shake = shake; Fullscreen = fullscreen is 0 or 1 ? fullscreen : -1;
        }
        static float Normalize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp01(value);
        public bool Same(LocalSettingsSnapshot other) => Master == other.Master && Bgm == other.Bgm && Sfx == other.Sfx
            && Shake == other.Shake && Fullscreen == other.Fullscreen;
    }
    public interface ILocalSettingsStore
    {
        LocalSettingsSnapshot Load();
        bool Save(LocalSettingsSnapshot snapshot, out string error);
    }

    // Application-local preferences only. No match state, SDK initialization or network writes.
    public sealed class LocalSettingsService
    {
        readonly ILocalSettingsStore _store;
        bool _saving;
        public LocalSettingsSnapshot Current { get; private set; }
        public LocalSettingsSnapshot LastSaved { get; private set; }
        public string Error { get; private set; }
        public bool HasUnsaved => Error != null || !Current.Same(LastSaved);
        public event Action Changed;
        public LocalSettingsService(ILocalSettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            try { Current = LastSaved = store.Load(); }
            catch (Exception error) { Current = LastSaved = new LocalSettingsSnapshot(1); Error = error.Message; }
        }
        public void SetVolumes(float master, float bgm, float sfx) => Apply(new(master, bgm, sfx, Current.Shake, Current.Fullscreen));
        public void SetShake(bool value) => Apply(new(Current.Master, Current.Bgm, Current.Sfx, value, Current.Fullscreen));
        public void SetFullscreen(bool value) => Apply(new(Current.Master, Current.Bgm, Current.Sfx, Current.Shake, value ? 1 : 0));
        void Apply(LocalSettingsSnapshot next)
        {
            if (Current.Same(next)) return;
            Current = next;
            Changed?.Invoke(); // Apply immediately even if persistence later fails.
            Save();
        }
        public bool Save()
        {
            if (_saving) return false;
            _saving = true;
            var captured = Current;
            try
            {
                if (_store.Save(captured, out var error)) { LastSaved = captured; Error = null; }
                else Error = error ?? "Settings could not be saved.";
            }
            catch (Exception error) { Error = error.Message; }
            finally { _saving = false; }
            Changed?.Invoke();
            return !HasUnsaved;
        }
    }

    public sealed class PlayerPrefsSettingsStore : ILocalSettingsStore
    {
        readonly string _prefix;
        readonly Action _flush;
        static readonly string[] FloatKeys = { "master_volume", "bgm_volume", "sfx_volume" };
        static readonly string[] IntKeys = { "screen_shake", "fullscreen" };
        public PlayerPrefsSettingsStore(string prefix = "", Action flush = null) { _prefix = prefix; _flush = flush ?? PlayerPrefs.Save; }
        public LocalSettingsSnapshot Load() => new(
            PlayerPrefs.GetFloat(_prefix + FloatKeys[0], 1), PlayerPrefs.GetFloat(_prefix + FloatKeys[1], 1),
            PlayerPrefs.GetFloat(_prefix + FloatKeys[2], 1), PlayerPrefs.GetInt(_prefix + IntKeys[0], 1) != 0,
            PlayerPrefs.GetInt(_prefix + IntKeys[1], -1));
        public bool Save(LocalSettingsSnapshot value, out string error)
        {
            // Compensate only owned keys after a failed flush; PlayerPrefs is not a transaction.
            var floats = new float[3]; var ints = new int[2]; var existed = new bool[5];
            for (int i = 0; i < 3; i++) { existed[i] = PlayerPrefs.HasKey(_prefix + FloatKeys[i]); floats[i] = PlayerPrefs.GetFloat(_prefix + FloatKeys[i]); }
            for (int i = 0; i < 2; i++) { existed[3 + i] = PlayerPrefs.HasKey(_prefix + IntKeys[i]); ints[i] = PlayerPrefs.GetInt(_prefix + IntKeys[i]); }
            try
            {
                PlayerPrefs.SetFloat(_prefix + FloatKeys[0], value.Master);
                PlayerPrefs.SetFloat(_prefix + FloatKeys[1], value.Bgm);
                PlayerPrefs.SetFloat(_prefix + FloatKeys[2], value.Sfx);
                PlayerPrefs.SetInt(_prefix + IntKeys[0], value.Shake ? 1 : 0);
                if (value.Fullscreen < 0) PlayerPrefs.DeleteKey(_prefix + IntKeys[1]);
                else PlayerPrefs.SetInt(_prefix + IntKeys[1], value.Fullscreen);
                _flush(); error = null; return true;
            }
            catch (Exception failure)
            {
                error = failure.Message;
                try
                {
                    for (int i = 0; i < 3; i++)
                        if (existed[i]) PlayerPrefs.SetFloat(_prefix + FloatKeys[i], floats[i]); else PlayerPrefs.DeleteKey(_prefix + FloatKeys[i]);
                    for (int i = 0; i < 2; i++)
                        if (existed[3 + i]) PlayerPrefs.SetInt(_prefix + IntKeys[i], ints[i]); else PlayerPrefs.DeleteKey(_prefix + IntKeys[i]);
                    _flush();
                }
                catch (Exception rollback) { error += "; preference rollback unconfirmed: " + rollback.Message; }
                return false;
            }
        }
    }
}
