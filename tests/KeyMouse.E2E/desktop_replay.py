"""Opt-in: real input, exclusively on a disposable interactive Windows desktop. Not in CI."""
import argparse
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import queue
import subprocess
import tempfile
import threading
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dedicated-desktop', action='store_true', required=True,
                        help='the unlocked test desktop is dedicated to this run; do not use the keyboard or mouse')
    parser.add_argument('cli', type=Path)
    parser.add_argument('target', type=Path, help='KeyMouse.SmokeTarget.exe')
    opt = parser.parse_args()
    cli, target = opt.cli.resolve(strict=True), opt.target.resolve(strict=True)
    if os.name != 'nt' or target.name.lower() != 'keymouse.smoketarget.exe':
        parser.error('requires the Windows SmokeTarget fixture')
    folder = Path(tempfile.mkdtemp(prefix='km-replay-evidence-'))
    print(f'Evidence: {folder}', flush=True)
    env = {**os.environ, 'KEYMOUSE_OUTPUT_ENCODING': 'utf-8'}
    def command(*args, check=True):
        return subprocess.run([cli, *map(str, args)], env=env, capture_output=True, text=True,
                              encoding='utf-8-sig', check=check, timeout=25)

    app = subprocess.Popen([target, '--replay-evidence'])
    recorder = None
    try:
        selector = ['--pid', str(app.pid)]
        deadline = time.monotonic() + 10
        while command('window', 'focus', *selector, check=False).returncode:
            if app.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError('fixture did not acquire focus')
            time.sleep(.1)
        def image(name, text):
            out = folder / f'{name}.bmp'
            result = command('probe', *selector, '--engine', 'builtin', '--region', '0,0,220,48',
                             '--reads', '1', '--json', '--keep-image', out)
            if ' '.join(line['text'].strip() for line in json.loads(result.stdout)['lines']) != text:
                raise RuntimeError(f'{name}: OCR did not find the expected fixture text')
            return out.read_bytes()
        blank = image('blank', 'EMPTY')
        recorded = folder / 'recorded.json'
        recorder = subprocess.Popen([cli, 'record', '--out', recorded, '--duration', '5000', '--no-shots'],
                                    env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding='utf-8-sig')
        # Wait for the hook-ready notification, not a fixed startup sleep. Drain until EOF.
        lines = queue.Queue()
        def drain():
            for line in recorder.stdout:
                lines.put(line)
            lines.put(None)
        threading.Thread(target=drain, daemon=True).start()
        deadline = time.monotonic() + 12
        while True:
            line = lines.get(timeout=max(.01, deadline - time.monotonic()))
            if line is None: raise RuntimeError('recorder exited before installing its hooks')
            if '● 录制中' in line: break
        word = 'HELLO'
        command('key', 'type', word, '--interval', '30', '--focus-policy', 'none', *selector)
        if recorder.wait(timeout=15): raise RuntimeError('recording failed')
        flow = json.loads(recorded.read_text(encoding='utf-8-sig'))
        steps = flow['steps']
        # Never replay unrelated activity or captured mouse coordinates. Only our own known text
        # is allowed. Reject rather than silently discard unexpected recorded events.
        if (not steps or any(s.get('type') != 'type' or set(s) - {'type','text','atMs','intervalMs','target','delayMs'} for s in steps)
                or ''.join(s['text'] for s in steps) != word):
            raise RuntimeError('unexpected recorded actions: aborting before replay')
        before = image('recorded', word)
        if before == blank: raise RuntimeError('reference image is blank')
        command('key', 'combo', 'ctrl+a', '--focus-policy', 'none', *selector)
        command('key', 'press', 'backspace', '--focus-policy', 'none', *selector)
        if image('cleared', 'EMPTY') != blank: raise RuntimeError('fixture did not reset')
        flow['steps'] = [{'type':'sleep', 'ms':500}, *steps]
        edited = folder / 'edited.json'
        edited.write_text(json.dumps(flow, ensure_ascii=False), encoding='utf-8')
        user32 = ctypes.WinDLL('user32', use_last_error=True)
        user32.GetForegroundWindow.restype = wintypes.HWND
        user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
        pid = wintypes.DWORD()
        user32.GetWindowThreadProcessId(user32.GetForegroundWindow(), ctypes.byref(pid))
        if pid.value != app.pid: raise RuntimeError('fixture lost foreground; aborting before replay')
        # Only play the allowlisted immutable text on the dedicated foreground test desktop.
        command('run', edited, '--report', folder / 'replay-report.json')
        if image('replayed', word) != before: raise RuntimeError('recorded and replayed pixels differ')
        print('PASS real record → insert sleep → replay: text and fixture pixels match')
    finally:
        for p in (recorder, app):
            if p is not None and p.poll() is None:
                p.terminate()
                p.wait(timeout=5)


if __name__ == '__main__':
    main()
