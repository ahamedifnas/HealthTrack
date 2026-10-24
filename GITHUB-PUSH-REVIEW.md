# HealthTrack GitHub push review

Reviewed on 2026-10-24. No commit, push, remote creation, history rewrite, application execution, or database operation was performed.

## Repository

- Repository root: `C:\Users\UL\source\repos\HealthTrack` (one directory above the web project).
- Branch: `main`, 13 commits. Local HEAD and cached `origin/main` were both `6c2725c` at inspection. The live remote was not fetched.
- Existing origin: `https://github.com/ahamedifnas/HealthTrack.git`.
- Many application, payment, migration, documentation and test changes already existed. This preparation does not implement or validate their runtime behavior.

## Credential findings and protection

- `HealthTrack/appsettings.json` contains populated PayHere Merchant ID/Secret. Its SQL connections use integrated authentication, with no SQL password detected; they still contain local configuration details.
- Removed that file from Git's index ONLY and added ignore rules. The existing file and values remain on disk unchanged. Its staged deletion is intentional; do not use `git add -f` to restore it.
- `HealthTrack/Tests/CaptureScreenshots.cjs` contains a literal matching the current development admin password in User Secrets. This untracked local helper is now ignored and remains unchanged on disk.
- SMTP and admin User Secrets exist outside the repository. No match for the current SMTP password was found in scanned repository files or Git blobs. The project `UserSecretsId` identifies the secret store and is not itself a credential.
- Existing demo seeding, demo documentation and demo smoke tests intentionally use a shared demonstration password. Other smoke tests generate passwords or use invalid test fixtures. These are public test data, never suitable for real accounts. No authentication, demo, PayHere or payment behavior was changed.
- The ignore file now covers private appsettings, environment files, secret exports, private keys, database files, backups and publish output, alongside existing Visual Studio/build/log exclusions. The existing logging-only `appsettings.Development.json` and placeholder examples remain eligible for Git.
- `HealthTrack/appsettings.example.json` contains placeholders only. It is not loaded by the default runtime configuration. New clones must copy it to `appsettings.json` and supply their own valid configuration; existing local settings were not overwritten.

## History scope and limitations

- Scanned 4,055 tracked/untracked/ignored paths and 609 unique blobs referenced by all local refs/reflogs, using credential patterns and exact matches against current sensitive configuration values. Five locked, ignored `.vs` index files could not be read. Binary text scanning cannot establish that arbitrary binary formats contain no secrets.
- The current PayHere credentials and development admin password were found in a local `refs/codex/turn-diffs/checkpoints/...` reference. Their blobs are NOT reachable from `main` or its cached identical `origin/main`.
- No real credential was detected in the 13 commits reachable from `main`; historical SQL strings contained local server/database details, and reviewed password literals were test fixtures. This is a heuristic audit, not a guarantee against every credential format or credentials outside this local repository.
- Do not mirror-push or publish internal refs, `.git`, ignored output, or a full folder archive. An explicit `main:main` push does not publish the checkpoint reference. No refs were removed or rewritten.
- Treat the local checkpoint copies as sensitive. Rotate the PayHere secret and development admin password if the checkpoint, helper, repository directory or build artifacts have been shared. This review cannot establish external exposure; credential rotation was not performed.

## Release build

`dotnet build HealthTrack.slnx --configuration Release --nologo` succeeded: zero errors, four warning instances representing two advisories against `System.Security.Cryptography.Pkcs` 9.0.4 in the test project dependency graph:

- NU1902, moderate: https://github.com/advisories/GHSA-2fmv-rv2c-59x7
- NU1903, high: https://github.com/advisories/GHSA-846f-f764-76mx

Resolve and verify the affected dependency before a production release. Packages were not upgraded as part of this behavior-preserving preparation. No smoke tests were run because they can create accounts or modify the database.

## Commit eligibility

Safe preparation changes: `.gitignore`, this report, `HealthTrack/appsettings.example.json`, and the staged removal of `HealthTrack/appsettings.json` from version control.

No additional live credentials were detected in the remaining eligible source, views, static assets, project/solution files, migrations, SQL scripts, tests or documentation. Their existing functional changes still require normal code review. Migration source files are eligible; they were not executed. Intentional demo credentials remain public demo data.

Do not commit the private appsettings, screenshot helper, User Secrets, ignored build/publish output, logs, database files or backups. The local audit script/report are under ignored `artifacts/` and are not part of the commit.

## Manual commands

This is an existing GitHub repository, so do not run `git init` or add another origin. The following creates a preparation-only commit, leaving unrelated application changes unstaged. Review the staged file list before committing. The appsettings deletion has already been staged and retains the local file.

```powershell
Set-Location 'C:\Users\UL\source\repos\HealthTrack'
git add -- .gitignore GITHUB-PUSH-REVIEW.md HealthTrack/appsettings.example.json
git diff --cached --name-status
git diff --cached --check
# Expected: .gitignore, this report, the example, and deletion of appsettings.json.
# Confirm the private runtime file still exists locally:
Test-Path .\HealthTrack\appsettings.json
git commit -m "Protect local configuration and add safe settings template"
git fetch origin
git log --oneline main..origin/main
# If the preceding command lists commits, STOP and reconcile remote changes.
# Otherwise preview, then push only main:
git -c remote.origin.mirror=false -c push.followTags=false push --dry-run origin main:main
git -c remote.origin.mirror=false -c push.followTags=false push -u origin main:main
```

Do not run the commit/push commands until the staged file list is reviewed. A subsequent full feature commit should explicitly stage and review the existing application changes separately; avoid `git commit -a`, forced adds, force pushes and mirror pushes.
