import { NextResponse } from "next/server";
import { searchPublicPortal } from "@/lib/public-api";

export async function GET(request: Request) {
  const query = new URL(request.url).searchParams.get("q")?.trim() || "";
  if (query.length < 1 || query.length > 200) {
    return NextResponse.json({ error: "q must contain between 1 and 200 characters." }, { status: 400 });
  }
  try {
    return NextResponse.json({ items: await searchPublicPortal(query) });
  } catch (error) {
    console.error("OpenPortalKit public search dependency is unavailable.", error);
    return NextResponse.json({ error: "Public search is temporarily unavailable." }, { status: 503 });
  }
}
