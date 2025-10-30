# Business Process Agents - Admin UI

Admin interface for the Business Process Agents MVP platform. This Next.js application provides operator tools for monitoring and managing the agent fleet, runs, and deployments.

## Features

- **Fleet Dashboard** - Monitor nodes and active runs
- **Runs List** - View latest runs with status and duration
- **Agent Editor** - Create and manage agent definitions
- **OIDC Authentication** - Secure login via Keycloak

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

Edit `.env.local` to configure the API endpoint and authentication settings:

**Required Environment Variables:**

```bash
# API Configuration
NEXT_PUBLIC_API_URL=http://localhost:5000

# Authentication (Keycloak OIDC)
# IMPORTANT: These variables are required for authentication to work
AUTH_SECRET=your-secret-here-generate-with-openssl-rand-base64-32
AUTH_KEYCLOAK_ID=admin-ui
AUTH_KEYCLOAK_SECRET=your-keycloak-client-secret
AUTH_KEYCLOAK_ISSUER=http://localhost:8080/realms/bpa
```

> **Note:** The application will build without these variables set, but authentication will not work at runtime. Make sure to configure them before starting the development server.

**Generate AUTH_SECRET:**

```bash
openssl rand -base64 32
```

**Get Keycloak Client Secret:**

1. Start Keycloak using docker-compose (see root AUTHENTICATION.md)
2. Access http://localhost:8080 and login with admin/admin
3. Navigate to Clients → admin-ui → Credentials tab
4. Copy the client secret

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

## Authentication

The Admin UI uses NextAuth.js v5 (Auth.js) with Keycloak as the OIDC provider.

### Setup Keycloak for Development

1. **Start Keycloak and dependencies:**

```bash
# From the repository root
docker-compose -f docker-compose.dev.yml up -d
```

2. **Configure Keycloak:**

Access http://localhost:8080 and login with `admin` / `admin`

- Create realm: `bpa`
- Create client: `admin-ui`
  - Client Protocol: `openid-connect`
  - Enable "Client authentication"
  - Valid Redirect URIs: `http://localhost:3000/*`
  - Web Origins: `http://localhost:3000`
- Create test user (optional):
  - Username: `testuser`
  - Password: `testpass`

3. **Configure environment variables:**

Copy the client secret from Keycloak (Clients → admin-ui → Credentials) and add to `.env.local`

4. **Start the application:**

```bash
npm run dev
```

Visit http://localhost:3000 - you'll be redirected to the login page and then to Keycloak for authentication.

### Authentication Flow

1. User visits protected route → redirected to `/login`
2. Click "Sign in with Keycloak" → redirected to Keycloak
3. Enter credentials → Keycloak validates
4. Redirected back to app with session established
5. User menu appears in header with logout option

### Protected Routes

All routes except `/login` are protected by the auth middleware. Unauthenticated users are automatically redirected to the login page.

### Session Management

- Sessions are managed by NextAuth.js
- Access tokens and ID tokens are stored in the session
- Token refresh is handled automatically
- Logout clears the session and redirects to login page

## Project Structure

```
src/admin-ui/
├── src/
│   ├── app/                      # Next.js App Router pages
│   │   ├── api/auth/[...nextauth]/  # NextAuth.js API routes
│   │   ├── login/                # Login page
│   │   ├── layout.tsx            # Root layout with SessionProvider
│   │   ├── page.tsx              # Home page
│   │   └── globals.css           # Global styles with Tailwind and shadcn/ui
│   ├── components/               # React components
│   │   ├── ui/                   # shadcn/ui components
│   │   ├── header.tsx            # App header with user menu
│   │   └── user-menu.tsx         # User dropdown menu
│   ├── lib/                      # Utility libraries
│   │   └── utils.ts              # Tailwind merge utility
│   ├── types/                    # TypeScript type definitions
│   │   └── next-auth.d.ts        # NextAuth.js type extensions
│   ├── auth.ts                   # NextAuth.js configuration
│   └── middleware.ts             # Route protection middleware
├── public/                       # Static assets
├── components.json               # shadcn/ui configuration
├── tsconfig.json                 # TypeScript configuration
├── next.config.ts                # Next.js configuration
├── postcss.config.mjs            # PostCSS configuration
└── package.json                  # Dependencies and scripts
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

- [x] OIDC integration with Keycloak
- [x] Protected routes
- [x] Session management
- [x] Login/logout functionality

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
