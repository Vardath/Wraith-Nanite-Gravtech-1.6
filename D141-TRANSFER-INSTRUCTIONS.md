# D141 transfer branch

Upload these three files to the root of this branch:

- D141.part01
- D141.part02
- D141.part03

Each part is under GitHub's 25 MB web-upload limit.

When all three are present, the GitHub Action will:
1. concatenate them in order,
2. verify SHA-256 `41a79ddae91ae734e8256f64806bd0733f45ad05328e2cda753992821fde0b32`,
3. extract `Wraith-Nanite-Gravtech/`,
4. replace the authoritative runtime mod contents on `main`,
5. preserve repository-only `.github/`, `Source/`, and `artupgrades.md`,
6. validate XML parsing and PNG signatures before committing.
