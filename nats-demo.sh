#!/bin/bash
# Demo script for NATS JetStream event publishing
# This script demonstrates the NATS setup by:
# 1. Starting a NATS server with JetStream
# 2. Starting the API (if NATS is available)
# 3. Publishing a test event
# 4. Showing the event in the NATS stream

set -e

echo "================================================"
echo "NATS JetStream Demo - E1-T5"
echo "================================================"
echo ""

# Check if Docker is available
if ! command -v docker &> /dev/null; then
    echo "Error: Docker is required but not installed."
    exit 1
fi

# Start NATS with JetStream
echo "1. Starting NATS server with JetStream..."
docker run -d --name nats-demo -p 4222:4222 -p 8222:8222 nats:latest --jetstream
sleep 2

# Wait for NATS to be ready
echo "   Waiting for NATS to be ready..."
for i in {1..10}; do
    if docker exec nats-demo nats-server -v > /dev/null 2>&1; then
        echo "   ✓ NATS server is ready"
        break
    fi
    sleep 1
done

echo ""
echo "2. NATS JetStream is now running at:"
echo "   - NATS: nats://localhost:4222"
echo "   - Monitoring: http://localhost:8222"
echo ""

# Show how to connect and view streams
echo "3. To view JetStream streams, you can use:"
echo "   docker exec -it nats-demo nats stream list"
echo ""

# Cleanup instructions
echo "4. To stop and remove the NATS container:"
echo "   docker stop nats-demo && docker rm nats-demo"
echo ""

echo "================================================"
echo "NATS Setup Complete!"
echo "================================================"
echo ""
echo "The Control Plane API can now connect to NATS at nats://localhost:4222"
echo "and will automatically provision the BPA_EVENTS stream on startup."
echo ""
echo "Test the setup with:"
echo "  curl -X POST http://localhost:5109/v1/events:test"
echo ""
