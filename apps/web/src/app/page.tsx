import Link from "next/link";

export default function Home() {
  return (
    <main className="min-h-screen bg-stone-50 text-neutral-950">
      <section className="mx-auto flex min-h-screen w-full max-w-6xl flex-col justify-between px-6 py-8 md:px-10">
        <nav className="flex items-center justify-between border-b border-neutral-200 pb-5">
          <Link className="text-sm font-semibold uppercase tracking-[0.14em]" href="/">
            OpenPortalKit
          </Link>
          <div className="flex gap-5 text-sm text-neutral-600">
            <a href="#modules">Modules</a>
            <a href="#outputs">Outputs</a>
            <a href="#packs">Packs</a>
          </div>
        </nav>

        <div className="grid gap-12 py-16 md:grid-cols-[1.2fr_0.8fr] md:items-center">
          <div>
            <p className="mb-5 text-sm font-medium uppercase tracking-[0.18em] text-emerald-700">
              Enterprise portal framework
            </p>
            <h1 className="max-w-3xl text-5xl font-semibold leading-tight md:text-7xl">
              Publish trustworthy content and structured data.
            </h1>
            <p className="mt-6 max-w-2xl text-lg leading-8 text-neutral-700">
              OpenPortalKit starts as a modular monolith for public websites,
              data portals, workflow, audit, search, dashboards, and
              agent-readable outputs.
            </p>
          </div>

          <div className="border border-neutral-300 bg-white p-6 shadow-sm">
            <h2 className="text-base font-semibold">Initialized surface</h2>
            <dl className="mt-6 grid gap-4 text-sm">
              <div className="flex justify-between border-b border-neutral-100 pb-3">
                <dt className="text-neutral-500">Backend</dt>
                <dd>.NET 10 modular monolith</dd>
              </div>
              <div className="flex justify-between border-b border-neutral-100 pb-3">
                <dt className="text-neutral-500">Public web</dt>
                <dd>Next.js App Router</dd>
              </div>
              <div className="flex justify-between border-b border-neutral-100 pb-3">
                <dt className="text-neutral-500">Core rule</dt>
                <dd>Industry-neutral</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-neutral-500">Pack model</dt>
                <dd>Optional industry packs</dd>
              </div>
            </dl>
          </div>
        </div>

        <div className="grid gap-5 border-t border-neutral-200 pt-8 md:grid-cols-3">
          <section id="modules">
            <h2 className="text-lg font-semibold">Module Boundaries</h2>
            <p className="mt-3 text-sm leading-6 text-neutral-700">
              Kernel, Content, Data, Search, SEO, AgentAccess, Dashboard,
              Audit, Workflow, Assets, Identity, and Jobs are separate projects.
            </p>
          </section>
          <section id="outputs">
            <h2 className="text-lg font-semibold">Machine-Readable Outputs</h2>
            <p className="mt-3 text-sm leading-6 text-neutral-700">
              Public resources should support HTML, Markdown, JSON, RSS,
              sitemap entries, and public OpenAPI descriptions.
            </p>
          </section>
          <section id="packs">
            <h2 className="text-lg font-semibold">Industry Packs</h2>
            <p className="mt-3 text-sm leading-6 text-neutral-700">
              Vertical adaptation lives outside the core. The first placeholder
              pack demonstrates how specialized publishing rules stay optional.
            </p>
          </section>
        </div>
      </section>
    </main>
  );
}
