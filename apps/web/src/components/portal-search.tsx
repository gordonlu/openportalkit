"use client";

import { FormEvent, useState } from "react";
import type { PortalSearchResult } from "@/lib/public-api";

export function PortalSearch({ enabled }: { enabled: boolean }) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<PortalSearchResult[]>([]);
  const [state, setState] = useState<"idle" | "loading" | "ready" | "error">("idle");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalized = query.trim();
    if (!enabled || !normalized) return;
    setState("loading");
    try {
      const response = await fetch(`/api/portal/search?q=${encodeURIComponent(normalized)}`, {
        headers: { Accept: "application/json" },
      });
      if (!response.ok) throw new Error("Search request failed.");
      const body = await response.json() as { items?: PortalSearchResult[] };
      setResults(Array.isArray(body.items) ? body.items : []);
      setState("ready");
    } catch {
      setResults([]);
      setState("error");
    }
  }

  return (
    <section className="portal-search" id="search" aria-labelledby="search-title">
      <div>
        <p>Public search</p>
        <h2 id="search-title">Find published information.</h2>
      </div>
      <form onSubmit={submit} role="search">
        <label htmlFor="portal-search-query">Search content and datasets</label>
        <div>
          <input
            id="portal-search-query"
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            minLength={1}
            maxLength={200}
            disabled={!enabled}
          />
          <button type="submit" disabled={!enabled || state === "loading"}>
            {state === "loading" ? "Searching" : "Search"}
          </button>
        </div>
      </form>
      <div className="search-results" aria-live="polite">
        {state === "error" ? <p>Public search is temporarily unavailable.</p> : null}
        {state === "ready" && results.length === 0 ? <p>No published results found.</p> : null}
        {results.map((result) => (
          <article key={`${result.type}:${result.id}`}>
            <span>{result.type}</span>
            <h3><a href={result.href}>{result.title}</a></h3>
            {result.summary ? <p>{result.summary}</p> : null}
          </article>
        ))}
      </div>
    </section>
  );
}
