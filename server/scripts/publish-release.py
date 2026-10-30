#!/usr/bin/env python3
"""
Publish a TopSolid MCP Server release to GitHub Releases via the REST API
(using git credential fill — no `gh` CLI dependency).

Usage:
    python server/scripts/publish-release.py --version 1.8.10 --title "v1.8.10 — Refonte UX Tray & SSE" --notes-file notes.md
"""
import argparse
import json
import os
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

REPO_OWNER = "Julien38300"
REPO_NAME = "topsolid-automation-mcp"
API_BASE = f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}"


def get_github_token() -> str:
    env_tok = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if env_tok:
        return env_tok.strip()
    proc = subprocess.run(
        ["git", "credential", "fill"],
        input="protocol=https\nhost=github.com\n\n",
        text=True,
        capture_output=True,
        check=True,
    )
    for line in proc.stdout.splitlines():
        if line.startswith("password="):
            return line.split("=", 1)[1].strip()
    raise RuntimeError("Could not obtain GitHub token from git credential fill")


def api_request(url: str, token: str, method: str = "GET", data: bytes | None = None, content_type: str = "application/json") -> dict:
    headers = {
        "User-Agent": "TopSolidMcpReleasePublisher/1.0",
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json",
    }
    if data is not None:
        headers["Content-Type"] = content_type
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=120) as resp:
        raw = resp.read().decode("utf-8")
        return json.loads(raw) if raw else {}


def main() -> int:
    parser = argparse.ArgumentParser(description="Publish GitHub Release for TopSolid MCP Server")
    parser.add_argument("--version", required=True, help="Version without v prefix, e.g. 1.8.10")
    parser.add_argument("--title", default=None, help="Release title (defaults to v<version>)")
    parser.add_argument("--notes", default=None, help="Release notes markdown string")
    parser.add_argument("--notes-file", default=None, help="UTF-8 markdown file with release notes")
    parser.add_argument("--target", default="main", help="Target branch or commit SHA (default: main)")
    args = parser.parse_args()

    version = args.version.lstrip("vV")
    tag = f"v{version}"
    title = args.title or tag
    if args.notes_file:
        notes = Path(args.notes_file).read_text(encoding="utf-8")
    elif args.notes:
        notes = args.notes
    else:
        notes = f"Release {tag}"

    server_dir = Path(__file__).resolve().parent.parent
    zip_path = server_dir / f"TopSolidMcpServer-v{version}.zip"
    sums_path = server_dir / "SHA256SUMS.txt"

    if not zip_path.exists():
        print(f"ERROR: Missing zip asset: {zip_path}", file=sys.stderr)
        return 1
    if not sums_path.exists():
        print(f"ERROR: Missing sums asset: {sums_path}", file=sys.stderr)
        return 1

    token = get_github_token()

    # Check if release for tag already exists (draft or published)
    existing = None
    rels = api_request(f"{API_BASE}/releases?per_page=20", token)
    for r in rels:
        if r.get("tag_name") == tag:
            existing = r
            break

    if existing:
        release_id = existing["id"]
        print(f"Updating existing release {tag} (id={release_id})...")
        rel = api_request(
            f"{API_BASE}/releases/{release_id}",
            token,
            method="PATCH",
            data=json.dumps({
                "tag_name": tag,
                "target_commitish": args.target,
                "name": title,
                "body": notes,
                "draft": True,
                "prerelease": False,
            }).encode("utf-8"),
        )
        # Delete old assets if any
        for asset in existing.get("assets", []):
            api_request(f"{API_BASE}/releases/assets/{asset['id']}", token, method="DELETE")
    else:
        print(f"Creating draft release {tag}...")
        rel = api_request(
            f"{API_BASE}/releases",
            token,
            method="POST",
            data=json.dumps({
                "tag_name": tag,
                "target_commitish": args.target,
                "name": title,
                "body": notes,
                "draft": True,
                "prerelease": False,
            }).encode("utf-8"),
        )
        release_id = rel["id"]

    upload_base = rel["upload_url"].split("{", 1)[0]

    for asset_path, ctype in [(zip_path, "application/zip"), (sums_path, "text/plain")]:
        name = asset_path.name
        print(f"Uploading {name} ({asset_path.stat().st_size / 1024:.1f} KB)...")
        url = f"{upload_base}?name={urllib.parse.quote(name)}"
        api_request(url, token, method="POST", data=asset_path.read_bytes(), content_type=ctype)

    print(f"Publishing release {tag} (draft=False)...")
    published = api_request(
        f"{API_BASE}/releases/{release_id}",
        token,
        method="PATCH",
        data=json.dumps({"draft": False}).encode("utf-8"),
    )

    latest = api_request(f"{API_BASE}/releases/latest", token)
    print(f"SUCCESS: Published {published.get('html_url')} (releases/latest = {latest.get('tag_name')})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
