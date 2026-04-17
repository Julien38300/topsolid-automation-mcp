"""Centralized path resolution for the TopSolid API sync pipeline.

All paths used by the pipeline are resolved here. Makes it easy to move the
pipeline without hunting for hardcoded paths.
"""
from __future__ import annotations

import os
import re
from pathlib import Path


# --- Project layout ---
SCRIPTS_DIR = Path(__file__).parent.parent.resolve()
PROJECT_DIR = SCRIPTS_DIR.parent
DATA_DIR = PROJECT_DIR / "data"

# --- API sync artifacts ---
API_DIR = DATA_DIR / "api"
API_CURRENT_LINK = API_DIR / "current"

# --- Inputs consumed by the pipeline ---
GRAPH_JSON = DATA_DIR / "graph.json"
API_INDEX_JSON = DATA_DIR / "api-index.json"
RECIPE_LIST_TXT = DATA_DIR / "recipe-list.txt"

# --- CHM source (outside project) ---
TOPSOLID_INSTALL_ROOT = Path("C:/Program Files/TOPSOLID")
CHM_RELATIVE_PATH = "Help/en/TopSolid'Design Automation.chm"

# --- External tools ---
SEVEN_ZIP = Path("C:/Program Files/7-Zip/7z.exe")
HH_EXE = Path("C:/Windows/hh.exe")

# --- Regex helpers ---
VERSION_RE = re.compile(r"^\d+\.\d+(?:\.\d+)*(?:\.\d+)?$")


# ============================================================================
# Snapshot directory per TopSolid version
# ============================================================================

def snapshot_dir(version: str) -> Path:
    """Directory for a given TopSolid version's snapshot.

    Example:
        >>> snapshot_dir("7.21.164.0")
        PosixPath('.../data/api/7.21.164.0')
    """
    if not VERSION_RE.match(version):
        raise ValueError(f"Invalid version format: {version!r}")
    return API_DIR / version


def snapshot_raw_dir(version: str) -> Path:
    """Raw extracted .htm files for a given version (gitignored, ~25 MB)."""
    return snapshot_dir(version) / "raw"


def snapshot_methods_json(version: str) -> Path:
    return snapshot_dir(version) / "methods.json"


def snapshot_types_json(version: str) -> Path:
    return snapshot_dir(version) / "types.json"


def snapshot_namespaces_json(version: str) -> Path:
    return snapshot_dir(version) / "namespaces.json"


def snapshot_meta_json(version: str) -> Path:
    return snapshot_dir(version) / "meta.json"


def snapshot_proposals_json(version: str) -> Path:
    return snapshot_dir(version) / "proposals.json"


# ============================================================================
# Top-level artifacts (diff + reports)
# ============================================================================

def diff_json(version: str) -> Path:
    return DATA_DIR / f"api-diff-{version}.json"


def proposals_md(version: str) -> Path:
    return DATA_DIR / f"recipe-proposals-{version}.md"


def changelog_md(version: str) -> Path:
    return DATA_DIR / f"changelog-{version}.md"


def test_stubs_json(version: str) -> Path:
    return snapshot_dir(version) / "proposals" / "tests" / "TestSuite.stubs.json"


# ============================================================================
# Auto-detection helpers
# ============================================================================

def auto_detect_chm() -> Path | None:
    """Find the newest TopSolid install's automation CHM.

    Scans C:/Program Files/TOPSOLID/TopSolid */Help/en/*.chm and returns the
    path inside the version folder with the highest version number.
    """
    if not TOPSOLID_INSTALL_ROOT.exists():
        return None

    candidates: list[tuple[tuple[int, ...], Path]] = []
    for install in TOPSOLID_INSTALL_ROOT.glob("TopSolid *"):
        m = re.match(r"TopSolid\s+(\d+(?:\.\d+)+)", install.name)
        if not m:
            continue
        version_tuple = tuple(int(x) for x in m.group(1).split("."))
        chm = install / CHM_RELATIVE_PATH
        if chm.exists():
            candidates.append((version_tuple, chm))

    if not candidates:
        return None
    candidates.sort(key=lambda x: x[0], reverse=True)
    return candidates[0][1]


def find_previous_snapshot(current_version: str) -> Path | None:
    """Return the directory of the most recent snapshot STRICTLY before current_version.

    Used by the differ to compare snapshot-vs-snapshot.
    """
    if not API_DIR.exists():
        return None

    current_tuple = tuple(int(x) for x in current_version.split("."))
    candidates: list[tuple[tuple[int, ...], Path]] = []
    for d in API_DIR.iterdir():
        if not d.is_dir() or d.name == "current":
            continue
        if not VERSION_RE.match(d.name):
            continue
        v_tuple = tuple(int(x) for x in d.name.split("."))
        if v_tuple < current_tuple:
            candidates.append((v_tuple, d))

    if not candidates:
        return None
    candidates.sort(key=lambda x: x[0], reverse=True)
    return candidates[0][1]


def set_current_link(version: str) -> None:
    """Point the `current` symlink/junction to the given version snapshot.

    On Windows we use a junction (mklink /J) since symlinks require admin.
    """
    target = snapshot_dir(version)
    if not target.exists():
        raise FileNotFoundError(f"Snapshot does not exist: {target}")

    if API_CURRENT_LINK.exists() or API_CURRENT_LINK.is_symlink():
        try:
            if API_CURRENT_LINK.is_symlink() or API_CURRENT_LINK.is_junction():
                API_CURRENT_LINK.unlink()
            elif API_CURRENT_LINK.is_dir():
                # Can't replace a real directory automatically — refuse.
                raise RuntimeError(
                    f"{API_CURRENT_LINK} is a real directory, not a symlink. "
                    f"Remove it manually before continuing."
                )
        except (OSError, AttributeError):
            # is_junction is Python 3.12+; fall back to rmdir attempt
            try:
                API_CURRENT_LINK.unlink()
            except Exception:
                os.rmdir(str(API_CURRENT_LINK))

    if os.name == "nt":
        # Junction works without admin rights
        import subprocess
        subprocess.run(
            ["cmd.exe", "/c", "mklink", "/J", str(API_CURRENT_LINK), str(target)],
            check=True, capture_output=True,
        )
    else:
        API_CURRENT_LINK.symlink_to(target.name, target_is_directory=True)
