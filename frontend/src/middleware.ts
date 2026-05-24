import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const hasAuthMarker = request.cookies.get("ps_auth")?.value === "1";

  if (pathname.startsWith("/dashboard") && !hasAuthMarker) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  if (pathname === "/" && hasAuthMarker) {
    return NextResponse.redirect(new URL("/dashboard", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/", "/dashboard/:path*"],
};