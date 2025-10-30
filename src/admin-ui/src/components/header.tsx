import { auth } from "@/auth";
import { UserMenu } from "./user-menu";
import Link from "next/link";

export async function Header() {
  const session = await auth();

  return (
    <header className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className="container flex h-14 items-center">
        <div className="mr-4 flex flex-1">
          <Link className="mr-6 flex items-center space-x-2" href="/">
            <span className="font-bold">Business Process Agents</span>
          </Link>
        </div>
        <div className="flex items-center space-x-4">
          {session?.user && <UserMenu userName={session.user.name} userEmail={session.user.email} />}
        </div>
      </div>
    </header>
  );
}
