# Security policy

## Report a vulnerability privately

Use **[Report a vulnerability](https://github.com/Julien38300/topsolid-automation-mcp/security/advisories/new)** (GitHub private reporting). Do not open a public issue, and do not post a working exploit in Discussions.

Please include the version (`/status` or the tray menu), how the server was started (tray, `--http-standalone`, stdio) and the steps to reproduce. You will get a first answer within a week; the fix ships in a release, and you are credited in its notes unless you prefer not to be.

## Supported versions

Only the latest release (`releases/latest`) receives fixes. The tray's *Check for updates* installs it after checking its SHA-256.

## What counts

The server drives TopSolid on the user's machine. These are vulnerabilities:

- reaching the HTTP endpoint without the `X-API-Key`, or from another origin than the ones allowed;
- the API key, a user name, a personal path or document content leaking into logs, bug reports or crash reports;
- running a write tool or a write recipe while the server is in read-only mode (`--read-only`);
- the update mechanism installing a file whose SHA-256 does not match the release.

**By design, and not a vulnerability:** `topsolid_execute_script` and `topsolid_modify_script` compile and run C# with the rights of the user, as the README explains. Their deny-list is a guardrail, not a sandbox. Do not auto-approve these tools in your AI client.
