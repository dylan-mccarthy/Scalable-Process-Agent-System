export { auth as middleware } from "@/auth";

// Matcher configuration:
// - Excludes Next.js internals: api, _next/static, _next/image
// - Excludes static files: favicon.ico
// - All other routes require authentication
export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico).*)"],
};
