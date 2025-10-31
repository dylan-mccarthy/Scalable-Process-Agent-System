import { auth } from "@/auth";
import { UserMenu } from "./user-menu";
import Link from "next/link";

export async function Header() {
  const session = await auth();

  return (
    <header className="bg-background/95 supports-[backdrop-filter]:bg-background/60 sticky top-0 z-50 w-full border-b backdrop-blur">
      <div className="container flex h-14 items-center">
        <div className="mr-4 flex flex-1 items-center gap-6">
          <Link className="flex items-center space-x-2" href="/">
            <span className="font-bold">Business Process Agents</span>
          </Link>
          <nav className="flex items-center gap-6 text-sm font-medium">
            <Link
              className="text-foreground/60 hover:text-foreground transition-colors"
              href="/fleet"
            >
              Fleet
            </Link>
            <Link
              className="text-foreground/60 hover:text-foreground transition-colors"
              href="/runs"
            >
              Runs
            </Link>
            <Link
              className="text-foreground/60 hover:text-foreground transition-colors"
              href="/agents"
            >
              Agents
            </Link>
          </nav>
        </div>
        <div className="flex items-center space-x-4">
          {session?.user && (
            <UserMenu userName={session.user.name} userEmail={session.user.email} />
          )}
        </div>
      </div>
    </header>
  );
}
