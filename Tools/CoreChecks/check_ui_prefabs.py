#!/usr/bin/env python3
"""Catch collapsed screen roots in serialized Unity prefabs without an Editor."""

from pathlib import Path
import math
import re
import sys


def vector(block, name, axes):
    match = re.search(r"^  " + name + r": \{([^}]+)\}$", block, re.MULTILINE)
    if match is None:
        raise ValueError(f"missing {name}")
    values = dict(part.strip().split(": ") for part in match[1].split(","))
    result = tuple(float(values[axis]) for axis in axes)
    if not all(math.isfinite(value) for value in result):
        raise ValueError(f"non-finite {name}")
    return result


def check(path):
    blocks = re.split(r"(?m)^--- !u!", path.read_text(encoding="utf-8"))
    roots = [block for block in blocks if block.startswith("224 &") and
             re.search(r"^  m_Father: \{fileID: 0\}$", block, re.MULTILINE)]
    if len(roots) != 1:
        raise ValueError("expected one root RectTransform")
    root = roots[0]
    scale = vector(root, "m_LocalScale", "xyz")
    if any(abs(axis) < 0.000001 for axis in scale):
        raise ValueError(f"root Scale {scale} collapses the screen")
    size = vector(root, "m_SizeDelta", "xy")
    if any(axis <= 0 for axis in size):
        raise ValueError(f"root reference size {size} has no visible area")


def main():
    folder = Path(sys.argv[1]) if len(sys.argv) > 1 else (
        Path(__file__).resolve().parents[2] / "Assets/BBSB/Resources/BBSB/Presentation")
    paths = sorted(folder.glob("*Screen.prefab"))
    if not paths:
        print(f"FAIL UI prefabs: no screen prefabs in {folder}")
        return 1
    failed = 0
    for path in paths:
        try:
            check(path)
        except (OSError, ValueError, KeyError) as error:
            failed += 1
            print(f"FAIL {path.name}: {error}")
    print(f"UI prefab roots: {len(paths)} screens, {failed} errors (not a Unity rendering test).")
    return int(failed > 0)


if __name__ == "__main__":
    sys.exit(main())
