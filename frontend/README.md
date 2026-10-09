# Moongate Admin frontend

React + TypeScript + Vite, shadcn/ui and Tailwind CSS v4. Talks only to the Moongate Admin REST API under `/api`.

Scripts: `npm run dev`, `npm run build`, `npm run lint`, `npm run typecheck`, `npm test`.

The JWT is kept in memory only (no web storage); reloading signs you out. Mutations are never retried.
