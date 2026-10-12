import json
from pathlib import Path
import tempfile
import unittest
from run_full_match_playtest import inspect_peer, compare_terminals, audit_events
from plan066_campaign import scenarios, verify_attempt, qualify_pilots
from unittest.mock import patch


class EvidenceGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.summary = dict(completed=True, visibleResult=True, healthyTelemetry=True, failure='')
        self.events = [dict(index=1, kind='terminal_checkpoint', winnerMask=1, terminalReleased=True,
                            seats=[dict(seat=0, score=5, wins=0, temperature=10, life='Alive', inventory=[])]),
                       dict(index=2, kind='recorder_stopping')]
    def tearDown(self):
        self.temp.cleanup()
    def inspect(self):
        (self.root / 'summary.json').write_text(json.dumps(self.summary))
        (self.root / 'events.jsonl').write_text('\n'.join(map(json.dumps, self.events)))
        return inspect_peer(self.root)
    def test_valid_closed_terminal(self):
        self.assertTrue(self.inspect()['passed'])
    def test_missing_inventory_never_passes(self):
        del self.events[0]['seats'][0]['inventory']
        self.assertIn('missing inventory serialization', self.inspect()['problems'])
    def test_visible_failure_overrides_game_completion(self):
        self.summary['visibleResult'] = False
        self.assertFalse(self.inspect()['passed'])
    def test_dropped_sequence_fails(self):
        self.events[-1]['index'] = 3
        self.assertFalse(self.inspect()['passed'])
    def test_unclosed_receipt_fails(self):
        self.events.insert(1, dict(index=2, kind='command_received', receipt=1))
        self.events[-1]['index'] = 3
        self.assertIn('unpaired/duplicate authority receipt', self.inspect()['problems'])
    def test_cross_peer_mismatch_fails(self):
        a = self.inspect()
        self.events[0]['seats'][0]['temperature'] = 11
        b = self.inspect()
        self.assertEqual('failed', compare_terminals([a, b])['status'])
    def test_missing_checkpoint_inconclusive(self):
        self.assertEqual('inconclusive', compare_terminals([dict(terminal=None)])['status'])
    def test_malformed_terminal_does_not_crash_aggregate(self):
        for terminal in ({}, dict(seats=None), dict(seats=['invalid'])):
            self.assertEqual('inconclusive', compare_terminals([dict(terminal=terminal)])['status'])
    def test_lobby_return_required_in_current_schema(self):
        self.summary['schema'] = 2
        self.assertIn('lobby return not verified', self.inspect()['problems'])
    def test_explicit_verdict_classifies_return(self):
        self.events = [dict(index=1, kind='command_received', receipt=8),
                       dict(index=2, kind='command_verdict', receipt=8, detail='Rejected'),
                       dict(index=3, kind='command_returned', receipt=8)] + self.events
        for index, event in enumerate(self.events, 1): event['index'] = index
        self.assertEqual(0, self.inspect()['unclassified_returns'])
    def test_malformed_summary_is_a_failure_not_analyzer_crash(self):
        (self.root / 'summary.json').write_text('{')
        self.assertFalse(inspect_peer(self.root)['passed'])
    def test_malformed_event_is_a_failure(self):
        self.inspect()
        with (self.root / 'events.jsonl').open('a') as stream: stream.write('\n[]')
        self.assertIn('malformed event', inspect_peer(self.root)['problems'])
    def test_exception_after_terminal_fails(self):
        (self.root / 'player.log').write_text('NullReferenceException: teardown')
        self.assertFalse(self.inspect()['passed'])
    def test_non_finite_temperature_fails(self):
        self.events[0]['seats'][0]['temperature'] = float('nan')
        self.assertIn('temperature outside legal range', self.inspect()['problems'])
    def test_inventory_missing_identity_fails(self):
        self.events[0]['seats'][0]['inventory'] = [dict(ItemId=1)]
        self.assertIn('invalid inventory serialization', self.inspect()['problems'])
    def test_ghost_cooldown_and_target_limit(self):
        def hit(turn, source=0):
            return dict(server=True, matchEpoch=1, roundEpoch=1, turn=turn, possessionSpent=0,
                        kind='temperature_effect', detail=f'cause=ghost_grudge:source={source};target=2;before=10;after=7;requested=-3',
                        seats=[dict(seat=2, temperature=7)])
        self.assertEqual([], audit_events([hit(1), hit(3)])['problems'])
        self.assertIn('grudge reused before one-turn cooldown', audit_events([hit(1), hit(2)])['problems'])
        self.assertIn('multiple ghost damage hits on target in same turn', audit_events([hit(1), hit(1, 1)])['problems'])
    def test_possession_only_resets_in_new_match(self):
        before = dict(server=True, matchEpoch=1, possessionSpent=1)
        self.assertIn('possession spent state reverted within match', audit_events([before, dict(server=True, matchEpoch=1, possessionSpent=0)])['problems'])
        self.assertEqual([], audit_events([before, dict(server=True, matchEpoch=2, possessionSpent=0)])['problems'])
    def test_frozen_matrix_has_expected_counts_and_unique_ids(self):
        import collections
        rows = scenarios('primary')
        self.assertEqual(60, len(rows))
        self.assertEqual(60, len({r['id'] for r in rows}))
        self.assertEqual({1:16, 2:16, 3:12, 4:16}, dict(collections.Counter(r['players'] for r in rows)))
        self.assertEqual({1:2, 2:2, 3:2, 4:2}, dict(collections.Counter(r['players'] for r in scenarios('pilot'))))
    def test_resume_rejects_different_build_or_scenario(self):
        row = scenarios('pilot')[0]
        manifest = {key: row[key] for key in ('players', 'policies', 'seed', 'gameplay_seed', 'timeout')}
        manifest.update(kind='pilot', behavior='normal', build_hashes={'game.dll':'original'})
        (self.root / 'manifest.json').write_text(json.dumps(manifest))
        (self.root / 'report.json').write_text(json.dumps(dict(cohort='pilot', passed=True)))
        self.assertTrue(verify_attempt(self.root, row, 'pilot', {'game.dll':'original'})['passed'])
        with self.assertRaises(ValueError): verify_attempt(self.root, row, 'pilot', {'game.dll':'changed'})
        with self.assertRaises(ValueError): verify_attempt(self.root, dict(row, seed=0), 'pilot', {'game.dll':'original'})

    def test_censored_pilot_requires_explicit_successful_followup(self):
        row = scenarios('pilot')[-1]
        failed = dict(passed=False, peers=[dict(summary=dict(failure='TimeoutException: natural match terminal',
                      healthyTelemetry=True), invariants=dict(problems=[]), problems=['completed false/missing'])])
        with patch('plan066_campaign.scenarios', return_value=[row]):
            with patch('plan066_campaign.verify_attempt', return_value=failed):
                with self.assertRaises(ValueError): qualify_pilots(self.root, None, {})
                with self.assertRaises(ValueError): qualify_pilots(self.root, self.root/'retry', {})
            with patch('plan066_campaign.verify_attempt', side_effect=[failed, dict(passed=True)]):
                result = qualify_pilots(self.root, self.root/'retry', {})
                self.assertTrue(result[0]['original_censored'])
                self.assertEqual(str(self.root/row['id']), result[0]['original'])
                self.assertIn('retry', result[0]['qualified_evidence'])

    def test_followup_cannot_hide_non_timeout_defects(self):
        row = scenarios('pilot')[-1]
        for problem in ('temperature outside legal range', 'unpaired/duplicate authority receipt'):
            failed = dict(passed=False, peers=[dict(summary=dict(failure='TimeoutException: natural match terminal',
                          healthyTelemetry=True), invariants=dict(problems=[]), problems=[problem])])
            with patch('plan066_campaign.scenarios', return_value=[row]), patch('plan066_campaign.verify_attempt', return_value=failed):
                with self.assertRaises(ValueError): qualify_pilots(self.root, self.root/'retry', {})


if __name__ == '__main__':
    unittest.main()
