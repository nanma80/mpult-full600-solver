# Publishing and preserving this work

1. Review the README and PORTABILITY_CHECK.json. The repository preserves the working solver and selected data; the full research archive remains separate.
2. Choose a license for the newly written solver files and add LICENSE. Keep vendor/mpult/LICENSE and upstream attribution unchanged. LICENSE-PENDING.md records the current undecided state.
3. Create an empty remote repository under your account. Avoid auto-generating a README or license if you want to push the existing local history directly.
4. Once ready, connect this repository and push. For example, if you want the public default branch named main:

```text
git branch -M main
git remote add origin YOUR_REMOTE_URL
git push -u origin main
```

The prepared local branch is codex/portable-solver; renaming is optional. No remote is configured and nothing has been uploaded by the release-preparation work.

5. Test the public clone instructions from a new folder. Users supply their own MPUlt1.55 installation. Keep generated runs in the ignored runs/ directory.
6. Share the repository link, a short explanation of the deterministic orbit-by-orbit approach, and screenshots of the real solve. There is no need to attach the multi-million-move output: readers regenerate it from the small included example or their own scramble.

## Backup

Pushing the repository to remote storage preserves code, algorithms, catalogs, documentation and Git history off this computer. A local commit alone does not protect against failure of the computer.

The Git repository is the intended deliverable and off-computer copy; separate source ZIPs and Git bundles are optional and are not needed for this workflow. Large generated solutions are disposable, while small initial states and independent-validation evidence are useful to retain.
