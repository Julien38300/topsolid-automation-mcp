#!/usr/bin/env python3
"""Build a SQLite FTS5 full-text search index of TopSolid help MD files.

Source: data/help-md/{EN,FR}/**/*.md (2974 EN + 2835 FR pages)
Output: data/help.db (SQLite with FTS5 virtual table)

Schema:
    CREATE VIRTUAL TABLE help USING fts5(
        title,          -- extracted from first heading or filename
        lang,           -- 'EN' or 'FR'
        domain,         -- Cad, Cae, Cam, Erp, Kernel, Pdm, WorkManager
        path,           -- relative file path under help-md/
        content,        -- full markdown text
        tokenize='unicode61 remove_diacritics 1'
    );

Usage:
    python scripts/build-help-index.py

The resulting help.db (~10-20 MB) is committed to the repo so users don't
need to re-index.
"""
from __future__ import annotations

import json
import re
import sqlite3
import sys
import time
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_DIR = SCRIPT_DIR.parent
HELP_ROOT = PROJECT_DIR / "data" / "help-md"
DB_PATH = PROJECT_DIR / "data" / "help.db"
META_PATH = PROJECT_DIR / "data" / "help-index-meta.json"


def extract_title(content: str, filename: str) -> str:
    """Get the first H1/H2 or fall back to the filename (without extension)."""
    m = re.search(r"^#\s+(.+)$", content, re.MULTILINE)
    if m:
        return m.group(1).strip()
    m = re.search(r"^##\s+(.+)$", content, re.MULTILINE)
    if m:
        return m.group(1).strip()
    return Path(filename).stem


def extract_domain(relative_path: Path) -> str:
    """Top-level folder under help-md/{LANG}/ → domain."""
    parts = relative_path.parts
    if len(parts) >= 2:
        return parts[1]  # LANG/DOMAIN/...
    return "unknown"


def iter_md_files(help_root: Path):
    """Yield (lang, relative_path_under_help_md, absolute_path)."""
    for lang in ("EN", "FR"):
        lang_dir = help_root / lang
        if not lang_dir.exists():
            continue
        for md in lang_dir.rglob("*.md"):
            rel = md.relative_to(help_root)
            yield lang, rel, md


def main() -> None:
    if not HELP_ROOT.exists():
        print(f"[ERR] {HELP_ROOT} not found", file=sys.stderr)
        sys.exit(1)

    if DB_PATH.exists():
        DB_PATH.unlink()
        print(f"[INFO] Removed existing {DB_PATH}")

    conn = sqlite3.connect(str(DB_PATH))
    conn.execute("""
        CREATE VIRTUAL TABLE help USING fts5(
            title,
            lang UNINDEXED,
            domain UNINDEXED,
            path UNINDEXED,
            content,
            tokenize='unicode61 remove_diacritics 1'
        );
    """)

    t0 = time.time()
    inserted = 0
    by_lang = {"EN": 0, "FR": 0}
    by_domain: dict[str, int] = {}

    for lang, rel_path, abs_path in iter_md_files(HELP_ROOT):
        try:
            content = abs_path.read_text(encoding="utf-8", errors="replace")
        except OSError as e:
            print(f"[WARN] {rel_path}: {e}", file=sys.stderr)
            continue

        title = extract_title(content, abs_path.name)
        domain = extract_domain(rel_path)

        conn.execute(
            "INSERT INTO help (title, lang, domain, path, content) VALUES (?, ?, ?, ?, ?)",
            (title, lang, domain, str(rel_path).replace("\\", "/"), content),
        )
        inserted += 1
        by_lang[lang] = by_lang.get(lang, 0) + 1
        by_domain[domain] = by_domain.get(domain, 0) + 1

    conn.commit()

    # Optimize FTS5
    conn.execute("INSERT INTO help(help) VALUES('optimize');")
    conn.commit()
    conn.close()

    elapsed = time.time() - t0
    size_mb = DB_PATH.stat().st_size / (1024 * 1024)

    meta = {
        "inserted": inserted,
        "by_lang": by_lang,
        "by_domain": by_domain,
        "db_size_mb": round(size_mb, 2),
        "build_elapsed_s": round(elapsed, 1),
        "built_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    META_PATH.write_text(json.dumps(meta, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"[DONE] Indexed {inserted} pages in {elapsed:.1f}s")
    print(f"  EN: {by_lang['EN']}  FR: {by_lang['FR']}")
    print(f"  Domains: {by_domain}")
    print(f"  DB: {DB_PATH} ({size_mb:.1f} MB)")


if __name__ == "__main__":
    main()
