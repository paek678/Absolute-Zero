"""A host phase marker alone cannot arm a client recovery assertion."""
import tempfile
import unittest
from pathlib import Path
from run_validation_matrix import clients_observed_game


class HostExitBarrierTests(unittest.TestCase):
    def test_all_remote_probes_must_observe_game(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            names = ["host", "client1", "client2", "client3"]
            (folder / "host.log").write_text("[MATRIX] STATE local=0 phase=PrepPhase")
            self.assertFalse(clients_observed_game(folder, names))
            for index in range(1, 4):
                path = folder / f"client{index}.log"
                path.write_text("Connected; loading GameScene")
                self.assertFalse(clients_observed_game(folder, names))
                path.write_text(f"[MATRIX] STATE local={index} phase=PrepPhase")
            self.assertTrue(clients_observed_game(folder, names))
            self.assertFalse(clients_observed_game(folder, ["host"]))


if __name__ == "__main__":
    unittest.main()
