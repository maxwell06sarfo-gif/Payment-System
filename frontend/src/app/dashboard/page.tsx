import { Suspense } from "react";
import { DashboardClient } from "@/components/dashboard/dashboard-client";
import DashboardLoading from "./loading";

// Next.js App Router: any component that calls useSearchParams() must be
// wrapped in <Suspense> so the server can render a fallback while the
// client-side params are resolved.  Without this wrapper Next.js throws
// a hydration error (Bug fix: missing Suspense boundary).
export default function DashboardPage() {
  return (
    <Suspense fallback={<DashboardLoading />}>
      <DashboardClient />
    </Suspense>
  );
}
