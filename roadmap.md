\# OpenPortalKit Roadmap



OpenPortalKit is an open-source framework for enterprise portals, content-heavy websites, and structured data publishing platforms.



It is designed for organizations that need more than a simple CMS:



\* public website rendering

\* content management

\* structured data publishing

\* publishing workflow

\* audit logs

\* search

\* dashboard and analytics

\* static rendering

\* machine-readable outputs

\* agent-friendly SEO

\* industry adaptation packs



OpenPortalKit should remain general-purpose. Finance support is implemented as an optional industry pack, not as part of the core domain model.



\---



\# 1. Product Positioning



\## 1.1 What OpenPortalKit Is



OpenPortalKit is a reusable publishing framework for enterprise websites and public data portals.



It is suitable for:



\* corporate websites

\* enterprise news portals

\* announcement portals

\* investor relations portals

\* research portals

\* structured data publishing sites

\* activity and event portals

\* public information portals

\* finance-oriented websites through an optional Finance Pack

\* education, healthcare, government-style, and industry information portals



\## 1.2 What OpenPortalKit Is Not



OpenPortalKit is not:



\* a blog system

\* a pure CMS

\* a low-code builder

\* a BI platform

\* a CRM

\* a trading system

\* a financial business system

\* an OA/BPM system

\* a general-purpose data warehouse

\* an AI SEO trick tool



OpenPortalKit is a \*\*publishing framework\*\*, not a business platform.



It helps organizations publish trustworthy content and structured data to humans, search engines, LLMs, RAG systems, and agents.



\---



\# 2. Core Product Formula



```txt

OpenPortalKit

= Enterprise Portal

\+ Content Publishing

\+ Structured Data Publishing

\+ Workflow / Audit

\+ Search

\+ Dashboard

\+ Agent-readable Outputs

\+ Industry Packs

```



The project should focus on trustworthy public publishing.



The core value is not only “manage articles”, but also:



\* publish content safely

\* publish structured data safely

\* track data source and freshness

\* expose machine-readable snapshots

\* support approval and audit

\* show operational dashboards

\* adapt to industries without polluting the core model



\---



\# 3. Architecture Principles



\## 3.1 Modular Monolith First



OpenPortalKit should use a modular monolith architecture.



Do not split into microservices too early.



Recommended runtime shape:



```txt

OpenPortalKit.AdminHost

OpenPortalKit.ApiHost

OpenPortalKit.JobHost

OpenPortalKit.Web

```



Recommended logical modules:



```txt

OpenPortalKit.Kernel

OpenPortalKit.Modules.Identity

OpenPortalKit.Modules.Content

OpenPortalKit.Modules.Assets

OpenPortalKit.Modules.Workflow

OpenPortalKit.Modules.Data

OpenPortalKit.Modules.Search

OpenPortalKit.Modules.Seo

OpenPortalKit.Modules.AgentAccess

OpenPortalKit.Modules.Dashboard

OpenPortalKit.Modules.Audit

OpenPortalKit.Modules.Jobs

OpenPortalKit.IndustryPacks.Finance

```



The system may run as a small number of processes, but code boundaries must stay clear.



\## 3.2 Core Must Stay Industry-Neutral



Allowed in core:



```txt

ContentItem

ContentType

Page

Block

Taxonomy

Asset

DataSet

DataRecord

DataSchemaVersion

DataImportBatch

DataSnapshot

DataView

Workflow

AuditLog

SearchDocument

SeoMetadata

AgentSnapshot

DashboardMetric

```



Not allowed in core:



```txt

Fund

IPO

Stock

Security

Broker

Finance

MarketCommentary

RiskDisclosure

FundNav

IpoProject

```



Finance-specific concepts belong only in:



```txt

industry-packs/Finance

```



\## 3.3 Publishing Events Are First-Class



Many features depend on publishing events:



```txt

ContentPublished

ContentUpdated

ContentArchived

DatasetImported

DatasetPublished

AssetUploaded

WorkflowApproved

WorkflowRejected

```



These events should drive:



\* search indexing

\* sitemap generation

\* RSS generation

\* Markdown snapshot generation

\* JSON snapshot generation

\* llms.txt generation

\* dashboard metrics

\* cache invalidation

\* Next.js revalidation

\* audit logging



Use an outbox pattern for reliable event processing.



\## 3.4 Dashboard Is a Core Capability



Dashboard is not an optional afterthought.



OpenPortalKit should help site owners understand:



\* whether content is fresh

\* whether data is up to date

\* whether publishing workflows are blocked

\* whether agents can read the site

\* whether search is healthy

\* whether background jobs are failing

\* whether the site is being used



\## 3.5 Agent-Friendly Publishing Is a Core Differentiator



Every important public resource should be available in multiple forms:



```txt

HTML     for humans and search engines

Markdown for LLMs, RAG, and agents

JSON     for integrations and structured consumption

RSS      for feed readers and automated watchers

Sitemap  for search engines and crawlers

OpenAPI  for public APIs

```



AgentSEO should be treated as structured, trustworthy publishing, not as ranking manipulation.



\---



\# 4. Recommended Technology Stack



\## 4.1 Backend



Recommended:



```txt

.NET 10

ASP.NET Core

EF Core

PostgreSQL

SQL Server optional

Redis

OpenTelemetry

Hangfire or Quartz.NET

.NET Aspire for local orchestration

Docker Compose for portable setup

```



\## 4.2 Frontend



Recommended:



```txt

Next.js App Router

TypeScript

Tailwind CSS

Server Components

SSR / SSG / ISR

semantic HTML

accessible forms and links

```



\## 4.3 Admin



Recommended initial approach:



```txt

ASP.NET Core Razor Pages / MVC

HTMX optional

Blazor SSR optional

```



Avoid a large React SPA admin in the early stages.



Admin should prioritize:



\* reliability

\* permissions

\* audit

\* forms

\* review workflow

\* data import

\* operational clarity



\## 4.4 Search



Recommended provider model:



```txt

PostgreSQL search provider       # baseline, no extra service

Meilisearch provider             # recommended enhanced search

OpenSearch provider              # enterprise option

Vector search provider           # optional future Agent/RAG enhancement

```



\## 4.5 Observability



Recommended:



```txt

OpenTelemetry

Prometheus

Grafana

Loki optional

Tempo optional

```



Built-in dashboard should show business and publishing health.



Detailed engineering observability can be handled by Grafana-compatible dashboards.



\---



\# 5. Repository Structure



```txt

openportalkit

├── apps

│   ├── web

│   └── admin-web

│

├── src

│   ├── OpenPortalKit.Kernel

│   ├── OpenPortalKit.AdminHost

│   ├── OpenPortalKit.ApiHost

│   ├── OpenPortalKit.JobHost

│   ├── OpenPortalKit.AppHost

│   │

│   ├── OpenPortalKit.Modules.Identity

│   ├── OpenPortalKit.Modules.Content

│   ├── OpenPortalKit.Modules.Assets

│   ├── OpenPortalKit.Modules.Workflow

│   ├── OpenPortalKit.Modules.Data

│   ├── OpenPortalKit.Modules.Search

│   ├── OpenPortalKit.Modules.Seo

│   ├── OpenPortalKit.Modules.AgentAccess

│   ├── OpenPortalKit.Modules.Dashboard

│   ├── OpenPortalKit.Modules.Audit

│   └── OpenPortalKit.Modules.Jobs

│

├── industry-packs

│   └── Finance

│       ├── pack.json

│       ├── content-types

│       ├── datasets

│       ├── rules

│       ├── templates

│       ├── dashboard-cards

│       ├── seed-data

│       └── docs

│

├── templates

│   ├── corporate

│   ├── data-portal

│   ├── research-portal

│   └── finance

│

├── docs

├── examples

├── docker

├── tests

└── tools

```



\---



\# 6. Roadmap Overview



OpenPortalKit should be built through the following release tracks:



```txt

R0  Architecture Guardrails

R1  Portal Kernel

R2  Content Publishing Core

R3  Public Rendering + SEO Baseline

R4  Workflow + Audit

R5  Structured Data Publishing

R6  Search + Discovery

R7  Dashboard + Analytics

R8  Agent Access / AgentSEO

R9  Block Template System

R10 Industry Pack System + Finance, Technology, Education, and Entertainment Packs

R11 Production Hardening + Migration Toolkit

R12 Developer Experience + 1.0 Stabilization

```



\---



\# R0 — Architecture Guardrails



\## Goal



Create the rules and documents that keep the project coherent before agents or contributors start implementing large features.



\## Deliverables



```txt

ROADMAP.md

ARCHITECTURE.md

AGENTS.md

CONTRIBUTING.md

SECURITY.md

MODULES.md

INDUSTRY\_PACKS.md

AGENT\_SEO.md

DASHBOARD.md

DATA\_PUBLISHING.md

```



\## Required Rules



Document:



\* product positioning

\* non-goals

\* module boundaries

\* forbidden core terms

\* industry pack boundaries

\* definition of done

\* test requirements

\* security requirements

\* migration requirements

\* AgentSEO requirements

\* dashboard requirements



\## Acceptance Criteria



\* Agents know what not to build.

\* Finance-specific concepts are explicitly forbidden in core.

\* Module dependencies are documented.

\* The project has a clear definition of done.

\* The roadmap can guide long-running coding agents safely.



\---



\# R1 — Portal Kernel



\## Goal



Build the technical foundation of the project.



\## Deliverables



\### Backend Foundation



\* .NET solution

\* ASP.NET Core hosts

\* modular monolith structure

\* module registration

\* configuration system

\* health checks

\* structured logging

\* OpenTelemetry integration

\* database migrations

\* seed data system



\### Infrastructure



\* PostgreSQL support

\* SQLite support for tests/local lightweight development

\* Redis integration

\* Docker Compose

\* .NET Aspire AppHost

\* CI pipeline

\* test project structure



\### Kernel Entities



```txt

Site

User

Role

Permission

Setting

AuditLog

OutboxMessage

JobRecord

Asset

```



\### Event Infrastructure



```txt

DomainEvent

IntegrationEvent

OutboxMessage

EventHandler

IdempotencyKey

RetryPolicy

```



\## Acceptance Criteria



\* A developer can run the local stack with one command.

\* Database migrations are reproducible.

\* Health checks work.

\* Outbox messages can be written and processed.

\* Audit logs can be recorded.

\* The system runs without Finance Pack.



\---



\# R2 — Content Publishing Core



\## Goal



Implement generic content publishing.



\## Core Entities



```txt

ContentType

ContentItem

ContentVersion

Taxonomy

Category

Tag

Asset

```



\## ContentItem Fields



```txt

id

site\_id

content\_type\_id

title

slug

summary

body

cover\_asset\_id

status

category\_id

tags

author\_id

source

published\_at

scheduled\_at

expires\_at

created\_by

updated\_by

created\_at

updated\_at

```



\## Deliverables



\* content type CRUD

\* content item CRUD

\* category and tag management

\* slug generation

\* draft and published states

\* content versioning

\* asset upload

\* basic public content API

\* basic admin content UI

\* seed content



\## Acceptance Criteria



\* Admin can create and edit content.

\* Admin can publish and unpublish content.

\* Public users can view published content.

\* Drafts are not visible publicly.

\* Content versions are stored.

\* Core content model remains industry-neutral.



\---



\# R3 — Public Rendering + SEO Baseline



\## Goal



Build the public website rendering layer.



\## Public Pages



```txt

Home page

Content list page

Content detail page

Announcement list/detail

Activity list/detail

Report list/detail

Static page

Search page placeholder

Data page placeholder

```



\## Rendering Requirements



\* SSR or static rendering for public content

\* ISR for cacheable pages

\* semantic HTML

\* accessible links and forms

\* stable URLs

\* canonical URLs

\* pagination

\* category filters

\* tag filters

\* breadcrumb navigation



\## SEO Baseline



\* title

\* description

\* canonical

\* Open Graph metadata

\* JSON-LD

\* sitemap.xml

\* robots.txt

\* RSS / Atom feeds

\* redirect rules

\* legacy URL mapping foundation



\## Revalidation Pipeline



Publishing events should trigger:



```txt

content publish event

\-> update search document

\-> regenerate sitemap/RSS

\-> regenerate snapshots

\-> invalidate route cache

\-> optionally warm important pages

\-> record revalidation result

```



\## Acceptance Criteria



\* Public pages are crawlable.

\* Core content is visible without client-side JavaScript.

\* Sitemap and RSS are generated.

\* Public pages include canonical metadata.

\* Published content can trigger frontend cache invalidation.

\* Legacy redirects can be configured.



\---



\# R4 — Workflow + Audit



\## Goal



Add enterprise-grade publishing workflow without building a full BPM system.



\## Workflow States



```txt

Draft

Review

Approved

Published

Rejected

Archived

```



\## Workflow Actions



```txt

create draft

submit for review

approve

reject

request changes

publish

schedule publish

unpublish

archive

restore

```



\## Core Entities



```txt

PublishRequest

ApprovalRecord

AuditLog

ContentVersion

```



\## Rules



\* published content must have title

\* published content must have slug

\* published content must have summary

\* scheduled content must have scheduled\_at

\* rejected content must have review comment

\* archived content must not appear in public lists

\* public-output-changing actions must be audited



\## Acceptance Criteria



\* Content can go through review before publishing.

\* Every workflow action is logged.

\* Rejections include comments.

\* Published content cannot be modified silently.

\* Version history is preserved.

\* Audit logs are queryable by actor and target.



\---



\# R5 — Structured Data Publishing



\## Goal



Make OpenPortalKit more than a CMS by supporting structured data publishing.



\## Core Entities



```txt

DataSet

DataSchemaVersion

DataRecord

DataImportBatch

DataSnapshot

DataView

DataQualityReport

```



\## DataSet



Represents a structured data collection.



Example uses:



```txt

product catalog

course catalog

hospital departments

government statistics

research indicators

fund NAV through Finance Pack

IPO projects through Finance Pack

```



\## DataRecord



Stores a record inside a dataset.



Required traceability fields:



```txt

dataset\_id

record\_key

payload\_json

as\_of\_date

schema\_version\_id

source\_batch\_id

checksum

created\_at

updated\_at

```



\## DataView



Represents a public view of a dataset.



```txt

dataset\_id

code

name

filter\_json

sort\_json

columns\_json

public\_slug

cache\_policy

```



\## Data Import



Support:



\* CSV import

\* validation

\* dry run

\* import batch tracking

\* error reports

\* idempotent import where possible

\* checksum-based change detection



\## Public Endpoints



```txt

/api/public/datasets

/api/public/datasets/{code}

/api/public/datasets/{code}/schema

/api/public/datasets/{code}/records

/api/public/datasets/{code}/records/{recordKey}

/api/public/datasets/{code}/export.csv

```



\## Acceptance Criteria



\* Admin can define a dataset.

\* Admin can import CSV data.

\* Invalid imports produce error reports.

\* Public users can view dataset pages.

\* Agents can retrieve dataset schema and records.

\* Dataset records include source, version, checksum, and as-of date.

\* Failed imports do not corrupt published data.



\---



\# R6 — Search + Discovery



\## Goal



Provide full-site search for public users and admin users.



\## Search Targets



```txt

content items

pages

announcements

activities

reports

datasets

data records

assets metadata

```



\## Search Providers



```txt

PostgreSQLSearchProvider

MeilisearchProvider

OpenSearchProvider

VectorSearchProvider later

```



\## SearchDocument



```txt

id

target\_type

target\_id

title

summary

body\_text

url

content\_type

tags

category

published\_at

updated\_at

visibility

language

metadata\_json

```



\## Deliverables



\* search abstraction

\* PostgreSQL baseline search

\* Meilisearch adapter

\* indexing events

\* reindex job

\* public search page

\* admin search page

\* filters

\* search health check



\## Acceptance Criteria



\* Published content is searchable.

\* Archived content does not appear in public search.

\* Admin search can find drafts when permission allows.

\* Search indexing is event-driven.

\* Reindexing is repeatable and idempotent.



\---



\# R7 — Dashboard + Analytics



\## Goal



Build a first-class dashboard system for site operations, content publishing, structured data publishing, AgentSEO readiness, and system health.



Dashboard should help organizations understand whether their portal is healthy, useful, fresh, searchable, and agent-readable.



\## Principle



Dashboard aggregates and visualizes signals from other modules.



Dashboard must not own core business state.



Finance-specific dashboard cards belong only to Finance Pack.



\## Dashboard Areas



\### 1. Site Operations Dashboard



Metrics:



```txt

page views

unique visitors

top pages

top sections

traffic sources

search keywords

entry pages

exit pages

downloads

form submissions

activity registrations

404 pages

slow pages

```



\### 2. Content Dashboard



Metrics:



```txt

draft count

review queue count

rejected content count

published today

scheduled publishing count

archived content count

stale content count

content missing SEO metadata

content missing summary

content missing cover

content missing AgentSEO snapshots

content distribution by type

content distribution by author

content distribution by category

```



\### 3. Data Publishing Dashboard



Metrics:



```txt

dataset count

data record count

import batch count

import success count

import failure count

data quality failure count

stale dataset count

missing source count

missing as\_of\_date count

latest snapshot status

dataset API request count

dataset export count

top datasets

```



\### 4. AgentSEO Dashboard



Metrics:



```txt

average Agent Readiness Score

low-score pages

missing Markdown snapshots

missing JSON snapshots

sitemap coverage

llms.txt coverage

structured data coverage

public OpenAPI status

AI bot traffic

blocked training bot requests

agent-facing 404/500 errors

top pages accessed by agents

```



\### 5. System Health Dashboard



Metrics:



```txt

API latency

API error rate

background job success rate

failed job count

outbox backlog

search indexing lag

snapshot generation failures

revalidation failures

database health

Redis health

search provider health

storage health

```



\## Core Entities



```txt

AnalyticsEvent

DashboardMetricSnapshot

DashboardAlert

DashboardCard

```



\## Analytics Privacy Requirements



Default analytics should be privacy-conscious.



Default behavior:



```txt

no cross-site tracking

no third-party cookies

hashed session identifier

configurable IP anonymization

configurable retention period

bot traffic classification

admin-visible privacy settings

```



\## Observability Integration



Built-in dashboard shows high-level business and publishing health.



Engineering observability should integrate with:



```txt

OpenTelemetry

Prometheus

Grafana

Loki optional

Tempo optional

```



\## Acceptance Criteria



\* Admin can view site traffic summary.

\* Admin can view content publishing status.

\* Admin can view dataset freshness and import status.

\* Admin can view AgentSEO readiness.

\* Admin can view background job failures.

\* Admin can see actionable alerts.

\* Dashboard does not require Finance Pack.

\* Dashboard aggregation does not slow down public pages.

\* Privacy settings are configurable.

\* OpenTelemetry metrics are exported.



\---



\# R8 — Agent Access / AgentSEO



\## Goal



Make OpenPortalKit content discoverable, understandable, citable, and usable by search engines, LLMs, RAG systems, and browser agents.



\## AgentSEO Layers



```txt

crawlable SEO

structured SEO

LLM/RAG snapshots

agent-friendly UX

AI bot policy

public machine-readable APIs

Agent Readiness Score

```



\## Traditional SEO



Support:



```txt

title

description

canonical URL

Open Graph metadata

breadcrumbs

sitemap.xml

robots.txt

RSS / Atom feeds

JSON-LD structured data

```



\## Markdown Snapshots



Every public content item should optionally expose a clean Markdown representation.



Example:



```txt

/news/example-article.md

```



Markdown should include:



```txt

title

summary

published date

updated date

author

source

key facts

body

related links

data sources

usage policy

```



\## JSON Snapshots



Every public content item should optionally expose a machine-readable JSON representation.



Example:



```txt

/api/public/content/{slug}.json

```



JSON should include:



```txt

content identity

content type

title

summary

key facts

body text

source

published date

updated date

canonical URL

citations

related content

agent visibility policy

```



\## llms.txt



Generate:



```txt

/llms.txt

/llms-full.txt

```



Should include:



```txt

site description

main sections

important public URLs

sitemap URL

RSS URL

public API URL

OpenAPI URL

usage policy

attribution policy

```



\## Agent Manifest



Experimental endpoint:



```txt

/.well-known/agent.json

```



Should include:



```txt

site name

description

public resources

sitemap

RSS

llms.txt

OpenAPI spec

public search endpoint

dataset endpoints

usage policy

```



\## AI Bot Policy



Support configuration for:



```txt

allow\_search\_bots

allow\_training\_bots

allow\_user\_agents

crawl\_delay\_seconds

```



\## OpenAPI First



Expose public API description:



```txt

/api/openapi.json

```



OpenAPI should cover public read endpoints.



\## Read-only MCP Later



Optional future read-only MCP server:



```txt

list\_public\_sections

search\_public\_content

get\_public\_content

list\_datasets

get\_dataset\_schema

query\_dataset\_records

```



Do not add write-capable MCP tools until permissions, approval, audit, and injection risks are handled.



\## Agent Readiness Score



Score public resources based on:



```txt

title exists

description exists

canonical exists

structured data exists

summary exists

key facts exist

published date exists

updated date exists

source exists

Markdown snapshot exists

JSON snapshot exists

sitemap included

RSS included

semantic HTML

accessible forms and links

```



For datasets:



```txt

schema endpoint exists

JSON records endpoint exists

CSV export exists

source exists

as\_of\_date exists

version exists

checksum exists

```



\## Acceptance Criteria



\* Public content has sitemap entries.

\* Public content has Markdown snapshots.

\* Public content has JSON snapshots.

\* Public datasets expose schema and records.

\* robots.txt can distinguish search bots and training bots.

\* llms.txt is generated.

\* OpenAPI public spec is generated.

\* Agent Readiness Score is visible in admin.

\* Agent-facing outputs are consistent with visible page content.



\---



\# R9 — Block Template System



\## Goal



Allow organizations to assemble public pages from reusable blocks without turning the project into a complex low-code platform.



\## Rename



Do not call this a full Page Builder in early versions.



Use:



```txt

Block Template System

```



\## Supported Blocks



```txt

HeroBlock

RichTextBlock

ContentListBlock

AnnouncementListBlock

ActivityListBlock

ReportListBlock

DataTableBlock

ChartBlock

LinkListBlock

DownloadListBlock

FAQBlock

ContactBlock

EmbedBlock

```



\## Constraints



Allowed:



```txt

predefined blocks

schema-based block config

simple ordering

server-rendered blocks

template seed data

preview

```



Avoid:



```txt

arbitrary drag-and-drop canvas

unbounded nested layouts

custom CSS injection

complex low-code logic

runtime plugin marketplace

```



\## Templates



Initial templates:



```txt

corporate homepage

news portal

announcement center

activity portal

research portal

data portal

investor-relations style page

finance-style portal page through Finance Pack

```



\## Acceptance Criteria



\* Admin can create a page using predefined blocks.

\* Blocks render on the public site.

\* Blocks are serializable.

\* Blocks are versioned with the page.

\* Templates can seed pages.

\* Page output remains accessible and SEO-friendly.



\---



\# R10 — Industry Pack System + Reference Packs



\## Goal



Support vertical industry adaptation without polluting the core domain model.



\## Industry Pack Contents



An industry pack may provide:



```txt

content types

dataset schemas

page templates

validation rules

publishing rules

dashboard cards

seed data

importers

public page components

admin extensions

terminology overrides

disclaimer templates

sample data

```



\## Pack Structure



```txt

industry-packs/{PackName}

├── pack.json

├── content-types

├── datasets

├── templates

├── rules

├── dashboard-cards

├── importers

├── seed-data

├── components

└── docs

```



\## Early Pack Strategy



Do not build a complex runtime plugin system too early.



Initial packs should use:



```txt

manifest

JSON schemas

seed data

templates

rules

dashboard card definitions

```



Code-level extension interfaces can come later.



\## Finance Pack Goal



Finance Pack demonstrates how OpenPortalKit adapts to regulated, data-heavy, high-trust industries.



Finance Pack must not become a core dependency.



\## Reference Pack Portfolio



R10 ships four optional reference packs through the same manifest and registration contracts:



```txt

Finance Pack

Technology Pack

Education Pack

Entertainment Pack

```



Technology Pack demonstrates product, engineering, documentation, release, and developer-community publishing.



Education Pack demonstrates course, program, faculty, research, admissions, and academic-calendar publishing without becoming a learning management system or student information system.



Entertainment Pack demonstrates title, release, event, editorial, talent, and media-catalog publishing without becoming a rights-management, ticketing, or streaming platform.



All four packs must remain optional and must use the generic content, data, workflow, dashboard, page, and AgentSEO systems.



\## Finance Content Types



```txt

Market Commentary

Investor Education

Disclosure Announcement

Research Article

Product Notice

Investor Activity

FAQ

```



\## Finance Datasets



```txt

fund\_nav

ipo\_projects

security\_snapshot

market\_calendar

product\_catalog

research\_indicators

```



\## Finance Rules



```txt

require disclaimer before publish

require source before publish

require as\_of\_date for data pages

require review before publish

require data import batch for public datasets

block publishing if required fields are missing

flag sensitive phrases

require version history

```



\## Finance Dashboard Cards



```txt

fund NAV freshness

IPO data freshness

disclosure publishing status

market commentary review queue

missing disclaimer count

stale financial dataset count

financial data API usage

popular finance data pages

```



\## Finance Templates



```txt

finance homepage

disclosure list

fund list

fund detail

IPO project list

research portal

investor activity page

investor education page

```



\## Acceptance Criteria



\* Core runs without any industry pack.

\* Finance Pack can register content types.

\* Finance Pack can register dataset schemas.

\* Finance Pack can register validation rules.

\* Finance Pack can register dashboard cards.

\* Finance Pack pages render using generic page/data systems.

\* Finance-specific terms appear only inside Finance Pack.

\* Technology, education, and entertainment concepts appear only inside their respective packs.

\* Every reference pack uses the same manifest validation and registration pipeline.

\* Packs can be enabled independently and do not depend on one another.



\---



\# R11 — Production Hardening + Migration Toolkit


\*\*Status: acceptance baseline completed.\*\* R11 closes at controlled migration staging; provider-specific object storage, distributed cache/CDN purge, external search, and transactional migration promotion require concrete providers and durable cross-module transaction boundaries.



\## Goal



Make OpenPortalKit suitable for serious self-hosted enterprise deployment and legacy website migration.



\## Security



Implement:



```txt

secure password handling

CSRF protection

XSS protection

content sanitization

file upload validation

MIME type checks

file size limits

permission checks

audit logs

rate limiting

admin session timeout

security headers

secret management

dependency scanning

```



\## Reliability



Implement:



```txt

health checks

readiness checks

background job retries

idempotent jobs

database migration safety

Redis failure handling

search index rebuild

cache invalidation

graceful shutdown

backup documentation

```



\## Performance



Optimize:



```txt

public page caching

CDN compatibility

image optimization

database indexes

search indexes

API pagination

N+1 query prevention

static page regeneration

Redis caching

ETag / Last-Modified

cache tags

```



\## Migration Toolkit



Support:



```txt

legacy content importer

CSV importer

SQL table importer

attachment importer

slug migration

redirect mapping

old URL to new URL mapping

content cleanup report

missing asset report

duplicate content detection

dry-run mode

```



\## Legacy .NET Migration Example



Document:



```txt

ASP.NET MVC3 / MVC5 migration

EF6 database extraction

old route mapping

legacy attachment migration

staged migration

strangler pattern

parallel run

rollback plan

```



\## Acceptance Criteria



\* Admin routes are protected.

\* Public pages are cacheable.

\* Background jobs are retry-safe.

\* Failed imports do not corrupt existing data.

\* Logs contain trace IDs.

\* Health checks work in Docker.

\* Legacy content can be imported in dry-run mode.

\* Old URLs can redirect to new URLs.

\* Migration errors are exported as reports.



\---



\# R12 — Developer Experience + 1.0 Stabilization


\*\*Status: repository implementation complete; 1.0 RC awaits target-environment rehearsal.\*\* R12 provides the
cross-platform `opk` contract, structural boundary enforcement, repository/live AgentSEO readiness checks, stable
extension/public contracts, runnable examples, browser coverage, and a release evidence checklist. Windows Server
and real-provider OIDC rehearsals remain explicit external release gates.



\## Goal



Make OpenPortalKit easy for humans and coding agents to extend safely.



\## Required Documentation



```txt

README.md

ROADMAP.md

ARCHITECTURE.md

AGENTS.md

CONTRIBUTING.md

SECURITY.md

MODULES.md

INDUSTRY\_PACKS.md

AGENT\_SEO.md

DASHBOARD.md

DATA\_PUBLISHING.md

DEPLOYMENT.md

MIGRATION\_FROM\_LEGACY\_DOTNET.md

```



\## CLI Tools



Optional CLI:



```txt

opk new

opk module add

opk industry-pack add

opk seed

opk reindex

opk check-boundaries

opk check-agent-readiness

opk export-snapshots

opk import legacy

```



\## Boundary Checker



Add:



```txt

opk check-boundaries

```



It should check:



```txt

core does not contain finance-specific terms

modules do not violate dependency rules

public API does not expose admin-only fields

industry packs do not create core dependencies

migrations have tests

public content considers AgentSEO output

```



\## Agent Readiness Checker



Add:



```txt

opk check-agent-readiness

```



It should check:



```txt

SSR/SSG availability

title/description/canonical

JSON-LD

Markdown snapshot

JSON snapshot

sitemap inclusion

RSS inclusion

semantic HTML

accessible forms

dataset schema endpoint

dataset export endpoint

```



\## Example Sites



Provide runnable examples:



```txt

corporate portal

data publishing portal

research portal

activity portal

finance portal

```



\## 1.0 Requirements



OpenPortalKit can reach 1.0 when:



```txt

core APIs are stable

module boundaries are stable

industry pack contract is stable

content publishing is stable

data publishing is stable

workflow and audit are stable

dashboard is useful

AgentSEO outputs are stable

migration path is documented

production deployment guide exists

security baseline is complete

example sites are runnable

tests cover critical flows

core remains finance-neutral

```



\---



\# R13 — Project Scaffolding + Source Distribution


\*\*Status: complete.\*\* Batch 1 provides a product-grade `opk new` command that creates a complete,
traceable, independently buildable source workspace from an OpenPortalKit release checkout. Batch 2 adds
transactional, boundary-checked `opk module add` authoring with generated contract tests and solution registration.
Batch 3 adds strict versioned project profiles and read-only provenance-based upgrade inspection.
Batch 4 closes R13 with verified portable source-template archives for repository-independent project creation.


\## Goal


Make OpenPortalKit practical as a framework that organizations customize, rather than distributing one fixed
precompiled portal application.


\## Project Creation


```txt

opk new --name <display-name> --output <directory> --profile corporate|data|research|activity|finance

```


Generated workspaces must preserve module boundaries, migrations, tests, public-output contracts, deployment tools,
and source provenance. Generation must exclude secrets and local/build state, refuse overwrite, and complete
atomically. Industry-specific selections remain in their matching packs.


\## Extension Authoring


R13 authoring surfaces:


```txt

opk module add (implemented)

opk industry-pack add (implemented)

versioned project profiles (implemented)

source template archives (implemented)

upgrade inspection (implemented)

```


The project generator must not become a low-code platform, arbitrary merge engine, or business-domain generator.


\---


\# 7. Testing Strategy



\## Required Test Types



```txt

unit tests

integration tests

migration tests

API tests

admin workflow tests

public rendering tests

search indexing tests

background job tests

dashboard aggregation tests

AgentSEO snapshot tests

industry pack tests

security tests

```



\## Critical Test Scenarios



\### Content



```txt

create draft

edit draft

publish content

archive content

version content

reject content

scheduled publish

```



\### Data



```txt

create dataset

import CSV

reject invalid CSV

publish dataset

export dataset

preserve source batch

expose schema

```



\### Workflow



```txt

submit review

approve

reject

request changes

publish after approval

block unauthorized publish

```



\### Dashboard



```txt

aggregate content metrics

aggregate data import metrics

show failed jobs

show AgentSEO readiness

respect privacy settings

avoid slow public-page queries

```



\### AgentSEO



```txt

generate sitemap

generate RSS

generate llms.txt

generate Markdown snapshot

generate JSON snapshot

calculate readiness score

ensure JSON snapshot matches visible content

```



\### Finance Pack



```txt

enable Finance Pack

register finance content types

register finance datasets

block publishing without disclaimer

show finance dashboard cards

ensure core has no finance-specific entities

```



\### Security



```txt

unauthorized admin access blocked

permission checks enforced

file upload validation works

audit logs are written

CSRF protection works

rate limiting works

```



\## Acceptance Criteria



\* CI runs all required tests.

\* Core tests do not require Finance Pack.

\* Industry pack tests run separately.

\* Snapshot tests detect accidental output changes.

\* Migration tests protect database evolution.

\* Boundary checks can run in CI.



\---



\# 8. Version Plan



\## v0.1 — Foundation Release



Focus:



```txt

solution structure

Aspire AppHost

Docker Compose

PostgreSQL

Redis

module system

identity basics

audit log

outbox

job host

content CRUD

public pages

sitemap

RSS

```



Goal:



A developer can run a basic enterprise portal with news, pages, announcements, and activities.



\## v0.2 — Workflow Release



Focus:



```txt

review workflow

approval records

version history

audit query

scheduled publishing

archive/unpublish

admin workflow UI

```



Goal:



The project becomes suitable for controlled enterprise publishing.



\## v0.3 — Data Portal Release



Focus:



```txt

datasets

schema versions

data records

CSV import

import batches

data views

schema endpoint

JSON export

CSV export

```



Goal:



The project becomes more than a CMS by supporting structured data publishing.



\## v0.4 — Search + Dashboard Release



Focus:



```txt

PostgreSQL search

Meilisearch adapter

public search

admin search

dashboard metrics

content dashboard

data dashboard

job dashboard

site analytics baseline

```



Goal:



The project becomes observable and operationally useful.



\## v0.5 — AgentSEO Release



Focus:



```txt

Markdown snapshots

JSON snapshots

llms.txt

AI bot policy

Agent Readiness Score

OpenAPI public spec

AgentSEO dashboard

```



Goal:



The project becomes agent-readable and RAG-friendly by design.



\## v0.6 — Block Template Release



Focus:



```txt

predefined blocks

page templates

server-rendered block system

content list blocks

data table blocks

template seed data

```



Goal:



Organizations can assemble reusable public portal pages without a complex low-code system.



\## v0.7 — Industry Pack Release



Focus:



```txt

industry pack manifest

content type registration

dataset registration

rule registration

dashboard card registration

template registration

Finance Pack preview

```



Goal:



The project can adapt to vertical industries without changing core.



\## v0.8 — Finance Pack Release



Focus:



```txt

finance templates

finance datasets

finance validation rules

finance dashboard cards

finance sample site

finance import examples

```



Goal:



Demonstrate that OpenPortalKit can support regulated, data-heavy, high-trust industries.



\## v0.9 — Production Hardening Release



Focus:



```txt

security

observability

backup docs

rate limiting

migration importers

legacy URL mapping

deployment docs

performance

```



Goal:



The project is suitable for serious self-hosted deployment and legacy website migration.



\## v1.0 — Stable Public Release



Requirements:



```txt

stable core APIs

stable module boundaries

stable industry pack contract

complete documentation

production deployment guide

security baseline

migration guide

example sites

critical test coverage

finance-neutral core

```



\---



\# 9. Agent Execution Rules



Agents working on this project must follow these rules.



\## Rule 1: Preserve Generality



Do not introduce industry-specific concepts into core.



If a feature sounds finance-specific, implement it as:



```txt

generic core abstraction

\+ Finance Pack extension

```



\## Rule 2: Keep OpenPortalKit a Publishing Framework



Do not turn the project into:



```txt

BI platform

CRM

trading system

OA system

full BPM engine

low-code platform

data warehouse

```



\## Rule 3: Use Modular Monolith Boundaries



Do not create unnecessary microservices.



Keep modules clear and dependencies explicit.



\## Rule 4: Public Content Must Be Machine-Readable



When adding a public content type, consider:



```txt

HTML page

Markdown snapshot

JSON snapshot

sitemap entry

RSS inclusion

search document

Agent Readiness Score

```



\## Rule 5: Data Must Be Traceable



Structured data must preserve:



```txt

source

import batch

as\_of\_date

schema version

record version

checksum

created\_at

updated\_at

```



\## Rule 6: Publishing Must Be Auditable



Any write action that changes public output should produce an audit log.



Examples:



```txt

create

update

publish

unpublish

archive

import

delete

approve

reject

```



\## Rule 7: Admin Actions Must Be Permission Checked



Do not rely on frontend-only permission checks.



Permissions must be enforced server-side.



\## Rule 8: Do Not Break Static Rendering



Public pages should remain friendly to:



```txt

search engines

LLM crawlers

browser agents

RAG pipelines

users with limited JavaScript

```



\## Rule 9: Dashboard Must Not Own Business State



Dashboard should aggregate and display metrics.



Core business state belongs in:



```txt

Content

Data

Workflow

Jobs

Audit

Search

AgentAccess

```



\## Rule 10: Avoid Silent Data Loss



Destructive actions should prefer:



```txt

soft delete

archive

versioning

audit logs

dry-run import

rollback-friendly import

```



\## Rule 11: Keep Finance Pack Optional



The project must run fully without Finance Pack.



Finance Pack demonstrates extensibility.



It must not become a dependency of core modules.



\## Rule 12: Tests and Docs Are Part of the Feature



A feature is incomplete without:



```txt

tests

migration

documentation

example seed data when useful

boundary consideration

AgentSEO consideration for public output

dashboard consideration when metrics are relevant

```



\---



\# 10. Definition of Done



A roadmap item is done only when:



```txt

code is implemented

database migration exists

tests pass

public API is documented

admin behavior is documented

audit behavior is considered

dashboard impact is considered

AgentSEO behavior is considered for public content

seed data or example is provided when useful

no forbidden domain leakage occurs

CI passes

```



\---



\# 11. Long-Term Vision



OpenPortalKit should become a practical open-source base for organizations that need trustworthy public websites with structured data and agent-readable outputs.



The long-term vision is:



```txt

Enterprise Portal

\+ Content Platform

\+ Data Publishing

\+ Workflow

\+ Audit

\+ Search

\+ Dashboard

\+ AgentSEO

\+ Industry Packs

```



OpenPortalKit should help teams build websites that are:



```txt

readable by humans

searchable by search engines

usable by agents

friendly to RAG systems

observable by operators

safe for enterprise publishing

adaptable to regulated industries

```
