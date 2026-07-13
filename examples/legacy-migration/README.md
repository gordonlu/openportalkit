# Legacy Migration Dry-Run Fixture

Build the solution once, then run this repeatable read-only analysis from the repository root:

```bash
rm -f /tmp/opk-legacy-example-report.json
./tools/opk import legacy \
  --input examples/legacy-migration/content.csv \
  --assets examples/legacy-migration/assets.txt \
  --output /tmp/opk-legacy-example-report.json \
  --source legacy-mvc5 \
  --import-batch example-20260713 \
  --as-of 2026-07-12 \
  --schema-version legacy-content.v1
```

The command exits `0`, reports two valid rows, and writes a traceable JSON report. The output path must not already
exist; refusing to overwrite evidence is intentional. Change an asset path or slug in the CSV to exercise the
blocking report and exit code `1`.
