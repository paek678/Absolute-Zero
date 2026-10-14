# Additional thirty-match campaign — 2026-10-13

Status: queued after `output/balance30-20261013-repeat`. The user explicitly requested ten more matches per mode. This adds ten Solo vs Bot, ten Duel and ten four-player Multi matches, making sixty planned matches across these two cohorts; earlier interrupted data remains separate.

Evidence root: `output/balance30-20261013-additional`. `launch-request.json` freezes the thirty cases, unchanged executable/assembly hashes, observation-tool hashes, +20000 seed offset and sixty observation requirements. Production balance and existing policy lineups are preserved. Seeds do not overlap the preceding repeat cohort. Sequential Solo/Duel/Multi rotation and JSON-only raw/derived export are retained.

The queue launches only after the prerequisite certifies thirty passing matches and its runner exits. It rejects build/tool fingerprint drift and blocks on prerequisite failure or process interruption. Queue state is in `progress.json`; output is in `queue-console.txt` and, after launch, `runner-console.txt`. `completion.json` and `final-status.json` certify completion only when all three modes have ten passing records.

Preflight passed: ten cases per mode, disjoint gameplay seeds, unchanged runtime hashes, Windows process liveness, rejection of a prerequisite with only twenty-nine passing records. No gameplay or Unity assets were changed to append this cohort. All previous observation qualification limits remain: legal-choice denominators, planned-draw versus committed-grant attribution, environmental-effect source gaps, full visual ordering and human experience are not automatically certified.

Full/per-mode JSON, tables, coverage, peer events/configuration/logs/reports are retained. Analyze cohorts separately first; pooled counts must retain cohort, seed, policy and mode identity.
