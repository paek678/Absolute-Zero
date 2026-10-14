import copy
import tempfile
import unittest
from pathlib import Path

from resume_balance_campaigns import (
    ALLOWED_TRANSIENT, digest, retryable_lobby_read, selected_entries,
    validate_cases, verify_fingerprints, execution_cases,
)
from run_balance_30_campaign import scenarios


def transient():
    return dict(
        diagnostic_errors=[dict(detail=ALLOWED_TRANSIENT, phase="none", round=0, turn=0)],
        terminal_comparison={"status": "passed"},
        peers=[dict(problems=["healthyTelemetry false/missing", "runtime error recorded"],
                    summary=dict(completed=True, visibleResult=True, returnedToLobby=True))],
    )


class RecoveryTests(unittest.TestCase):
    def test_additional_cases_follow_original_interleaved_launch_order(self):
        ordered = execution_cases(scenarios())
        self.assertEqual([c["id"] for c in ordered[:6]], ["S01", "D01", "M01", "S02", "D02", "M02"])

    def test_lobby_error_cannot_hide_a_balance_or_drop_failure(self):
        self.assertFalse(retryable_lobby_read(transient(), ["normal match gate did not pass on revalidation", "invalid candidate weights"]))
        self.assertTrue(retryable_lobby_read(transient(), ["normal match gate did not pass on revalidation"]))

    def test_exact_pre_game_lobby_failure_is_retryable(self):
        self.assertTrue(retryable_lobby_read(transient()))

    def test_gameplay_error_is_not_silently_retried(self):
        value = transient()
        value["diagnostic_errors"][0].update(phase="AttackPhase", round=1, turn=2)
        self.assertFalse(retryable_lobby_read(value))

    def test_different_lobby_operation_is_not_retryable(self):
        value = transient()
        value["diagnostic_errors"][0]["detail"] = "Error:[LobbyGateway] Update failed"
        self.assertFalse(retryable_lobby_read(value))

    def test_extra_runtime_error_is_not_ignored(self):
        value = transient()
        value["diagnostic_errors"].append(dict(detail="NullReferenceException", phase="none", round=0, turn=0))
        self.assertFalse(retryable_lobby_read(value))

    def test_game_must_complete_and_return(self):
        for field in ("completed", "visibleResult", "returnedToLobby"):
            value = transient()
            value["peers"][0]["summary"][field] = False
            self.assertFalse(retryable_lobby_read(value), field)

    def test_state_desync_blocks_retry(self):
        value = transient()
        value["terminal_comparison"]["status"] = "failed"
        self.assertFalse(retryable_lobby_read(value))

    def test_bad_authority_or_missing_closure_blocks_retry(self):
        for problem in ("unpaired/duplicate authority receipt", "missing recorder closure"):
            value = transient()
            value["peers"][0]["problems"].append(problem)
            self.assertFalse(retryable_lobby_read(value), problem)

    def test_missing_error_or_peers_is_not_retryable(self):
        value = transient()
        value["diagnostic_errors"] = []
        self.assertFalse(retryable_lobby_read(value))
        value = transient()
        value["peers"] = []
        self.assertFalse(retryable_lobby_read(value))

    def test_retry_does_not_double_count_case(self):
        fail = dict(series="repeat", case="M02", passed=False, attempt="original")
        passing = dict(series="repeat", case="M02", passed=True, attempt="attempt-01")
        duplicate = dict(passing, attempt="attempt-02")
        values = selected_entries([fail, passing, duplicate])
        self.assertEqual(len(values), 1)
        self.assertEqual(values[("repeat", "M02")]["attempt"], "attempt-01")

    def test_same_case_id_in_another_cohort_is_separate(self):
        one = dict(series="repeat", case="S01", passed=True)
        two = dict(series="additional", case="S01", passed=True)
        self.assertEqual(len(selected_entries([one, two])), 2)

    def test_later_failure_cannot_replace_passing_case(self):
        one = dict(series="repeat", case="S01", passed=True)
        two = dict(one, passed=False)
        self.assertTrue(selected_entries([one, two])[("repeat", "S01")]["passed"])

    def test_cases_are_exactly_ten_per_mode(self):
        cases = scenarios()
        validate_cases(cases)
        for broken in (cases[:-1], cases + [cases[0]], cases[:-1] + [cases[0]]):
            with self.assertRaises(ValueError):
                validate_cases(broken)
        broken = copy.deepcopy(cases)
        broken[0]["players"] = 4
        with self.assertRaises(ValueError):
            validate_cases(broken)

    def test_build_drift_is_blocked(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "game.exe"
            path.write_bytes(b"original")
            expected = {str(path): digest(path)}
            verify_fingerprints(expected)
            path.write_bytes(b"changed")
            with self.assertRaises(RuntimeError):
                verify_fingerprints(expected)
            path.unlink()
            with self.assertRaises(RuntimeError):
                verify_fingerprints(expected)


if __name__ == "__main__":
    unittest.main()
