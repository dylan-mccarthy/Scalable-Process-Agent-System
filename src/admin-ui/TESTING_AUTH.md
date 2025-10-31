# Testing OIDC Authentication with Keycloak

This guide walks through testing the OIDC authentication integration locally.

## Prerequisites

- Docker installed and running
- Node.js 18+ installed
- Admin UI environment configured

## Step 1: Start Keycloak

From the repository root:

```bash
docker compose -f docker-compose.dev.yml up -d postgres
# Wait 5 seconds for postgres to initialize
docker exec bpa-postgres psql -U postgres -c "CREATE DATABASE keycloak;"
docker compose -f docker-compose.dev.yml up -d keycloak
```

Wait ~30 seconds for Keycloak to fully start.

## Step 2: Configure Keycloak

1. **Access Keycloak Admin Console:**
   - URL: http://localhost:8080
   - Username: `admin`
   - Password: `admin`

2. **Create Realm:**
   - Click dropdown in top-left (should say "master")
   - Click "Create Realm"
   - Name: `bpa`
   - Click "Create"

3. **Create Client:**
   - In the `bpa` realm, go to "Clients"
   - Click "Create client"
   - **General Settings:**
     - Client type: `OpenID Connect`
     - Client ID: `admin-ui`
     - Click "Next"
   - **Capability config:**
     - Client authentication: `ON`
     - Click "Next"
   - **Login settings:**
     - Valid redirect URIs: `http://localhost:3000/*`
     - Valid post logout redirect URIs: `http://localhost:3000/*`
     - Web origins: `http://localhost:3000`
     - Click "Save"

4. **Get Client Secret:**
   - Go to "Credentials" tab
   - Copy the "Client secret" value

5. **Create Test User (Optional):**
   - Go to "Users" → "Add user"
   - Username: `testuser`
   - Email: `test@example.com`
   - First name: `Test`
   - Last name: `User`
   - Click "Create"
   - Go to "Credentials" tab
   - Click "Set password"
   - Password: `testpass`
   - Temporary: `OFF`
   - Click "Save"

## Step 3: Configure Admin UI

1. **Create environment file:**

```bash
cd src/admin-ui
cp .env.example .env.local
```

2. **Generate AUTH_SECRET:**

```bash
openssl rand -base64 32
```

3. **Edit `.env.local`:**

```bash
# API Configuration
NEXT_PUBLIC_API_URL=http://localhost:5000

# Authentication (Keycloak OIDC)
AUTH_SECRET=<generated-secret-from-step-2>
AUTH_KEYCLOAK_ID=admin-ui
AUTH_KEYCLOAK_SECRET=<client-secret-from-keycloak>
AUTH_KEYCLOAK_ISSUER=http://localhost:8080/realms/bpa
```

## Step 4: Start Admin UI

```bash
cd src/admin-ui
npm install  # if not already done
npm run dev
```

## Step 5: Test Authentication Flow

1. **Visit the application:**
   - Open http://localhost:3000 in your browser
   - You should be redirected to `/login`

2. **Click "Sign in with Keycloak":**
   - You'll be redirected to Keycloak login page
   - URL will be like: `http://localhost:8080/realms/bpa/protocol/openid-connect/auth?...`

3. **Login with credentials:**
   - Username: `testuser`
   - Password: `testpass`
   - Click "Sign In"

4. **Verify authentication:**
   - You should be redirected back to http://localhost:3000
   - You should see the home page
   - A user menu icon should appear in the top-right header
   - Click the user icon to see your name/email and logout option

5. **Test logout:**
   - Click the user icon
   - Click "Sign out"
   - You should be redirected back to `/login`

## Expected Results

### Successful Authentication Flow

1. ✅ Unauthenticated access to `/` redirects to `/login`
2. ✅ Click "Sign in with Keycloak" redirects to Keycloak
3. ✅ Valid credentials authenticate successfully
4. ✅ Redirect back to `/` with active session
5. ✅ User menu displays with user information
6. ✅ Logout clears session and redirects to `/login`

### Protected Routes

- All routes except `/login` require authentication
- Attempting to access any protected route without authentication redirects to `/login`
- After successful authentication, users can access all routes

## Troubleshooting

### "Configuration error" on login

- Verify `AUTH_KEYCLOAK_ID` matches the client ID in Keycloak
- Verify `AUTH_KEYCLOAK_SECRET` matches the client secret
- Verify `AUTH_KEYCLOAK_ISSUER` is correct (should be `http://localhost:8080/realms/bpa`)

### Redirect loop

- Check that valid redirect URIs are configured in Keycloak client
- Verify `AUTH_SECRET` is set and valid
- Clear browser cookies and try again

### "Invalid redirect uri" error

- Ensure `http://localhost:3000/*` is in the valid redirect URIs list in Keycloak client
- Verify Web Origins includes `http://localhost:3000`

### Token validation errors

- Ensure Keycloak is running and accessible at http://localhost:8080
- Verify the realm name is `bpa` (case-sensitive)
- Check Keycloak logs: `docker logs bpa-keycloak`

## Security Notes

- `AUTH_SECRET` should be generated with `openssl rand -base64 32`
- Never commit `.env.local` to version control
- In production, use HTTPS for all endpoints
- Use environment-specific secrets for different environments
- Rotate client secrets regularly

## Next Steps

After verifying authentication works:

1. Implement role-based access control (RBAC)
2. Add Keycloak user/role management
3. Integrate access tokens with Control Plane API calls
4. Add token refresh handling
5. Configure production Entra ID provider
