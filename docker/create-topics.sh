#!/bin/bash
# Creates Kafka topics in Redpanda (runs inside a Redpanda container)
# Unlike scripts/create-topics.sh, this uses rpk directly (no docker exec)

set -e

BROKER="${KAFKA_BROKERS:-redpanda:9092}"

echo "Creating Kafka topics on ${BROKER}..."

rpk topic create market-data.prices \
  --brokers "$BROKER" \
  --partitions 6 \
  --topic-config retention.ms=86400000 \
  --topic-config cleanup.policy=delete \
  || echo "Topic market-data.prices may already exist"

rpk topic create market-data.fx \
  --brokers "$BROKER" \
  --partitions 3 \
  --topic-config retention.ms=86400000 \
  --topic-config cleanup.policy=delete \
  || echo "Topic market-data.fx may already exist"

rpk topic create trades.inbound \
  --brokers "$BROKER" \
  --partitions 6 \
  --topic-config retention.ms=604800000 \
  --topic-config cleanup.policy=delete \
  || echo "Topic trades.inbound may already exist"

rpk topic create risk.snapshots \
  --brokers "$BROKER" \
  --partitions 3 \
  --topic-config retention.ms=86400000 \
  --topic-config cleanup.policy=compact \
  || echo "Topic risk.snapshots may already exist"

echo ""
echo "Topics created:"
rpk topic list --brokers "$BROKER"
