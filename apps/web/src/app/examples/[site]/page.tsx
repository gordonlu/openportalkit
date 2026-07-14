import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ExamplePortal } from "@/components/example-portal";
import { exampleSites, getExampleSite } from "@/lib/example-sites";

export const dynamicParams = false;

export function generateStaticParams() {
  return exampleSites.map((site) => ({ site: site.slug }));
}

export async function generateMetadata({ params }: PageProps<"/examples/[site]">): Promise<Metadata> {
  const { site: slug } = await params;
  const site = getExampleSite(slug);
  if (!site) return {};
  return {
    title: `${site.name} | OpenPortalKit Example`,
    description: site.summary,
  };
}

export default async function ExamplePage({ params }: PageProps<"/examples/[site]">) {
  const { site: slug } = await params;
  const site = getExampleSite(slug);
  if (!site) notFound();
  return <ExamplePortal site={site} />;
}
