#!/bin/bash
# Certificate generation script for mTLS development and testing
# This script generates a Certificate Authority (CA) and certificates for the Control Plane server and Node Runtime client

set -e

# Configuration
CERTS_DIR="${CERTS_DIR:-./certs}"
VALIDITY_DAYS="${VALIDITY_DAYS:-365}"
CA_SUBJECT="${CA_SUBJECT:-/CN=BPA-CA/O=Business Process Agents/C=US}"
SERVER_SUBJECT="${SERVER_SUBJECT:-/CN=control-plane/O=Business Process Agents/C=US}"
CLIENT_SUBJECT="${CLIENT_SUBJECT:-/CN=node-runtime/O=Business Process Agents/C=US}"

echo "==================================================="
echo "Generating mTLS Certificates for BPA"
echo "==================================================="
echo "Output directory: $CERTS_DIR"
echo "Validity: $VALIDITY_DAYS days"
echo ""

# Create certificates directory
mkdir -p "$CERTS_DIR"

# Generate CA private key
echo "[1/7] Generating CA private key..."
openssl genrsa -out "$CERTS_DIR/ca-key.pem" 4096

# Generate CA certificate
echo "[2/7] Generating CA certificate..."
openssl req -new -x509 -days "$VALIDITY_DAYS" \
    -key "$CERTS_DIR/ca-key.pem" \
    -out "$CERTS_DIR/ca-cert.pem" \
    -subj "$CA_SUBJECT"

# Generate server private key
echo "[3/7] Generating server private key..."
openssl genrsa -out "$CERTS_DIR/server-key.pem" 4096

# Generate server certificate signing request (CSR)
echo "[4/7] Generating server CSR..."
openssl req -new \
    -key "$CERTS_DIR/server-key.pem" \
    -out "$CERTS_DIR/server.csr" \
    -subj "$SERVER_SUBJECT"

# Sign server certificate with CA
echo "[5/7] Signing server certificate..."
openssl x509 -req -days "$VALIDITY_DAYS" \
    -in "$CERTS_DIR/server.csr" \
    -CA "$CERTS_DIR/ca-cert.pem" \
    -CAkey "$CERTS_DIR/ca-key.pem" \
    -CAcreateserial \
    -out "$CERTS_DIR/server-cert.pem" \
    -extfile <(printf "subjectAltName=DNS:localhost,DNS:control-plane,DNS:control-plane.bpa.svc.cluster.local,IP:127.0.0.1")

# Generate client private key
echo "[6/7] Generating client private key..."
openssl genrsa -out "$CERTS_DIR/node-key.pem" 4096

# Generate client certificate signing request (CSR)
echo "[7/7] Generating client CSR and signing..."
openssl req -new \
    -key "$CERTS_DIR/node-key.pem" \
    -out "$CERTS_DIR/node.csr" \
    -subj "$CLIENT_SUBJECT"

# Sign client certificate with CA
openssl x509 -req -days "$VALIDITY_DAYS" \
    -in "$CERTS_DIR/node.csr" \
    -CA "$CERTS_DIR/ca-cert.pem" \
    -CAkey "$CERTS_DIR/ca-key.pem" \
    -CAcreateserial \
    -out "$CERTS_DIR/node-cert.pem"

# Clean up CSR files
rm "$CERTS_DIR/server.csr" "$CERTS_DIR/node.csr"

echo ""
echo "==================================================="
echo "Certificate generation completed successfully!"
echo "==================================================="
echo ""
echo "Generated files:"
echo "  CA Certificate:      $CERTS_DIR/ca-cert.pem"
echo "  CA Private Key:      $CERTS_DIR/ca-key.pem (KEEP SECURE)"
echo "  Server Certificate:  $CERTS_DIR/server-cert.pem"
echo "  Server Private Key:  $CERTS_DIR/server-key.pem (KEEP SECURE)"
echo "  Client Certificate:  $CERTS_DIR/node-cert.pem"
echo "  Client Private Key:  $CERTS_DIR/node-key.pem (KEEP SECURE)"
echo ""
echo "Certificate verification:"
openssl x509 -in "$CERTS_DIR/ca-cert.pem" -noout -subject -issuer -dates
echo ""
openssl x509 -in "$CERTS_DIR/server-cert.pem" -noout -subject -issuer -dates
echo ""
openssl x509 -in "$CERTS_DIR/node-cert.pem" -noout -subject -issuer -dates
echo ""
echo "To use these certificates, update your configuration:"
echo ""
echo "ControlPlane.Api appsettings.json:"
echo '  "MTls": {'
echo '    "Enabled": true,'
echo "    \"ServerCertificatePath\": \"$CERTS_DIR/server-cert.pem\","
echo "    \"ServerKeyPath\": \"$CERTS_DIR/server-key.pem\","
echo "    \"ClientCaCertificatePath\": \"$CERTS_DIR/ca-cert.pem\","
echo '    "RequireClientCertificate": true'
echo '  }'
echo ""
echo "Node.Runtime appsettings.json:"
echo '  "MTls": {'
echo '    "Enabled": true,'
echo "    \"ClientCertificatePath\": \"$CERTS_DIR/node-cert.pem\","
echo "    \"ClientKeyPath\": \"$CERTS_DIR/node-key.pem\","
echo "    \"ServerCaCertificatePath\": \"$CERTS_DIR/ca-cert.pem\","
echo '    "ExpectedServerCertificateSubject": "control-plane"'
echo '  }'
echo ""
echo "IMPORTANT: Keep private keys secure and never commit them to version control!"
echo "==================================================="
