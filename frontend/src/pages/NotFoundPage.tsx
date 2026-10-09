import { Link } from "react-router-dom";

export function NotFoundPage() {
  return (
    <div className="mx-auto mt-24 max-w-md text-center">
      <h2 className="text-2xl font-semibold">Page not found</h2>
      <p className="mt-2 text-muted-foreground">The page you are looking for does not exist.</p>
      <Link className="mt-4 inline-block underline" to="/servers">
        Back to servers
      </Link>
    </div>
  );
}
