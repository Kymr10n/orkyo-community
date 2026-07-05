# Launch assets — manual TODO

The README screenshots gallery is wired up (`.github/screenshot-*.png`). A few launch polish items
still need a human hand — they can't be generated from code. This is the checklist.

## 1. Social preview image (GitHub)

A social preview is the card shown when the repo is linked on Slack / X / LinkedIn / etc.

- **Size:** 1280×640 px (2:1), PNG or JPG, < 1 MB.
- **Content:** the Orkyo wordmark + the one-line tagline ("Operational planning, made simple") over a
  lightly-dimmed product shot (the calendar or spaces view works well). Reuse `.github/orkyo-teaser.jpg`
  as the base if convenient.
- **Apply:** repo **Settings → General → Social preview → Edit → Upload an image**. Not stored in git.

## 2. Optional demo GIF

A short (< 8 s, < 10 MB) GIF of dragging a request onto the calendar / a space makes the README land
harder than static shots. Optional — the static gallery is sufficient for launch.

- Capture at ≥ 1280 px wide, trim to one clear interaction, export as an optimised GIF or a short MP4.
- Drop it in `.github/` (e.g. `screenshot-demo.gif`) and add it above the Screenshots table.

## 3. Live demo link

If a public read-only demo instance is stood up, add it to the README header row (next to Website /
Issues / Discussions) and to the Screenshots section intro. Until then, no demo link is shown (better
than a dead one).

## 4. Pin the repository

On the GitHub **profile/org page → Customize your pins**, pin `orkyo-community` so it surfaces first for
visitors. Repo-level setting, nothing to commit.

## 5. Refresh screenshots when the UI changes

The current shots are from the `manufacturing / large` demo seed (`./dev.sh seed --profile manufacturing
--scale large --mode reset`). Re-capture at the same window size after notable UI changes so the gallery
stays honest. Keep filenames stable (`screenshot-<feature>.png`) so the README needs no edit.
