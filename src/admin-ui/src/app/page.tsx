import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export default function Home() {
  return (
    <div className="bg-background min-h-screen">
      <main className="container mx-auto px-4 py-8">
        <div className="mx-auto max-w-4xl space-y-8">
          <div className="space-y-2">
            <h1 className="text-4xl font-bold tracking-tight">Business Process Agents</h1>
            <p className="text-muted-foreground text-lg">
              Admin interface for monitoring and managing the agent platform
            </p>
          </div>

          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            <Card>
              <CardHeader>
                <CardTitle>Fleet Dashboard</CardTitle>
                <CardDescription>Monitor nodes and active runs</CardDescription>
              </CardHeader>
              <CardContent>
                <Link href="/fleet">
                  <Button variant="outline" className="w-full">
                    View Dashboard
                  </Button>
                </Link>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Runs</CardTitle>
                <CardDescription>View latest runs with status</CardDescription>
              </CardHeader>
              <CardContent>
                <Link href="/runs">
                  <Button variant="outline" className="w-full">
                    View Runs
                  </Button>
                </Link>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Agent Editor</CardTitle>
                <CardDescription>Create and manage agents</CardDescription>
              </CardHeader>
              <CardContent>
                <Button variant="outline" className="w-full">
                  Manage Agents
                </Button>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Getting Started</CardTitle>
              <CardDescription>Welcome to the Business Process Agents Admin UI</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <h3 className="mb-2 font-semibold">Features</h3>
                <ul className="text-muted-foreground list-inside list-disc space-y-1 text-sm">
                  <li>Fleet Dashboard - Monitor nodes and active runs</li>
                  <li>Runs List - View latest runs with status and duration</li>
                  <li>Agent Editor - Create and manage agent definitions</li>
                  <li>OIDC Authentication - Secure login via Keycloak</li>
                </ul>
              </div>
              <div>
                <h3 className="mb-2 font-semibold">Technology Stack</h3>
                <ul className="text-muted-foreground list-inside list-disc space-y-1 text-sm">
                  <li>Next.js 14+ with App Router</li>
                  <li>TypeScript with strict mode</li>
                  <li>Tailwind CSS for styling</li>
                  <li>shadcn/ui component library</li>
                </ul>
              </div>
            </CardContent>
          </Card>
        </div>
      </main>
    </div>
  );
}
