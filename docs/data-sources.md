# Data sources

Every online source HASI uses: what it is, its terms, its limits, and what happens if it disappears.

## GitHub Releases of HASI (updates, phase 0.11)

- What: the release feed of `https://github.com/AllxCucu/hasi`, read by Velopack (`GithubSource`) to find a newer
  version and download its package.
- When: once at every start (if "Verificare automată la pornire" is on) and when the user chooses
  "Caută actualizări acum". Nothing about the user is sent; GitHub sees the request like any web request.
- Terms: GitHub's public API and release downloads, under GitHub's Terms of Service; HASI's releases are our own
  content (GPL-3.0-or-later).
- Limits: unauthenticated API requests are limited to 60 per hour per IP address; one check per start stays far
  below. If the limit is hit, the check fails and HASI says nothing at startup.
- Fallback if it disappears or the project moves: the source is configuration, not code (`UpdateSource` in
  `settings.json`: `github:<other repository>`, `http:<URL>` of a static folder, or `folder:<path>`).
