# Business Process Agents - Admin UI

Admin interface for the Business Process Agents MVP platform. This Next.js application provides operator tools for monitoring and managing the agent fleet, runs, and deployments.

## Features

- **Fleet Dashboard** - Monitor nodes and active runs
- **Runs List** - View latest runs with status and duration
- **Agent Editor** - Create and manage agent definitions
- **OIDC Authentication** - Secure login via Keycloak (planned for E5-T2)

## Technology Stack

- **Next.js 14+** with App Router and Server Components
- **TypeScript** with strict mode enabled
- **Tailwind CSS** for styling
- **shadcn/ui** component library
- **React 19** with modern hooks and patterns

## Prerequisites

- Node.js 18+ or 20+
- npm or yarn

## Getting Started

### Installation

```bash
# Install dependencies
npm install
```

### Configuration

Copy the example environment file and configure:

```bash
cp .env.example .env.local
```

Edit `.env.local` to configure the API endpoint and authentication settings.

### Development

Run the development server:

```bash
npm run dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the application.

### Building

Build the production application:

```bash
npm run build
```

### Production

Start the production server:

```bash
npm run start
```

## Project Structure

```
src/admin-ui/
├── src/
│   ├── app/              # Next.js App Router pages
│   │   ├── layout.tsx    # Root layout
│   │   ├── page.tsx      # Home page
│   │   └── globals.css   # Global styles with Tailwind and shadcn/ui
│   ├── components/       # React components
│   │   └── ui/           # shadcn/ui components
│   ├── lib/              # Utility libraries
│   │   └── utils.ts      # Tailwind merge utility
│   ├── hooks/            # Custom React hooks
│   └── utils/            # Helper functions
├── public/               # Static assets
├── components.json       # shadcn/ui configuration
├── tsconfig.json         # TypeScript configuration
├── next.config.ts        # Next.js configuration
├── postcss.config.mjs    # PostCSS configuration
└── package.json          # Dependencies and scripts
```

## Available Scripts

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run start` - Start production server
- `npm run lint` - Run ESLint

## Development Guidelines

### Code Style

- Follow TypeScript strict mode conventions
- Use functional components with React hooks
- Prefer Server Components for data fetching when possible
- Keep client and server logic well-separated

### Component Guidelines

- Use shadcn/ui components for consistent UI
- Leverage Tailwind utility classes for styling
- Ensure accessibility (ARIA labels, semantic HTML)
- Optimize images with `next/image`

### Performance

- Use dynamic imports for code splitting
- Implement proper caching strategies
- Optimize bundle size
- Follow Next.js performance best practices

## Roadmap

### Phase 1 - Foundation (Current - E5-T1)

- [x] Next.js setup with TypeScript
- [x] Tailwind CSS integration
- [x] shadcn/ui component library
- [x] Basic project structure
- [x] Development environment

### Phase 2 - Authentication (E5-T2)

- [ ] OIDC integration with Keycloak
- [ ] Protected routes
- [ ] Session management

### Phase 3 - Core Features (E5-T3, E5-T4, E5-T5)

- [ ] Fleet Dashboard
- [ ] Runs List with filtering
- [ ] Agent Editor with form validation

## Related Documentation

- [System Architecture Document](../../sad.md)
- [Tasks Overview](../../tasks.yaml)
- [Control Plane API](../ControlPlane.Api/README.md)

## License

Internal project - Platform Engineering
