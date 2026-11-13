#!/usr/bin/env python3
"""Generates the default tactics (content/tactics/*.json) in the Kick Off 2 zone format.

The *format* follows the original game (see docs/REFERENCE_ANALYSIS.md): for each of the 10 outfield
players, one position per ball zone (12 zones: 3 columns x 4 rows) plus 8 special situations
(kick-offs, goal kicks, corners), in tactic units of 912 x 1392 (12 units per yard, origin at the
team's own left corner flag, y towards the opponent goal).

The *positions* are our own: they are computed from a simple shape (home position shifted with the
ball, a little higher when the team has the ball). No value is copied from original tactic files.
Usage: python3 tools/generate_default_tactics.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
WIDTH, LENGTH = 912, 1392


def shape(name, follow, attack_shift, slots):
    return {"name": name, "follow": follow, "attack_shift": attack_shift, "slots": slots}


# Outfield slots only (the goalkeeper is handled separately, as in Kick Off 2).
# home: normalised [x, y] (-1..1, x left->right when attacking upwards, y own goal -> opponent goal);
# kick: kick-off position when the team kicks off; min_y/max_y: depth limits.
T442 = shape("4-4-2", (0.4, 0.5), 0.12, [
    {"home": (-0.68, -0.58), "kick": (-0.6, -0.55), "min_y": -0.92, "max_y": 0.25},
    {"home": (-0.24, -0.66), "kick": (-0.22, -0.62), "min_y": -0.92, "max_y": 0.0},
    {"home": (0.24, -0.66), "kick": (0.22, -0.62), "min_y": -0.92, "max_y": 0.0},
    {"home": (0.68, -0.58), "kick": (0.6, -0.55), "min_y": -0.92, "max_y": 0.25},
    {"home": (-0.66, -0.12), "kick": (-0.6, -0.25), "min_y": -0.75, "max_y": 0.6},
    {"home": (-0.22, -0.2), "kick": (-0.2, -0.3), "min_y": -0.8, "max_y": 0.5},
    {"home": (0.22, -0.2), "kick": (0.2, -0.3), "min_y": -0.8, "max_y": 0.5},
    {"home": (0.66, -0.12), "kick": (0.6, -0.25), "min_y": -0.75, "max_y": 0.6},
    {"home": (-0.2, 0.28), "kick": (0.0, -0.01), "min_y": -0.4, "max_y": 0.88},
    {"home": (0.2, 0.28), "kick": (0.22, -0.04), "min_y": -0.4, "max_y": 0.88},
])
T433 = shape("4-3-3", (0.45, 0.5), 0.15, [
    {"home": (-0.68, -0.55), "kick": (-0.6, -0.55), "min_y": -0.92, "max_y": 0.3},
    {"home": (-0.24, -0.64), "kick": (-0.22, -0.62), "min_y": -0.92, "max_y": 0.0},
    {"home": (0.24, -0.64), "kick": (0.22, -0.62), "min_y": -0.92, "max_y": 0.0},
    {"home": (0.68, -0.55), "kick": (0.6, -0.55), "min_y": -0.92, "max_y": 0.3},
    {"home": (-0.4, -0.15), "kick": (-0.35, -0.28), "min_y": -0.75, "max_y": 0.55},
    {"home": (0.0, -0.25), "kick": (0.0, -0.35), "min_y": -0.8, "max_y": 0.45},
    {"home": (0.4, -0.15), "kick": (0.35, -0.28), "min_y": -0.75, "max_y": 0.55},
    {"home": (-0.6, 0.3), "kick": (-0.5, -0.06), "min_y": -0.3, "max_y": 0.88},
    {"home": (0.0, 0.36), "kick": (0.0, -0.01), "min_y": -0.3, "max_y": 0.9},
    {"home": (0.6, 0.3), "kick": (0.22, -0.04), "min_y": -0.3, "max_y": 0.88},
])

CIRCLE_X, CIRCLE_Y = 0.30, 0.20  # normalised half-axes of the centre circle (+ margin)


def clamp(v, lo, hi):
    return max(lo, min(hi, v))


def to_units(x, y):
    return [round((x + 1) / 2 * WIDTH), round((y + 1) / 2 * LENGTH)]


def place(slot, t, ball, possession):
    x = slot["home"][0] + ball[0] * t["follow"][0]
    y = slot["home"][1] + ball[1] * t["follow"][1] + (t["attack_shift"] if possession else 0)
    return to_units(clamp(x, -0.94, 0.94), clamp(y, slot["min_y"], slot["max_y"]))


def kick_off_opponent(slot):
    x, y = slot["kick"]
    if (x / CIRCLE_X) ** 2 + (y / CIRCLE_Y) ** 2 < 1:  # stay outside the centre circle
        y = -CIRCLE_Y
    return to_units(x, min(y, -0.02))


def build(t):
    players = []
    zone_centres = [((c + 0.5) / 3 * 2 - 1, (r + 0.5) / 4 * 2 - 1) for c in range(3) for r in range(4)]
    for slot in t["slots"]:
        players.append({
            "defend": [place(slot, t, b, False) for b in zone_centres],
            "attack": [place(slot, t, b, True) for b in zone_centres],
            "kickOffOwn": to_units(*slot["kick"]),
            "kickOffOpponent": kick_off_opponent(slot),
            "goalKickOwn": place(slot, t, (0.0, -0.9), True),
            "goalKickOpponent": place(slot, t, (0.0, 0.9), False),
            "cornerOwnLeft": place(slot, t, (-1.0, -1.0), False),
            "cornerOwnRight": place(slot, t, (1.0, -1.0), False),
            "cornerOpponentLeft": place(slot, t, (-1.0, 1.0), True),
            "cornerOpponentRight": place(slot, t, (1.0, 1.0), True),
        })
    return {
        "id": t["name"],
        "nameKey": "tactic." + t["name"].replace("-", ""),
        "units": {"width": WIDTH, "length": LENGTH},
        "players": players,
    }


def dump(data):
    """Compact JSON: one line per position list, readable in diffs."""
    lines = ["{", f'  "id": "{data["id"]}",', f'  "nameKey": "{data["nameKey"]}",',
             '  "units": { "width": %d, "length": %d },' % (WIDTH, LENGTH), '  "players": [']
    for i, p in enumerate(data["players"]):
        lines.append("    {")
        keys = list(p.keys())
        for k in keys:
            sep = "," if k != keys[-1] else ""
            lines.append(f'      "{k}": {json.dumps(p[k])}{sep}')
        lines.append("    }" + ("," if i < len(data["players"]) - 1 else ""))
    lines += ["  ]", "}"]
    return "\n".join(lines) + "\n"


def main():
    out = ROOT / "content" / "tactics"
    for t in (T442, T433):
        (out / f'{t["name"]}.json').write_text(dump(build(t)))


if __name__ == "__main__":
    main()
