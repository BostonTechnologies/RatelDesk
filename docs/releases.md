# Releases

RatelDesk has one repository-owned release line. The root `Directory.Build.props` is the source of truth: maintainers update `VersionPrefix` when preparing the next release. All application projects inherit that value. `VersionSuffix` creates prereleases without changing the release line; for example, `-p:VersionPrefix=0.2.0 -p:VersionSuffix=beta.1` evaluates to `0.2.0-beta.1`.

## Build identity

Assemblies contain the semantic version, source repository, source revision, and a UTC `BuildTimestamp` metadata value. CI supplies one timestamp and the full commit SHA to every Web and API build. At runtime, the top-right Release Info panel and `GET /api/v1/system/version` report the independently deployed Web and API identities:

```json
{
  "version": "0.1.0-rc.4",
  "commitHash": "abcdef1234567890",
  "buildTimestamp": "2026-09-10T10:34:00Z",
  "assemblyName": "Helpdesk.API",
  "environment": "Production"
}
```

Deployment configuration never owns the product version. A deployment-specific label, if one is needed later, must be modeled separately and cannot override this assembly metadata.

## Publishing a release

After the release-engineering pull request is merged and `main` is green, verify the evaluated value rather than reading the props file directly:

```bash
dotnet msbuild src/Helpdesk.API/Helpdesk.API.csproj -nologo -getProperty:Version
git status --short
git tag -a v0.1.0-rc.4 -m "RatelDesk 0.1.0-rc.4"
git push origin v0.1.0-rc.4
```

The tag must be a SemVer tag (`vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH-prerelease`) on `main`, and its value must exactly equal MSBuild's evaluated repository version. The tag workflow reruns release validation, builds Web and API images for amd64 and arm64, attaches provenance/SBOM data, then creates the GitHub Release only after both image pushes complete.

Stable releases publish `ghcr.io/bostontechnologies/rateldesk-web` and `ghcr.io/bostontechnologies/rateldesk-api` with the exact version and `latest`. Prereleases publish only their exact tag and become GitHub prereleases. Consumers should use an exact tag or an immutable digest, never rely on `latest` for a production deployment.

To use released images locally, set required credentials and run:

```bash
RATELDESK_VERSION=0.1.0-rc.4 docker compose -f docker/docker-compose.release.yml up -d
```

The GitHub Release records the source commit, UTC build timestamp, and immutable Web/API digests. Before a private deployment is updated, operators should clean-pull both public images and pin the deployment to those recorded digests.
