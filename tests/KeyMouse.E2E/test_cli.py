"""Black-box acceptance against a built or published CLI. Never launch a live input command."""
import concurrent.futures
import json
import pathlib
import subprocess
import sys
import tempfile
import unittest
import uuid

CLI = pathlib.Path(sys.argv.pop(1)).resolve(strict=True)


class PublicCli(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="keymouse-e2e-")
        self.addCleanup(self.tmp.cleanup)
        self.root = pathlib.Path(self.tmp.name)

    def replay(self, name, steps, *args, **document):
        flow = self.root / (name + ".json")
        report = self.root / (name + ".report.json")
        flow.write_text(json.dumps(dict(format="keymouse-flow", version=1, steps=steps, **document)), encoding="utf-8")
        p = subprocess.run([CLI, "run", flow, "--dry-run", "--report", report, *args],
                           cwd=self.root, capture_output=True, timeout=30)
        data = json.loads(report.read_bytes()) if report.exists() else None
        if data is not None:
            self.assertTrue(data["dryRun"])
            self.assertEqual(data["injectedEvents"], 0)
        return p.returncode, data

    def test_background_requires_an_explicit_unambiguous_target(self):
        # All cases must fail in parsing, before acquiring an HWND or creating a UIA client.
        window = ["--hwnd", "0x1234", "--pid", "123"]
        cases = [[], ["invoke", "--id", "Apply"], ["invoke", *window],
                 ["invoke", *window, "--control", "0"], ["invoke", *window, "--id", ""],
                 ["invoke", *window, "--id", "a", "--control", "1"],
                 ["invoke", *window, "--id", "a", "--id", "b"],
                 ["set-value", *window, "--control", "1"],
                 ["insert-text", *window, "--control", "1", "--text", ""],
                 ["set-value", *window, "--id", "a", "--text", "x" * 4097],
                 ["controls", *window, "--id", "a"], ["controls", *window, "--allow-restore"],
                 ["invoke", *window, "--id", "a", "--text", "unexpected"]]
        for args in cases:
            with self.subTest(args=args[:4]):
                p = subprocess.run([CLI, "background", *args], cwd=self.root, capture_output=True, timeout=5)
                self.assertEqual(p.returncode, 2)

    def test_ten_concurrent_jobs_keep_their_reports_and_literal_text(self):
        def one(i):
            text = "--process" + "x" * i
            code, report = self.replay(str(i), [{"type": "type", "text": text, "intervalMs": 0}])
            self.assertEqual(code, 0)
            self.assertEqual(report["suppressedEvents"], len(text)*2)
            self.assertEqual(pathlib.Path(report["script"]).name, f"{i}.json")
        with concurrent.futures.ThreadPoolExecutor(max_workers=10) as pool:
            list(pool.map(one, range(10)))

    def test_edit_synthetic_recording_then_replay(self):
        steps = [{"type": "keydown", "combo": "a", "atMs": 1}, {"type": "keyup", "combo": "a", "atMs": 4}]
        before = self.replay("before", steps)[1]
        steps.insert(1, {"type": "sleep", "ms": 500})
        after = self.replay("after", steps)[1]
        self.assertEqual((before["succeeded"], after["succeeded"]), (2, 3))
        self.assertEqual((before["suppressedEvents"], after["suppressedEvents"]), (2, 2))

    def test_literal_global_options_never_change_the_target(self):
        for i, text in enumerate(["--stdin", "--allow-restore", "--title=not-a-window", "-n", "你好 世界"]):
            with self.subTest(text=text):
                code, report = self.replay(str(i), [{"type": "type", "text": text, "intervalMs": 0}])
                self.assertEqual(code, 0)
                self.assertEqual(report["suppressedEvents"], 2*len(text))

    def test_no_target_match_means_no_input_in_either_restore_mode(self):
        for allow in (False, True):
            code, report = self.replay(str(allow), [{"type": "keydown", "combo": "a", "target": {"process": str(uuid.uuid4())}}], allowRestore=allow)
            self.assertEqual((code, report["suppressedEvents"]), (3, 0))

    def test_keep_going_reports_usage_error_and_next_step(self):
        code, report = self.replay("badkey", [{"type": "keydown", "combo": "km-missing-key"}, {"type": "keyup", "combo": "a"}], "--keep-going")
        self.assertEqual(code, 2)
        self.assertEqual([c["exitCode"] for c in report["commands"]], [2, 0])
        self.assertEqual(report["suppressedEvents"], 1)


if __name__ == "__main__":
    unittest.main()
