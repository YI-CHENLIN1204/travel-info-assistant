# Travel Info Assistant development rules

These instructions apply to the entire repository.

## Workflow

- Before proposing work, inspect `README.md`, this file, `git status`, recent Git history, and the relevant code and tests.
- Work on exactly one smallest coherent, testable MVP slice at a time.
- Before editing, propose the slice, files to change, and verification commands, then wait for explicit user approval.
- After implementation, run the focused tests and checks relevant to the slice.
- Create one local commit for each completed slice. Never run `git push`; the user pushes commits.
- Prefer repository evidence over asking for information already available in code, documentation, tests, or Git history.
- Preserve unrelated user changes and do not perform unrelated cleanup.

## Secrets and external services

- Never read, display, modify, stage, or commit `.env` files or API keys.
- Do not place secrets in source code, tests, documentation, logs, or commit messages.
- The ODPT account is under manual review and no API key is available. Do not perform real ODPT API validation until the user explicitly updates this status. Local unit tests and mocked fixtures remain allowed.

## Taiwan transit data policy

- Apply the same quota-conscious design to all Taiwan cities and all Taiwan public transport modes, including existing integrations.
- Use official daily timetables and shared caches as the default data path.
- Fetch realtime arrivals only on explicit user demand; do not continuously poll realtime TDX endpoints in the background.
- When realtime data is unavailable or the internal TDX budget threshold is reached, fall back to timetable or retained cache data instead of hiding the feature.
- Never present timetable-derived countdowns as realtime ETA. Label scheduled and realtime information distinctly.
- Implement migrations to this policy as separate smallest coherent, testable slices; do not combine all cities or modes into one change.

## Handoff

- Before proposing each next slice, report the current branch, latest commit, worktree status, and whether local commits are ahead of the remote.
- After completing a slice, report the commit hash, verification results, and any remaining risk. Do not push.
