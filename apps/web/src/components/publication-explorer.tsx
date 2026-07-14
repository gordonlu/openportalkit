"use client";

import { useState } from "react";
import type { ExamplePublication } from "@/lib/example-sites";

export function PublicationExplorer({
  publications,
  siteSlug,
  eyebrow,
  title,
  unavailable = false,
}: {
  publications: ExamplePublication[];
  siteSlug: string;
  eyebrow: string;
  title: string;
  unavailable?: boolean;
}) {
  const [format, setFormat] = useState("All");
  const formats = ["All", "Article", "Dataset", "Event", "Page", "Report"];
  const filtered = format === "All"
    ? publications
    : publications.filter((publication) => publication.format === format);

  return (
    <>
      <div className="section-heading">
        <div>
          <p>{eyebrow}</p>
          <h2>{title}</h2>
        </div>
        <label>
          <span>Content type</span>
          <select value={format} onChange={(event) => setFormat(event.target.value)} disabled={unavailable}>
            {formats.map((value) => <option key={value}>{value}</option>)}
          </select>
        </label>
      </div>
      <div className={`publication-table publication-table-${siteSlug}`} role="region" aria-live="polite" aria-label="Filtered publications">
        <div className="publication-row publication-labels" aria-hidden="true">
          <span>Title</span><span>Category</span><span>Published</span><span>Status</span>
        </div>
        {filtered.length > 0 ? filtered.map((publication) => (
          <article className="publication-row" key={publication.id || publication.title}>
            <h3><a href={publication.href || "#output-formats"}>{publication.title}</a><small>{publication.format}</small></h3>
            <span>{publication.category}</span>
            <time>{publication.date}</time>
            <span className="freshness"><i aria-hidden="true" />{publication.freshness}</span>
          </article>
        )) : (
          <p className="empty-state" role={unavailable ? "status" : undefined}>
            {unavailable ? "Publications are temporarily unavailable." : "No publications match this content type."}
          </p>
        )}
      </div>
    </>
  );
}
