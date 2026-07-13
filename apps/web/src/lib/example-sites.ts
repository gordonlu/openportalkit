export type ExamplePublication = {
  title: string;
  category: string;
  date: string;
  freshness: string;
  format: "Article" | "Dataset" | "Event" | "Report";
};

export type ExampleSite = {
  slug: string;
  name: string;
  descriptor: string;
  title: string;
  summary: string;
  image: string;
  imageAlt: string;
  accent: string;
  accentStrong: string;
  pack?: string;
  statistics: { value: string; label: string }[];
  publications: ExamplePublication[];
};

export const exampleSites: ExampleSite[] = [
  {
    slug: "corporate",
    name: "Northstar Group",
    descriptor: "Corporate portal",
    title: "Engineering progress, published with evidence.",
    summary: "Company news, governance, research and operational data in one accountable public record.",
    image: "/examples/corporate.webp",
    imageAlt: "Engineers inspect an electric motor in a bright manufacturing laboratory.",
    accent: "#087c78",
    accentStrong: "#075f5c",
    statistics: [
      { value: "42", label: "Publications this quarter" },
      { value: "8", label: "Open datasets" },
      { value: "100%", label: "Reviewed before release" },
    ],
    publications: [
      { title: "2026 advanced manufacturing progress report", category: "Company", date: "13 Jul 2026", freshness: "Updated today", format: "Report" },
      { title: "Electric drive efficiency benchmark", category: "Research", date: "11 Jul 2026", freshness: "As of 30 Jun", format: "Dataset" },
      { title: "New materials laboratory opens to partners", category: "News", date: "08 Jul 2026", freshness: "Published", format: "Article" },
      { title: "Supplier responsibility standard", category: "Governance", date: "02 Jul 2026", freshness: "Version 3.2", format: "Report" },
    ],
  },
  {
    slug: "data",
    name: "Civic Data Exchange",
    descriptor: "Data publishing portal",
    title: "Public data with provenance built in.",
    summary: "Discover versioned datasets, inspect schemas and export records with source and freshness intact.",
    image: "/examples/data.webp",
    imageAlt: "Data analysts examine maps and charts in a public operations room.",
    accent: "#176b45",
    accentStrong: "#0f5134",
    statistics: [
      { value: "128", label: "Published datasets" },
      { value: "6h", label: "Median refresh time" },
      { value: "99.8%", label: "Schema compliance" },
    ],
    publications: [
      { title: "Regional transit service levels", category: "Mobility", date: "13 Jul 2026", freshness: "As of 12 Jul", format: "Dataset" },
      { title: "Air quality monitoring archive", category: "Environment", date: "12 Jul 2026", freshness: "Updated 6h ago", format: "Dataset" },
      { title: "Data quality and correction policy", category: "Standards", date: "09 Jul 2026", freshness: "Version 2.1", format: "Report" },
      { title: "Quarterly dataset change digest", category: "Updates", date: "01 Jul 2026", freshness: "Published", format: "Article" },
    ],
  },
  {
    slug: "research",
    name: "Meridian Research Institute",
    descriptor: "Research portal",
    title: "Research made citable and reusable.",
    summary: "Peer-reviewed findings, supporting datasets and reproducible methods for public discovery.",
    image: "/examples/research.webp",
    imageAlt: "Researchers examine a material sample beside scientific equipment.",
    accent: "#8a2445",
    accentStrong: "#681a34",
    statistics: [
      { value: "74", label: "Active studies" },
      { value: "31", label: "Open data deposits" },
      { value: "18", label: "Partner institutions" },
    ],
    publications: [
      { title: "Low-carbon binder durability study", category: "Materials", date: "12 Jul 2026", freshness: "Peer reviewed", format: "Report" },
      { title: "Urban heat observation dataset", category: "Climate", date: "10 Jul 2026", freshness: "As of 30 Jun", format: "Dataset" },
      { title: "Methods note: reproducible sample preparation", category: "Methods", date: "06 Jul 2026", freshness: "Version 1.1", format: "Article" },
      { title: "Open research briefing", category: "Briefing", date: "18 Jul 2026", freshness: "Registration open", format: "Event" },
    ],
  },
  {
    slug: "activity",
    name: "Common Ground",
    descriptor: "Activity portal",
    title: "Events that connect ideas and people.",
    summary: "A reliable public calendar with accessible event details, recordings and post-event resources.",
    image: "/examples/activity.webp",
    imageAlt: "A speaker addresses an audience at an inclusive technology forum.",
    accent: "#c4442f",
    accentStrong: "#92301f",
    statistics: [
      { value: "26", label: "Upcoming activities" },
      { value: "14", label: "Published recordings" },
      { value: "9", label: "Community partners" },
    ],
    publications: [
      { title: "Responsible technology public forum", category: "Forum", date: "20 Jul 2026", freshness: "48 seats left", format: "Event" },
      { title: "Open data skills workshop", category: "Workshop", date: "27 Jul 2026", freshness: "Registration open", format: "Event" },
      { title: "Spring community programme review", category: "Report", date: "08 Jul 2026", freshness: "Published", format: "Report" },
      { title: "Accessibility guide for event partners", category: "Guidance", date: "30 Jun 2026", freshness: "Version 1.3", format: "Article" },
    ],
  },
  {
    slug: "finance",
    name: "Northstar Investor Information",
    descriptor: "Finance Pack example",
    title: "Clear disclosure, durable public records.",
    summary: "Audited reports, announcements and reference datasets delivered through the optional Finance Pack.",
    image: "/examples/finance.webp",
    imageAlt: "A corporate team reviews audited reports in a bright boardroom.",
    accent: "#28603e",
    accentStrong: "#19462c",
    pack: "Finance Pack 0.1.0",
    statistics: [
      { value: "FY26", label: "Latest reporting period" },
      { value: "12", label: "Regulated announcements" },
      { value: "0", label: "Overdue disclosures" },
    ],
    publications: [
      { title: "FY2026 annual report", category: "Results", date: "13 Jul 2026", freshness: "Audited", format: "Report" },
      { title: "Five-year financial highlights", category: "Reference data", date: "13 Jul 2026", freshness: "As of FY26", format: "Dataset" },
      { title: "Board governance statement", category: "Governance", date: "11 Jul 2026", freshness: "Current", format: "Article" },
      { title: "Annual results briefing", category: "Calendar", date: "22 Jul 2026", freshness: "Confirmed", format: "Event" },
    ],
  },
];

export const exampleSiteMap = new Map(exampleSites.map((site) => [site.slug, site]));
