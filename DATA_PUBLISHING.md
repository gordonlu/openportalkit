# Data Publishing

Structured data publishing is a core differentiator of OpenPortalKit.

## Core Concepts

- `DataSet`
- `DataSchemaVersion`
- `DataRecord`
- `DataImportBatch`
- `DataSnapshot`
- `DataView`
- `DataQualityReport`

## Traceability

Data records must preserve:

- dataset identity
- record key
- payload
- as-of date
- schema version
- source batch
- checksum
- created and updated timestamps

## Import Rules

CSV import should support validation, dry runs, import batch tracking, error reports, idempotency where possible, and checksum-based change detection.

Failed imports must not corrupt published data.
