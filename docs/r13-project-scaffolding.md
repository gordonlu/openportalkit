# R13 Project Scaffolding and Source Distribution

## Product Model

OpenPortalKit is distributed as a source workspace for organizations to adapt, not as one fixed portal binary.
Generated projects retain the modular framework, tests, migrations, reference packs, examples, and deployment tools
so teams can change the public experience without forking undocumented build machinery.

## Batch 1: `opk new`

Generate a workspace from a clean OpenPortalKit source release:

```bash
./tools/opk new \
  --name "Atlas Public Portal" \
  --profile data \
  --output ../atlas-public-portal
```

Supported profiles are `corporate`, `data`, `research`, `activity`, and `finance`. The profile selects the initial
Web route and project display name. Finance records `Finance` in `selectedIndustryPacks`; it does not add finance
types to core or silently install anything in a database.

The output is a complete source workspace. `openportalkit.project.json` records schema version, display name,
profile, UTC generation time, template version, copied-file count, aggregate SHA-256, and selected packs.
`apps/web/src/lib/project-profile.json` is the site-level customization point consumed by the Web app.

Project profiles are versioned manifests under `templates/project-profiles/`. Each manifest has the strict
`opk.project-template-profile.v1` schema, a semantic version, an existing Web site id, and an explicit industry-pack
selection. `openportalkit.project.json` uses schema `opk.project.v2` and records the selected profile's id, version,
and SHA-256 independently from the complete source-template provenance.

### Safety Contract

- The destination must not exist and cannot contain, or be contained by, the source repository.
- Generation writes to a sibling staging directory and atomically renames it only after all files are complete.
- `.env`, build outputs, IDE state, dependency folders, test artifacts, local data, and agent state are excluded.
- `.env.example` is retained; symbolic links are rejected to prevent source-root escape.
- Executable file modes are preserved on Unix.
- The source tree is copied from explicit root file and directory allowlists.

The command does not rename framework assemblies or namespaces. Customer naming belongs to the generated project
profile; module names remain stable for upgrades, documentation, and boundary enforcement.

## Batch 2: `opk module add`

Add an industry-neutral module to an OpenPortalKit source workspace:

```bash
./tools/opk module add \
  --name Announcements \
  --area publishing-support \
  --description "Coordinates reusable announcement delivery contracts." \
  --owns-state false \
  --public-outputs JSON,Markdown
```

The command creates `src/OpenPortalKit.Modules.Announcements` and its executable contract-test project under
`tests/`, then registers both in the correct solution folders. The initial module references only `Kernel`.
Cross-module dependencies, Host registration, persistence, routes, and public writes are intentionally explicit
engineering decisions rather than generated behavior.

Module creation is transactional: destinations must not exist, files are staged, the solution is backed up, and
all changes are removed if solution registration or the post-generation boundary check fails. The repository must
already pass boundary checks. Names and generated descriptor content are checked by the same industry-neutrality
rules used in CI.

Supported public-output declarations are `HTML`, `Markdown`, `JSON`, `Sitemap`, `RSS`, `Search`, and `AgentSEO`.
Declaring an output records the contract only. Implementations must still add read authorization, audit behavior
for output-changing actions, tests, dashboard impact, and the relevant discoverability representation.

## Batch 3: Upgrade Inspection

Compare a generated workspace with a candidate OpenPortalKit checkout without changing customer code:

```bash
./tools/opk upgrade inspect \
  --root ../atlas-public-portal \
  --source . \
  --format text
```

The report validates the v2 project manifest, compares source template version and aggregate checksum, compares the
versioned profile checksum, and runs boundary checks against the customer workspace. Source or profile differences
are warnings intended for release review. Missing provenance, malformed checksums, or broken boundaries are failures.
The command does not copy, merge, delete, or rewrite files.

## Batch 4: Offline Source Archives

Release maintainers can package the controlled source template into a portable `.opkt` archive:

```bash
./tools/opk template pack --source . --output ./artifacts/openportalkit-0.4.0-r13.opkt
```

An installed CLI can then create a workspace without a nearby repository checkout:

```bash
opk new --name "Atlas Public Portal" --profile data \
  --source ./openportalkit-0.4.0-r13.opkt --output ../atlas-public-portal
```

The `opk.source-template-archive.v1` manifest records template version, UTC creation time, file count, and aggregate
source checksum. Packing uses the same allowlist and secret/build-output exclusions as direct generation. Extraction
is temporary and verifies the complete inventory before generation; it rejects duplicate or unsafe paths, symbolic
links, unsupported versions, file-count drift, oversized entries, archive expansion beyond 2 GiB, and checksum drift.

## Verification

CLI tests cover non-overwrite behavior, invalid profiles, nested paths, symlink rejection, atomic cleanup, secret and
build-output exclusion, project traceability, Web profile customization, Finance Pack selection, module solution
registration, deterministic descriptor contracts, duplicate protection, and boundary-failure rollback. A repository
fixture creates a complete current-source workspace and reruns the product boundary checker. Release validation also
builds a generated solution with one MSBuild worker, including a generated module and its contract test.

R13 will not implement automatic merge upgrades, arbitrary code generation, or a low-code module designer.

The generated workspace is the start of a customer project, not a finished production deployment. Supported
source-level branding locations, the current fixture-backed Web limitation, Windows publishing commands, database
promotion, and production acceptance are documented in `deployment.md`. R15 will close the live Web runtime and
repeatable Windows delivery gaps identified there.
