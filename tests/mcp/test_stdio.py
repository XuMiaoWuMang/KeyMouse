"""Black-box MCP contract. Run: python tests/mcp/test_stdio.py path/to/KeyMouse.exe"""

import json
import queue
import subprocess
import sys
import threading
import unittest


class Session:
    def __init__(self, *options):
        self.proc = subprocess.Popen(
            [EXE, "mcp", *options], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            text=True, encoding="utf-8", bufsize=1,
        )
        self.lines = queue.Queue()
        self.next_id = 0
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        for line in self.proc.stdout:
            self.lines.put(line)
        self.lines.put(None)

    def send(self, method, params, **extra):
        frame = dict(jsonrpc="2.0", method=method, params=params, **extra)
        self.proc.stdin.write(json.dumps(frame, ensure_ascii=False) + "\n")
        self.proc.stdin.flush()

    def request(self, method, **params):
        self.next_id += 1
        self.send(method, params, id=self.next_id)
        while True:
            line = self.lines.get(timeout=15)
            if line is None:
                raise AssertionError("Unexpected EOF from MCP server")
            frame = json.loads(line)  # No banners, logs, BOM or human output on stdout.
            if "id" in frame:
                assert frame["id"] == self.next_id
                return frame

    def call(self, name, **arguments):
        return self.request("tools/call", name=name, arguments=arguments)

    def close(self):
        self.proc.stdin.close()
        try:
            assert self.proc.wait(timeout=8) == 0
        finally:
            if self.proc.poll() is None:
                self.proc.kill()
                self.proc.wait()
            self.proc.stdout.close()


class StdioTests(unittest.TestCase):
    def connect(self, *options):
        s = Session(*options)
        self.addCleanup(s.close)
        hello = s.request("initialize", protocolVersion="2025-11-25", capabilities={},
                          clientInfo={"name": "keymouse-test", "version": "1"})["result"]
        self.assertIn("tools", hello["capabilities"])
        self.assertEqual(hello["serverInfo"]["name"], "KeyMouse")
        s.send("notifications/initialized", {})
        return s

    def test_read_only(self):
        s = self.connect()
        catalog = s.request("tools/list")["result"]["tools"]
        self.assertEqual({t["name"] for t in catalog}, {"list_windows", "read_text"})
        self.assertTrue(all(t["annotations"]["readOnlyHint"] for t in catalog))
        denied = s.call("type_text", hwnd="0x0", text="test")
        self.assertTrue("error" in denied or denied["result"].get("isError"))
        # This must remain a literal process filter, not an option or shell command.
        result = s.call("list_windows", process="--all 中文")["result"]
        structured = result["structuredContent"]
        self.assertEqual(structured["exitCode"], 0)
        self.assertEqual(structured["data"], {"windows": []})
        self.assertEqual(json.loads(result["content"][0]["text"]), structured)
        bad = s.call("read_text", hwnd="1", region=dict(x=0, y=0, width=-1, height=1))["result"]
        self.assertTrue(bad["isError"])
        self.assertEqual(bad["structuredContent"]["exitCode"], 2)

    def test_input_opt_in(self):
        s = self.connect("--allow-input")
        tools = {t["name"]: t for t in s.request("tools/list")["result"]["tools"]}
        self.assertEqual(set(tools), {"list_windows", "read_text", "click", "press_key", "type_text"})
        for name in ("click", "press_key", "type_text"):
            self.assertIn("hwnd", tools[name]["inputSchema"]["required"])
        # An invalid HWND must reach the normal window gate, return its exit code,
        # and never send even option-looking text to the active window.
        for name, args in [("type_text", {"text": "--hwnd=0 中文\n"}),
                           ("press_key", {"key": "enter"}), ("click", {"x": 0, "y": 0})]:
            result = s.call(name, hwnd="0x7fffffff", **args)["result"]
            self.assertTrue(result["isError"])
            self.assertEqual(result["structuredContent"]["exitCode"], 3)
        self.assertIn("result", s.request("ping"))


if __name__ == "__main__":
    EXE = sys.argv.pop(1)
    unittest.main()
