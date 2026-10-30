# Contributing

Thank you for helping. Issues and pull requests are welcome.

## How changes reach this repository

This repository is published from Proxus's internal driver repository, where the drivers are developed next to the
commercial ones and verified against a larger lab. A pull request here is reviewed here; once accepted, the change is
applied upstream with you credited as its author, and it appears in this repository with the next export. The pull
request is then closed with a link to that commit.

## What makes a change easy to accept

- One protocol behaviour per pull request, with a test in the matching `tests/*.SelfTests` project.
- No reflection, no runtime code generation, no blocking calls on network paths: the drivers must stay Native AOT
  compatible (the build treats trimming and AOT warnings as errors).
- A note in the protocol guide when behaviour or a limitation changes.
- For a device-specific fix, the device, firmware and what you observed (a capture helps most).

## Sign-off

Contributions are accepted under the Apache License 2.0. Sign each commit off (`git commit -s`) to certify the
[Developer Certificate of Origin](https://developercertificate.org/).
