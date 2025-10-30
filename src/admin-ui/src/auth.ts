import NextAuth from "next-auth";
import Keycloak from "next-auth/providers/keycloak";

export const { handlers, signIn, signOut, auth } = NextAuth({
  providers: [
    Keycloak({
      clientId: process.env.AUTH_KEYCLOAK_ID ?? "",
      clientSecret: process.env.AUTH_KEYCLOAK_SECRET ?? "",
      issuer: process.env.AUTH_KEYCLOAK_ISSUER ?? "",
    }),
  ],
  callbacks: {
    authorized({ auth: session, request: { nextUrl } }) {
      const isLoggedIn = !!session?.user;
      const isOnLoginPage = nextUrl.pathname.startsWith("/login");
      
      if (isOnLoginPage) {
        if (isLoggedIn) {
          // Redirect authenticated users away from login page
          return Response.redirect(new URL("/", nextUrl));
        }
        return true;
      }
      
      // Require authentication for all other pages
      return isLoggedIn;
    },
    jwt({ token, user, account }) {
      if (account && user) {
        token.accessToken = account.access_token;
        token.idToken = account.id_token;
      }
      return token;
    },
    session({ session, token }) {
      if (token) {
        session.accessToken = token.accessToken as string;
        session.idToken = token.idToken as string;
      }
      return session;
    },
  },
  pages: {
    signIn: "/login",
  },
  trustHost: true,
});
