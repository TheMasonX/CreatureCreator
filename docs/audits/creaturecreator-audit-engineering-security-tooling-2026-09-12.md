# CreatureCreator — Engineering, Security & Tooling Audit

**Report ID:** `CC-AUDIT-ENG-20260912-C83D71FA`
**Suite:** `CC-AUDIT-SUITE-20260912-9F42C6D1`
**Branch base audited:** `8bbbd3a1b7f940f6d2c866be09e89cef954d5e4b`
**Primary sources:** repository tooling/scripts, `.gitignore`, `.github/skills/*`, README/ADR/task infrastructure, current editor/runtime architecture

## Executive assessment

No high-confidence security vulnerability was established in the current application path from the available source inspection. The more relevant engineering risks are credential hygiene in developer tooling, script assumptions, unchecked external-process failures, and the danger of treating repository automation as part of the product without the same validation discipline.

The project already has one positive security-control example: local `.vscode/mcp.json` is ignored because it may contain an access credential. That is exactly the right direction.

## Findings

### ET-01 — Local MCP credential handling is correctly excluded but deserves a repository-wide secret scan policy

**Severity:** P2  
**Confidence:** 96%  
**Owner:** tooling/process

The repository explicitly ignores `.vscode/mcp.json` because it may contain a credential. This should be accompanied by a documented rule against committing tokens, cookies, local MCP endpoints carrying auth, or exported connection files.

A lightweight pre-commit/local scan is useful even if CI remains intentionally disabled.

### ET-02 — PowerShell deployment scripts have operational side effects larger than their documentation surface

**Severity:** P2  
**Confidence:** 93%  
**Owner:** Scripts/tooling

The repository contains deployment/bootstrap/import scripts that create directories, publish applications, and invoke external processes. These are appropriate for developer tooling but need explicit dry-run and exit-code semantics.

Any script that calls `dotnet`, installs/publishes artifacts, or mutates a local service should:

- stop on command failure;
- report the failing command;
- avoid partial success being reported as completion;
- support a safe preview/dry-run where practical.

### ET-03 — Task-import tooling must remain strictly one-way and resumable

**Severity:** P2  
**Confidence:** 95%  
**Owner:** MemorySmith migration tooling

The repository includes import/normalization scripts created for migration to MemorySmith. Their purpose is historical migration, not ongoing source-of-truth synchronization.

This must remain explicit. Bidirectional synchronization between `TSK-*` JSON and frozen CC markdown would recreate the dual-authority problem the migration was intended to remove.

### ET-04 — Tooling should distinguish local machine state from repository state

**Severity:** P2  
**Confidence:** 97%  
**Owner:** scripts/docs

EditorPrefs, SessionState, local MCP configuration, generated wiki/service state, and repository `Data/Tasks` are fundamentally different persistence domains.

Documentation should make this distinction obvious. A contributor should know which files are authoritative project artifacts and which are local machine state.

### ET-05 — Scripted deployment should verify postconditions, not only process exit codes

**Severity:** P2  
**Confidence:** 92%  
**Owner:** deployment scripts

A successful `dotnet publish` or service launch does not prove the expected artifact is available. Postconditions should verify:

- output path exists;
- expected files exist;
- configured port/service responds where appropriate;
- no credential/config file was accidentally copied into tracked output.

### ET-06 — GitHub repository automation should not be treated as validation if the active project policy disables CI

**Severity:** P2  
**Confidence:** 99%  
**Owner:** process

The repository explicitly disabled the former CI path during the MemorySmith migration. Therefore a green/absent GitHub status cannot be used as implicit product validation.

Task and Unity validation must remain the canonical evidence path until the project policy changes.

### ET-07 — Documentation of engineering skills is effectively executable policy and should be versioned carefully

**Severity:** P3  
**Confidence:** 94%  
**Owner:** `.github/skills/*`

The repository carries audit, task-tracking, Unity-validation, orchestration, and engineering-guardrail skill documents. These influence how future agents modify the repository.

A stale skill can therefore cause real code changes, not merely documentation confusion.

Skills should have:

- clear ownership;
- current task-system vocabulary (`TSK-*`);
- explicit “do not use retired CC tickets as authority” guidance;
- validation requirements matching the current project state.

### ET-08 — Repository automation should fail closed around missing configuration

**Severity:** P2  
**Confidence:** 92%  
**Owner:** scripts

When deployment/config assets are missing, tools should fail loudly rather than infer a local path, default credential, or project environment. This mirrors the runtime palette principle: missing configuration should not silently change behavior.

### ET-09 — Generated/local artifacts need explicit classification

**Severity:** P3  
**Confidence:** 96%  
**Owner:** repository hygiene

The project has had editor-session backups and generated artifacts appear during previous work. Continue classifying artifacts as one of:

- source-of-truth;
- generated-but-committed;
- generated-and-ignored;
- local-only.

The classification should be reflected in `.gitignore` and README documentation rather than relying on developer memory.

## Positive observations

- `.gitignore` already protects a local MCP credential-bearing file.
- Runtime code avoids direct `UnityEditor` dependencies.
- Shared palette assets are intentionally runtime-safe.
- Task records have explicit source provenance and revisions.
- The project retains deterministic generation-oriented design instead of depending on hidden editor state.

## Conclusion

No new critical application-security defect is justified from the current evidence. The stronger recommendation is to treat developer tooling, task automation, and skills as a controlled part of the engineering system: fail closed, classify local state, verify postconditions, and never recreate a second source of truth.
