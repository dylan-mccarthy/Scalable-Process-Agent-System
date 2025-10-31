import { signIn } from "@/auth";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export default function LoginPage() {
  return (
    <div className="bg-background flex min-h-screen items-center justify-center">
      <Card className="w-full max-w-md">
        <CardHeader className="space-y-1">
          <CardTitle className="text-2xl font-bold">Welcome Back</CardTitle>
          <CardDescription>Sign in to Business Process Agents Admin UI</CardDescription>
        </CardHeader>
        <CardContent>
          <form
            action={async () => {
              "use server";
              await signIn("keycloak", { redirectTo: "/" });
            }}
          >
            <Button type="submit" className="w-full">
              Sign in with Keycloak
            </Button>
          </form>
          <p className="text-muted-foreground mt-4 text-center text-sm">
            Secure authentication via OIDC
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
