import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "OpenPortalKit Examples",
  description: "Runnable enterprise portal examples built with OpenPortalKit.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      className="h-full antialiased"
      data-scroll-behavior="smooth"
    >
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
