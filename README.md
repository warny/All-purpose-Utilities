# GitHub Pages deployment branch

This branch contains the generated documentation website for **omy.Utils**.

It is maintained automatically by the GitHub Actions workflow:

`.github/workflows/docs.yml`

Published content:

- `latest/`: documentation generated from `master`
- `vX.Y.Z/`: documentation for released versions
- `index.html`: version index for the documentation website
- `.nojekyll`: disables Jekyll processing for DocFX assets

This branch is not a development branch. It should not be merged into `master` or deleted while GitHub Pages uses this deployment mechanism.
