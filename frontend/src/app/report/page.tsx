import { redirect } from "next/navigation";

export default function LegacyReportIndexPage() {
  redirect("/reports");
}
