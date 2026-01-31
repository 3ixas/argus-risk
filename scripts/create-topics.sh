#!/bin/bash
# Creates Kafka topics in Redpanda for Argus Risk

set -e

echo "Creating Kafka topics..."

# Wait for Redpanda to be ready
until docker exec argus-redpanda rpk cluster health | grep -q "Healthy:.*true"; do
  echo "Waiting for Redpanda to be healthy..."
  sleep 2
done

# Create topics with appropriate partition counts
# More partitions = more parallelism, but also more overhead
docker exec argus-redpanda rpk topic create market-data.prices \
  --partitions 6 \
  --topic-config retention.ms=86400000 \
  --topic-config cleanup.policy=delete \
  || echo "Topic market-data.prices may already exist"

docker exec argus-redpanda rpk topic create market-data.fx \
  --partitions 3 \
  --topic-config retention.ms=86400000 \
  --topic-config cleanup.policy=delete \
  || echo "Topic market-data.fx may already exist"

docker exec argus-redpanda rpk topic create trades.inbound \
  --partitions 6 \
  --topic-config retention.ms=604800000 \
  --topic-config cleanup.policy=delete \
  || echo "Topic trades.inbound may already exist"

docker exec argus-redpanda rpk topic create risk.snapshots \
  --partitions 3 \
  --topic-config retention.ms=86400000 \
  --topic-config cleanup.policy=compact \
  || echo "Topic risk.snapshots may already exist"

echo ""
echo "Topics created successfully:"
docker exec argus-redpanda rpk topic list

echo ""
echo "Done! You can view topics at http://localhost:8080"
