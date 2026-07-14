import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ExamplePortal } from "@/components/example-portal";
import { branding, isBrandingActive } from "@/lib/branding";
import { exampleSites, getExampleSite } from "@/lib/example-sites";
import { getConfiguredPublicBaseUrl, getPortalDataMode, getPortalRuntimeData } from "@/lib/public-api";

export const dynamicParams = false;
export const dynamic = "force-dynamic";

export function generateStaticParams() {
  return exampleSites.map((site) => ({ site: site.slug }));
}

export async function generateMetadata({ params }: PageProps<"/examples/[site]">): Promise<Metadata> {
  const { site: slug } = await params;
  const site = getExampleSite(slug);
  if (!site) return {};
  const publicBaseUrl = getConfiguredPublicBaseUrl();
  const canonical = publicBaseUrl ? new URL(`/examples/${site.slug}`, publicBaseUrl).toString() : undefined;
  const live = getPortalDataMode() === "live";
  const branded = isBrandingActive(site.slug, live);
  const title = branded ? branding.site.name : site.name;
  const description = branded ? branding.site.description : site.summary;
  const socialImage = branded ? branding.assets.socialImage : { src: site.image, alt: site.imageAlt };
  return {
    title: branded ? title : `${title} | OpenPortalKit Example`,
    description,
    metadataBase: publicBaseUrl,
    alternates: canonical ? { canonical } : undefined,
    openGraph: {
      title,
      description,
      url: canonical,
      images: publicBaseUrl ? [{ url: new URL(socialImage.src, publicBaseUrl), alt: socialImage.alt }] : undefined,
      type: "website",
    },
  };
}

export default async function ExamplePage({ params }: PageProps<"/examples/[site]">) {
  const { site: slug } = await params;
  const site = getExampleSite(slug);
  if (!site) notFound();
  const runtime = await getPortalRuntimeData();
  return <ExamplePortal site={site} runtime={runtime} />;
}
