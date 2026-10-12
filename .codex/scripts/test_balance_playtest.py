import unittest
from analyze_balance_playtest import analyze

A=dict(id=1,source=0,target=1,item=7,copy=42)
def e(kind,data=None,**kw):
 return dict(server=True,kind='balance_'+kind,balance=data or {},time=1,round=1,turn=1,seats=[],**kw)
def trace():
 return [e('prep_open'),e('action_begin',{'action':A}),e('impact',{'action':A,'before':1,'after':0,'requested':-5,'mitigated':2,'clipped':2}),e('action_end',{'action':A})]
class BalanceTests(unittest.TestCase):
 def test_reconciled_overkill_and_defense(self):self.assertTrue(analyze(trace())['passed'])
 def test_missing_block_not_hidden_as_damage(self):
  t=trace();t[2]['balance']['mitigated']=0;self.assertFalse(analyze(t)['passed'])
 def test_damage_cannot_be_reported_as_healing(self):
  t=trace();t[2]['balance'].update(before=0,after=1);self.assertFalse(analyze(t)['passed'])
 def test_blocked_healing_is_not_prevented_damage(self):
  t=trace();t[2]['balance'].update(before=20,after=20,requested=5,mitigated=5,clipped=0,defenseItem=20,target=1)
  r=analyze(t);defense=next(x for x in r['items'] if x['item_id']==20)
  self.assertEqual(5,defense['prevented_healing']);self.assertEqual(0,defense.get('prevented_damage',0))
 def test_duplicate_action_rejected(self):
  t=trace();t.append(t[1]);self.assertFalse(analyze(t)['passed'])
 def test_missing_source_rejected(self):
  t=trace();t[2]['balance']['action']=dict(A,id=2);self.assertFalse(analyze(t)['passed'])
 def test_delayed_effect_keeps_origin(self):
  t=trace();t.insert(2,e('scheduled',{'action':A,'effect':1}));t.append(e('schedule_end',{'action':A,'effect':1,'reason':'applied'}));self.assertTrue(analyze(t)['passed'])
 def test_duplicate_delayed_resolution_rejected(self):
  t=trace();t.insert(2,e('scheduled',{'action':A,'effect':1}));t.extend([e('schedule_end',{'action':A,'effect':1})]*2);self.assertFalse(analyze(t)['passed'])
 def test_pending_schedule_not_fabricated_as_zero_effect(self):
  t=trace();t.insert(2,e('scheduled',{'action':A,'effect':3}));self.assertEqual([3],analyze(t)['scheduled_pending_at_end'])
 def test_duplicate_temperature_event_rejected(self):
  t=trace();t[2]['index']=9;t.append(t[2]);self.assertFalse(analyze(t)['passed'])
 def test_old_logs_cannot_pass_new_gate(self):self.assertFalse(analyze([])['passed'])
 def test_client_duplicates_excluded(self):
  t=trace();t.extend([dict(x,server=False) for x in t]);self.assertEqual(1,analyze(t)['actions'])
 def test_ready_requires_preparation(self):
  t=trace()+[e('ready',{'action':A,'seconds':-1})];self.assertFalse(analyze(t)['passed'])
 def test_consumption_requires_same_physical_copy(self):
  t=trace()+[e('consume',{'action':dict(A,copy=43),'reason':'limited'})];self.assertFalse(analyze(t)['passed'])
 def test_end_requires_original_identity(self):
  t=trace();t[-1]['balance']['action']=dict(A,item=8);self.assertFalse(analyze(t)['passed'])
 def test_possession_snapshots_do_not_duplicate_uses(self):
  t=trace();t.extend([e('prep_open',possessionSpent=4,possessed=2)]*3)
  r=analyze(t);self.assertEqual(1,r['ghosts'][2]['possession_spends_observed']);self.assertEqual(1,r['seats'][1]['possessed_turns_observed'])
 def test_possession_target_turns_are_distinct(self):
  t=trace();t.extend([e('prep_open',possessed=2),dict(e('prep_open',possessed=2),turn=2)])
  self.assertEqual(2,analyze(t)['seats'][1]['possessed_turns_observed'])
if __name__=='__main__':unittest.main()
