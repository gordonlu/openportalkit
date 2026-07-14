import type { CSSProperties } from "react";
import Image from "next/image";
import Link from "next/link";
import { PublicationExplorer } from "@/components/publication-explorer";
import { PortalSearch } from "@/components/portal-search";
import { branding, isBrandingActive } from "@/lib/branding";
import type { ExampleSite } from "@/lib/example-sites";
import { exampleSites } from "@/lib/example-sites";
import type { PortalRuntimeData } from "@/lib/public-api";

export function ExamplePortal({ site, runtime }: { site: ExampleSite; runtime: PortalRuntimeData }) {
  const live = runtime.mode === "live";
  const branded = isBrandingActive(site.slug, live);
  const identity = branded ? branding.site : { name: site.name, shortName: "N" };
  const colors = branded ? branding.colors : { accent: site.accent, accentStrong: site.accentStrong, surface: "#ffffff", surfaceMuted: "#f4f7f6", text: "#151918" };
  const unavailable = live && runtime.status === "unavailable";
  const publications = live ? runtime.publications : site.publications;
  const statistics = live ? runtime.statistics : site.statistics;
  const publicHref = (path: string) => {
    if (path.startsWith("#") || path.startsWith("https://")) return path;
    return runtime.publicBaseUrl ? new URL(path, runtime.publicBaseUrl).toString() : path;
  };

  return (
    <main
      className={`portal portal-${site.slug}`}
      data-typography={branded ? branding.typography.preset : undefined}
      style={{ "--accent": colors.accent, "--accent-strong": colors.accentStrong, "--surface": colors.surface, "--surface-muted": colors.surfaceMuted, "--text": colors.text } as CSSProperties}
    >
      <a className="skip-link" href="#publications">Skip to publications</a>
      <header className={`portal-header${branded ? " portal-header-branded" : ""}`}>
        <Link className="brand" href={`/examples/${site.slug}`} aria-label={`${identity.name} home`}>
          {branded && branding.assets.logo ? (
            <Image className="brand-logo" src={branding.assets.logo.src} alt={branding.assets.logo.alt ?? ""} width={branding.assets.logo.width} height={branding.assets.logo.height} />
          ) : (
            <span className="brand-mark" aria-hidden="true">{identity.shortName}</span>
          )}
          <span>{identity.name}</span>
        </Link>
        <nav className="primary-nav" aria-label="Primary navigation">
          {(branded ? branding.navigation : defaultNavigation)
            .filter((link) => live || link.href !== "#search")
            .map((link) => <a key={`${link.label}-${link.href}`} href={publicHref(link.href)}>{link.label}</a>)}
        </nav>
        {!branded ? <details className="site-switcher">
          <summary>Examples</summary>
          <div className="site-menu">
            {exampleSites.map((entry) => (
              <Link key={entry.slug} aria-current={entry.slug === site.slug ? "page" : undefined} href={`/examples/${entry.slug}`}>
                <span>{entry.name}</span>
                <small>{entry.descriptor}</small>
              </Link>
            ))}
          </div>
        </details> : null}
        {branded ? (
          <details className="mobile-navigation">
            <summary>Menu</summary>
            <nav aria-label="Mobile navigation">
              {branding.navigation
                .filter((link) => live || link.href !== "#search")
                .map((link) => <a key={`${link.label}-${link.href}`} href={publicHref(link.href)}>{link.label}</a>)}
            </nav>
          </details>
        ) : null}
      </header>

      <section className="hero" aria-labelledby="hero-title">
        <Image
          className="hero-image"
          src={site.image}
          alt={site.imageAlt}
          fill
          priority
          sizes={site.slug === "data" || site.slug === "finance" ? "(max-width: 820px) 100vw, 52vw" : "100vw"}
        />
        <div className="hero-copy">
          <p>{site.descriptor}</p>
          <h1 id="hero-title">{site.title}</h1>
          <span>{site.summary}</span>
          {site.pack ? <strong>{site.pack}</strong> : null}
        </div>
      </section>

      {live ? (
        <div className={`runtime-status runtime-status-${runtime.status}`} role="status">
          <strong>{unavailable ? "Public API unavailable" : "Live public data"}</strong>
          <span>{unavailable ? "The portal will retry automatically. No demo records are being shown." : "This view is reading published records from OpenPortalKit."}</span>
        </div>
      ) : null}

      <section className="statistics" id="data" aria-label="Portal statistics">
        {statistics.map((statistic) => (
          <div key={statistic.label}>
            <strong>{statistic.value}</strong>
            <span>{statistic.label}</span>
          </div>
        ))}
        <a href={publicHref("/api/public/content")}>Explore public API <span aria-hidden="true">→</span></a>
      </section>

      {live ? (
        <section className="dataset-catalog" aria-labelledby="datasets-title">
          <div className="section-heading">
            <div>
              <p>Structured public data</p>
              <h2 id="datasets-title">Dataset catalogue</h2>
            </div>
            <span>{runtime.datasets.length} available</span>
          </div>
          <div className="dataset-list">
            {runtime.datasets.map((dataset) => (
              <article key={dataset.code}>
                <span>{dataset.code}</span>
                <h3><a href={dataset.href}>{dataset.name}</a></h3>
                <p>{dataset.description}</p>
                <time>Updated {dataset.updatedAt}</time>
              </article>
            ))}
            {runtime.datasets.length === 0 ? (
              <p className="empty-state">{unavailable ? "Dataset catalogue is temporarily unavailable." : "No public datasets are available."}</p>
            ) : null}
          </div>
        </section>
      ) : null}

      {live ? <PortalSearch enabled={!unavailable} /> : null}

      <section className="publications" id="publications">
        <PublicationExplorer
          publications={publications}
          siteSlug={site.slug}
          eyebrow={sectionCopy[site.slug].eyebrow}
          title={sectionCopy[site.slug].title}
          unavailable={unavailable}
        />
      </section>

      <section className="output-formats" id="output-formats">
        <div>
          <p>One source, multiple representations</p>
          <h2>Ready for people, search and agents.</h2>
        </div>
        <nav aria-label="Public output formats">
          <a href={publicHref("/sitemap.xml")}>Sitemap</a>
          <a href={publicHref("/rss.xml")}>RSS</a>
          <a href={publicHref("/llms.txt")}>LLMs.txt</a>
          <a href={publicHref("/api/openapi.json")}>OpenAPI</a>
        </nav>
      </section>

      <footer id="about">
        <strong>{branded ? branding.footer.copyright : site.name}</strong>
        {branded ? (
          <nav aria-label="Footer navigation">
            {branding.footer.links.map((link) => <a key={`${link.label}-${link.href}`} href={publicHref(link.href)}>{link.label}</a>)}
          </nav>
        ) : <span>Runnable OpenPortalKit example</span>}
      </footer>
    </main>
  );
}

const sectionCopy: Record<string, { eyebrow: string; title: string }> = {
  corporate: { eyebrow: "Latest public outputs", title: "Recently published" },
  data: { eyebrow: "Dataset catalogue", title: "Browse trusted data" },
  research: { eyebrow: "Research library", title: "Findings and methods" },
  activity: { eyebrow: "Programme", title: "What is happening" },
  finance: { eyebrow: "Disclosure centre", title: "Reports and announcements" },
};

const defaultNavigation = [
  { label: "Publications", href: "#publications" },
  { label: "Data", href: "#data" },
  { label: "About", href: "#about" },
];
