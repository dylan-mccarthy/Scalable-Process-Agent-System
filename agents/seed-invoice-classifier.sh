#!/bin/bash

# Seed Invoice Classifier Agent
# This script registers the Invoice Classifier agent with the Control Plane API

set -e

# Configuration
CONTROL_PLANE_URL="${CONTROL_PLANE_URL:-http://localhost:5109}"
AGENT_DEFINITION_FILE="$(dirname "$0")/definitions/invoice-classifier.json"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Seeding Invoice Classifier Agent...${NC}"

# Check if the agent definition file exists
if [ ! -f "$AGENT_DEFINITION_FILE" ]; then
    echo -e "${RED}Error: Agent definition file not found: $AGENT_DEFINITION_FILE${NC}"
    exit 1
fi

# Read the agent definition
AGENT_DEF=$(cat "$AGENT_DEFINITION_FILE")

# Extract agent properties for creation
AGENT_NAME=$(echo "$AGENT_DEF" | jq -r '.name')
AGENT_DESC=$(echo "$AGENT_DEF" | jq -r '.description')
AGENT_INSTRUCTIONS=$(echo "$AGENT_DEF" | jq -r '.instructions')
AGENT_MODEL_PROFILE=$(echo "$AGENT_DEF" | jq '.modelProfile')
AGENT_BUDGET=$(echo "$AGENT_DEF" | jq '.budget')
AGENT_TOOLS=$(echo "$AGENT_DEF" | jq '.tools')
AGENT_INPUT=$(echo "$AGENT_DEF" | jq '.input')
AGENT_OUTPUT=$(echo "$AGENT_DEF" | jq '.output')
AGENT_METADATA=$(echo "$AGENT_DEF" | jq '.metadata')

# Create the agent
echo -e "\n${YELLOW}Step 1: Creating agent...${NC}"
CREATE_AGENT_RESPONSE=$(curl -s -X POST "${CONTROL_PLANE_URL}/v1/agents" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": ${AGENT_NAME},
    \"description\": ${AGENT_DESC},
    \"instructions\": ${AGENT_INSTRUCTIONS},
    \"modelProfile\": ${AGENT_MODEL_PROFILE},
    \"budget\": ${AGENT_BUDGET},
    \"tools\": ${AGENT_TOOLS},
    \"input\": ${AGENT_INPUT},
    \"output\": ${AGENT_OUTPUT},
    \"metadata\": ${AGENT_METADATA}
  }")

# Extract agent ID
AGENT_ID=$(echo "$CREATE_AGENT_RESPONSE" | jq -r '.agentId')

if [ "$AGENT_ID" == "null" ] || [ -z "$AGENT_ID" ]; then
    echo -e "${RED}Error: Failed to create agent${NC}"
    echo "Response: $CREATE_AGENT_RESPONSE"
    exit 1
fi

echo -e "${GREEN}✓ Agent created successfully with ID: $AGENT_ID${NC}"

# Create the first version (1.0.0)
echo -e "\n${YELLOW}Step 2: Creating agent version 1.0.0...${NC}"
VERSION_RESPONSE=$(curl -s -X POST "${CONTROL_PLANE_URL}/v1/agents/${AGENT_ID}:version" \
  -H "Content-Type: application/json" \
  -d "{
    \"version\": \"1.0.0\",
    \"spec\": $(echo "$AGENT_DEF" | jq 'del(.agentId) | .agentId = "'$AGENT_ID'"')
  }")

VERSION=$(echo "$VERSION_RESPONSE" | jq -r '.version')

if [ "$VERSION" == "null" ] || [ -z "$VERSION" ]; then
    echo -e "${RED}Error: Failed to create agent version${NC}"
    echo "Response: $VERSION_RESPONSE"
    exit 1
fi

echo -e "${GREEN}✓ Agent version $VERSION created successfully${NC}"

# Summary
echo -e "\n${GREEN}========================================${NC}"
echo -e "${GREEN}Invoice Classifier Agent Seeded Successfully!${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "Agent ID: ${AGENT_ID}"
echo -e "Version: ${VERSION}"
echo -e "Name: ${AGENT_NAME}"
echo -e "\nNext steps:"
echo -e "1. Create a deployment: POST ${CONTROL_PLANE_URL}/v1/deployments"
echo -e "2. Configure environment variables:"
echo -e "   - SERVICE_BUS_CONNECTION_STRING"
echo -e "   - INVOICE_API_ENDPOINT"
echo -e "   - INVOICE_API_KEY"
echo -e "3. Start a Node Runtime to execute the agent"
echo -e "\nView agent details:"
echo -e "curl ${CONTROL_PLANE_URL}/v1/agents/${AGENT_ID}"
