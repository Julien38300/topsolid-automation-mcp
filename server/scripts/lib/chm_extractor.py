"""CHM extraction — 7-Zip preferred, hh.exe fallback.

Extracts a CHM file into a directory. Preserves the original file.
Produces a meta.json sidecar with version, hash, and extractor info.
"""
from __future__ import annotations

import hashlib
import json
import re
import shutil
import subprocess
import sys
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path

from .paths import HH_EXE, SEVEN_ZIP


@dataclass
class ExtractionResult:
    """Metadata about a CHM extraction."""
    chm_path: str
    chm_sha256: str
    extracted_to: str
    html_file_count: int
    assembly_version: str  # e.g. "7.21.164.0" parsed from a page footer
    topsolid_version: str  # parsed from install path (e.g. "7.21")
    extractor: str  # "7z" or "hh.exe"
    extracted_at: str  # ISO 8601


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def parse_install_version(chm_path: Path) -> str:
    """Parse TopSolid version from the install path.

    `.../TopSolid 7.21/Help/en/...` → "7.21"
    """
    for part in chm_path.parts:
        m = re.match(r"TopSolid\s+(\d+\.\d+(?:\.\d+)*)", part)
        if m:
            return m.group(1)
    return "unknown"


def extract_assembly_version(raw_dir: Path) -> str:
    """Scan one HTML page for the assembly version footer.

    Looks for patterns like: `Version: 7.21.164.0`
    """
    html_dir = raw_dir / "html"
    if not html_dir.exists():
        html_dir = raw_dir  # some CHMs don't nest under html/

    version_re = re.compile(r"Version[:\s]*?(\d+\.\d+\.\d+\.\d+)")
    for htm in html_dir.glob("*.htm"):
        try:
            text = htm.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        m = version_re.search(text)
        if m:
            return m.group(1)
    return "unknown"


def _copy_chm_to_temp(chm_path: Path) -> Path:
    """Copy CHM to a quote-free path so 7z doesn't choke.

    The TopSolid CHM name contains an apostrophe which breaks 7z's CLI parser.
    We copy to a simple name in a temp dir.
    """
    import tempfile
    tmp = Path(tempfile.mkdtemp(prefix="chm-src-"))
    target = tmp / "automation.chm"
    shutil.copy2(chm_path, target)
    return target


def _extract_with_7z(chm_copy: Path, out_dir: Path) -> None:
    """Run 7z x on a CHM file. chm_copy should have a quote-free path."""
    if not SEVEN_ZIP.exists():
        raise FileNotFoundError(f"7-Zip not found at {SEVEN_ZIP}")
    out_dir.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(
        [str(SEVEN_ZIP), "x", str(chm_copy), f"-o{out_dir}", "-y"],
        capture_output=True, text=True,
    )
    if result.returncode != 0:
        raise RuntimeError(
            f"7z extraction failed (rc={result.returncode}):\n"
            f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
        )


def _extract_with_hh(chm_path: Path, out_dir: Path) -> None:
    """Fallback: Windows hh.exe -decompile."""
    if not HH_EXE.exists():
        raise FileNotFoundError(f"hh.exe not found at {HH_EXE}")
    out_dir.mkdir(parents=True, exist_ok=True)
    # hh.exe -decompile <target> <chm>
    result = subprocess.run(
        [str(HH_EXE), "-decompile", str(out_dir), str(chm_path)],
        capture_output=True, text=True, timeout=60,
    )
    if result.returncode != 0:
        raise RuntimeError(
            f"hh.exe extraction failed (rc={result.returncode}):\n"
            f"stdout:\n{result.stdout}\nstderr:\n{result.stderr}"
        )


def count_html_files(out_dir: Path) -> int:
    """Count extracted HTML pages (html/*.htm or *.htm)."""
    html_dir = out_dir / "html"
    if html_dir.exists():
        return sum(1 for _ in html_dir.glob("*.htm*"))
    return sum(1 for _ in out_dir.rglob("*.htm*"))


def extract(chm_path: Path, out_dir: Path, *, prefer_7z: bool = True) -> ExtractionResult:
    """Extract a CHM into out_dir. Returns metadata.

    Raises RuntimeError if both 7z and hh.exe fail.
    """
    chm_path = chm_path.resolve()
    if not chm_path.exists():
        raise FileNotFoundError(f"CHM not found: {chm_path}")

    out_dir = out_dir.resolve()
    # Clean previous extraction (idempotency)
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    # Compute hash BEFORE copying to guard against "did you re-extract?"
    chm_hash = sha256_file(chm_path)

    # Copy CHM to quote-free path for 7z
    chm_copy = _copy_chm_to_temp(chm_path)
    try:
        extractor_used = None
        if prefer_7z:
            try:
                _extract_with_7z(chm_copy, out_dir)
                extractor_used = "7z"
            except (FileNotFoundError, RuntimeError) as e:
                print(f"[chm_extractor] 7z failed ({e}); trying hh.exe", file=sys.stderr)
        if extractor_used is None:
            _extract_with_hh(chm_path, out_dir)  # hh.exe handles the apostrophe fine
            extractor_used = "hh.exe"
    finally:
        # Clean temp copy
        try:
            shutil.rmtree(chm_copy.parent)
        except OSError:
            pass

    html_count = count_html_files(out_dir)
    if html_count == 0:
        raise RuntimeError(
            f"Extraction produced 0 HTML files in {out_dir}. "
            f"CHM may be corrupted or unsupported."
        )

    result = ExtractionResult(
        chm_path=str(chm_path),
        chm_sha256=chm_hash,
        extracted_to=str(out_dir),
        html_file_count=html_count,
        assembly_version=extract_assembly_version(out_dir),
        topsolid_version=parse_install_version(chm_path),
        extractor=extractor_used,
        extracted_at=datetime.now(timezone.utc).isoformat(),
    )
    return result


def write_meta(meta_path: Path, result: ExtractionResult) -> None:
    """Write meta.json sidecar next to the extraction."""
    meta_path.parent.mkdir(parents=True, exist_ok=True)
    meta_path.write_text(
        json.dumps(asdict(result), indent=2, ensure_ascii=False),
        encoding="utf-8",
    )
