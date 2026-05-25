import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const hasAuthMarker = request.cookies.get("ps_auth")?.value === "1";

  // Only guard dashboard routes. Unauthenticated users hitting /dashboard
  // are redirected to the login page.
  // We do NOT redirect "/" → "/dashboard" here because the cookie can be
  // stale (expired JWT, cleared localStorage). The auth-panel component
  // already calls api.getProfile() and redirects only when the token is
  // still valid, which is the correct server-verified check.
  if (pathname.startsWith("/dashboard") && !hasAuthMarker) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/dashboard/:path*"],
};
