"""Report-contract tests; placeholder bytes are not rendering evidence."""
import tempfile
import unittest
from pathlib import Path
from run_visual_four_player import complete_presentation_captures


class CaptureReportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        # A combat and a death-only presentation share a single preparation phase.
        self.names = ["host.1.turn-01-prep.png", "host.2.settled-seq-1.png",
                      "host.3.settled-seq-2.png", "host.4.final-turn-limit.png"]
        for name in self.names:
            (self.root / name).write_bytes(b"report-test-placeholder")
        self.result = dict(capture_records=self.names.copy(), screenshots=self.names.copy(),
                           checkpoints=["seq=1 local=0", "seq=2 local=0"])

    def test_death_presentation_does_not_require_a_second_preparation(self):
        self.assertTrue(complete_presentation_captures(self.root, self.result))

    def test_overwritten_frame_is_rejected(self):
        self.result["capture_records"].append(self.names[0])
        self.assertFalse(complete_presentation_captures(self.root, self.result))

    def test_missing_settled_frame_is_rejected(self):
        self.result["capture_records"].remove(self.names[2])
        self.assertFalse(complete_presentation_captures(self.root, self.result))

    def test_empty_frame_is_rejected(self):
        (self.root / self.names[1]).write_bytes(b"")
        self.assertFalse(complete_presentation_captures(self.root, self.result))

    def test_missing_file_is_rejected(self):
        (self.root / self.names[1]).unlink()
        self.assertFalse(complete_presentation_captures(self.root, self.result))


if __name__ == "__main__":
    unittest.main()
