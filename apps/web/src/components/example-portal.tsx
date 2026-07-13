"use client";

import Image from "next/image";
import Link from "next/link";
import { useState } from "react";
import type { ExampleSite } from "@/lib/example-sites";
import { exampleSites } from "@/lib/example-sites";

export function ExamplePortal({ site }: { site: ExampleSite }) {
  const [format, setFormat] = useState("All");
  const publications = format === "All"
    ? site.publications
    : site.publications.filter((publication) => publication.format === format);

  return (
    <main className={`portal portal-${site.slug}`} style={{ "--accent": site.accent, "--accent-strong": site.accentStrong } as React.CSSProperties}>
      <a className="skip-link" href="#publications">Skip to publications</a>
      <header className="portal-header">
        <Link className="brand" href={`/examples/${site.slug}`} aria-label={`${site.name} home`}>
          <span className="brand-mark" aria-hidden="true">N</span>
          <span>{site.name}</span>
        </Link>
        <nav className="primary-nav" aria-label="Primary navigation">
          <a href="#publications">Publications</a>
          <a href="#data">Data</a>
          <a href="#about">About</a>
        </nav>
        <details className="site-switcher">
          <summary>Examples</summary>
          <div className="site-menu">
            {exampleSites.map((entry) => (
              <Link key={entry.slug} aria-current={entry.slug === site.slug ? "page" : undefined} href={`/examples/${entry.slug}`}>
                <span>{entry.name}</span>
                <small>{entry.descriptor}</small>
              </Link>
            ))}
          </div>
        </details>
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

      <section className="statistics" id="data" aria-label="Portal statistics">
        {site.statistics.map((statistic) => (
          <div key={statistic.label}>
            <strong>{statistic.value}</strong>
            <span>{statistic.label}</span>
          </div>
        ))}
        <a href="/api/public">Explore public API <span aria-hidden="true">→</span></a>
      </section>

      <section className="publications" id="publications">
        <div className="section-heading">
          <div>
            <p>{sectionCopy[site.slug].eyebrow}</p>
            <h2>{sectionCopy[site.slug].title}</h2>
          </div>
          <label>
            <span>Content type</span>
            <select value={format} onChange={(event) => setFormat(event.target.value)}>
              {['All', 'Article', 'Dataset', 'Event', 'Report'].map((value) => <option key={value}>{value}</option>)}
            </select>
          </label>
        </div>
        <div className={`publication-table publication-table-${site.slug}`} role="region" aria-live="polite" aria-label="Filtered publications">
          <div className="publication-row publication-labels" aria-hidden="true">
            <span>Title</span><span>Category</span><span>Published</span><span>Status</span>
          </div>
          {publications.length > 0 ? publications.map((publication) => (
            <article className="publication-row" key={publication.title}>
              <h3><a href="#output-formats">{publication.title}</a><small>{publication.format}</small></h3>
              <span>{publication.category}</span>
              <time>{publication.date}</time>
              <span className="freshness"><i aria-hidden="true" />{publication.freshness}</span>
            </article>
          )) : <p className="empty-state">No publications match this content type.</p>}
        </div>
      </section>

      <section className="output-formats" id="output-formats">
        <div>
          <p>One source, multiple representations</p>
          <h2>Ready for people, search and agents.</h2>
        </div>
        <nav aria-label="Public output formats">
          <a href="/sitemap.xml">Sitemap</a>
          <a href="/rss.xml">RSS</a>
          <a href="/llms.txt">LLMs.txt</a>
          <a href="/api/openapi.json">OpenAPI</a>
        </nav>
      </section>

      <footer id="about">
        <strong>{site.name}</strong>
        <span>Runnable OpenPortalKit R12 example</span>
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
