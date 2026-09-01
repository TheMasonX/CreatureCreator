---
name: ste-technical-writing
description: "Write, rewrite, or lint CreatureCreator technical documentation in concise Simplified Technical English. Use for README sections, task records, ADRs, validation notes, and Unity workflow instructions."
argument-hint: "Choose rewrite, generate, or lint, then provide the documentation target"
---

# STE Technical Writing

## Modes

- **Rewrite**: preserve the facts and replace unclear or indirect wording.
- **Generate**: create concise documentation for a creature system, Unity
  workflow, API, test, or known limitation.
- **Lint**: list the exact wording problem, rule, and corrected wording.

## Rules

- Use active voice and concrete terms.
- Use short sentences, 20 words for procedures and 25 words for descriptions.
- Use one action per procedure step.
- Use one noun for each object and one verb for each action.
- Remove marketing language, filler, hedging, and unnecessary synonyms.
- Use terminal punctuation and commas. Avoid semicolons and em dashes.
- State Unity version, prerequisites, validation evidence, and limitations.
- Keep code identifiers and file paths exact.

## Document scope

Apply the strictest wording to durable project documentation, architecture
decisions, and user-facing workflow instructions. Keep working notes concise,
but preserve technical evidence, uncertainty, and ownership. Do not rewrite
working notes only to remove a permitted punctuation style.

## Completion criteria

The result is self-contained, factual, actionable, and consistent with the
project README and task tracker.