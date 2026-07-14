import type { Metadata } from "next";
import { branding } from "@/lib/branding";
import "./globals.css";

export const metadata: Metadata = {
  title: branding.site.name,
  description: branding.site.description,
  icons: { icon: branding.assets.favicon.src },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang={branding.site.locale}
      className="h-full antialiased"
      data-scroll-behavior="smooth"
    >
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
