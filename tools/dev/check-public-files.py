"""Check public Git candidates without printing matching text. Run at the repository root."""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path, PurePosixPath


TEXT_PATTERNS = [
    ('credential signature', re.compile(
        rb"""-----BEGIN ([A-Z0-9 ]+ )?PRIVATE KEY-----|github_pat_[A-Za-z0-9_]{30,}|gh[pousr]_[A-Za-z0-9]{30,}|AKIA[A-Z0-9]{16}|https?://[^ /:@]+:[^ /@]+@""",
        re.IGNORECASE,
    )),
    ('local user profile path', re.compile(
        rb"""(?:[A-Z]:[\\/]+Users[\\/]+[^\\/\s"'<>]+[\\/]+|/(?:home|Users)/[^/\s"'<>]+/)""",
        re.IGNORECASE,
    )),
]


def git(*args, ok=(0,)):
    result = subprocess.run(['git', *args], stdout=subprocess.PIPE, check=False)
    if result.returncode not in ok:
        raise SystemExit(f'git {args[0]} failed ({result.returncode})')
    return result.stdout


def paths(data):
    return set(data.decode('utf-8', 'surrogateescape').split('\0')) - {''}


def check(index):
    names = paths(git('ls-files', '-z', '--cached', *([] if index else ['--others', '--exclude-standard'])))
    root = Path.cwd().resolve()
    if not index:
        names = {n for n in names if Path(n).exists() or Path(n).is_symlink()}
    findings = set()
    for n in paths(git('ls-files', '-z', '-ci', '--exclude-standard')) & names:
        findings.add((n, 'tracked despite ignore rule'))
    for n in names:
        p = PurePosixPath(n.lower())
        if (p.suffix in {'.key', '.pem', '.pfx', '.p12', '.log', '.dmp'} or p.name in {'id_rsa', 'id_dsa', 'id_ecdsa', 'id_ed25519', '.env'}
                or (p.name.startswith('.env.') and p.name != '.env.example')
                or any(part.endswith('.shots') for part in p.parts)):
            findings.add((n, 'private material or generated output'))
    for n in names:
        path = Path(n)
        if not index and path.is_symlink():
            try:
                if not path.resolve().is_relative_to(root):
                    findings.add((n, 'symlink points outside checkout'))
                    continue
            except OSError:
                findings.add((n, 'unreadable symlink'))
                continue
        try:
            raw = git('show', ':' + n) if index else path.read_bytes()
        except (OSError, SystemExit):
            findings.add((n, 'unreadable candidate'))
            continue
        # Binary contents need format-specific review; do not decode them as text.
        if b'\0' not in raw:
            for kind, pattern in TEXT_PATTERNS:
                if pattern.search(raw):
                    findings.add((n, kind))
        if n.startswith('samples/') and n.endswith('.json'):
            try:
                doc = json.loads(raw)
            except (ValueError, UnicodeError):
                findings.add((n, 'unreadable JSON example')); continue
            if isinstance(doc, dict) and ('recordedAt' in doc or 'screen' in doc):
                findings.add((n, 'recording metadata in a public example'))
    for n, reason in sorted(findings):
        print(f'{reason}: {n}')
    print(f'{"Index" if index else "Worktree"}: {len(names)} files; {len(findings)} findings. Binary contents and history not scanned.')
    return bool(findings)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--index', action='store_true', help='inspect Git index blobs, excluding unstaged edits')
    args = parser.parse_args()
    if git('rev-parse', '--show-prefix').strip():
        parser.error('run from repository root')
    sys.exit(check(args.index))
