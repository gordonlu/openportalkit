import { redirect } from "next/navigation";
import { defaultExampleSite } from "@/lib/example-sites";

export default function Home() {
  redirect(`/examples/${defaultExampleSite}`);
}
