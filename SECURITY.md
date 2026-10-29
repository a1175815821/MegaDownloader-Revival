# Security Policy

MegaDownloader Revival is a download manager: it handles remote links, writes files to disk and can expose a local web server. Security reports are taken seriously and handled privately.

## Supported versions

| Version | Supported |
| --- | --- |
| 2.6.x | ✅ |
| 2.5.x | ⚠️ fixes only for issues that also affect 2.6 |
| ≤ 2.4 | ❌ please upgrade |

## Reporting a vulnerability

**Do not open a public issue for a security problem.**

1. Open the repository's **Security** tab → **Report a vulnerability** (GitHub private vulnerability reporting). This creates a private advisory visible only to the maintainer.
2. If private reporting is not available to you, open a normal issue that says only *"security issue — please provide a private channel"*, without technical details, links or proof-of-concept code.

Please include, when you can:

- Affected version (About → Version) and Windows version
- The component involved: download engine, extraction, web interface, streaming server, ELC / `mega://` link parsing, steganography, updater
- Reproduction steps or a minimal proof of concept
- Impact: what an attacker gains (file write outside the target folder, credential disclosure, remote request forgery, denial of service, …)

## What to expect

- **Acknowledgement** within 7 days.
- An assessment and a remediation plan, or a request for more information, within 14 days.
- Credit in the release notes once a fix ships, unless you prefer to stay anonymous.

Please give us a reasonable window to release a fix before disclosing the issue publicly.

## Scope

In scope:

- Path traversal or arbitrary file write during download, extraction, rename or cleanup
- Authentication or CSRF flaws in the built-in web interface and streaming server
- Credential handling (MEGA account password, ELC passwords, proxy credentials) and their storage
- Remote code execution, unsafe deserialization, DLL or archive handling issues
- Denial of service reachable from a link, a web request or a crafted archive

Out of scope:

- MEGA service behaviour, quota limits or account restrictions
- Issues that require an attacker to already have local administrator rights
- Social engineering, or vulnerabilities in third-party DLLs that have no exploitable path in this application (still worth telling us)

## Safe harbour

We will not pursue legal action against researchers who act in good faith: testing against their own installation, avoiding privacy violations and data destruction, and reporting privately before any disclosure.
