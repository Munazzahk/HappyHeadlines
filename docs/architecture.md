# Architecture notes

## Context

HappyHeadlines is a global positive-news platform. Publishers create content and readers read articles and post comments.

## Containers relevant to this assignment

```text
Reader
  |
  v
NGINX
  |
  +--> ArticleService #1 ----+
  +--> ArticleService #2 ----+--> ArticleCache --> ArticleDatabase
  +--> ArticleService #3 ----+
  |
  +--> CommentService --> CommentCache --> CommentDatabase

ArticleCache --> background warmer --> latest 14 days of global articles

CommentCache --> cache-aside + LRU --> maximum 30 article entries

Prometheus --> /metrics --> Grafana
```

## Why Redis?

Redis is used because it provides fast key/value access, TTL support and sorted sets. A sorted set is useful for the comment cache because the score can represent the last access time.

## Why two Redis instances?

The article and comment caches are independent concerns. Separate instances make the demonstration and monitoring clearer and provide basic fault isolation between the cache layers.

## Cache patterns

Article cache:

- background refresh
- latest 14 days
- 30 minute TTL safety net

Comment cache:

- cache-aside
- cache miss loads PostgreSQL
- maximum 30 articles
- LRU based on last access timestamp

## Monitoring

Both cache implementations increment hit/miss Prometheus counters. Grafana calculates the hit ratio from those counters.

Hit ratio:

`hits / (hits + misses)`

## Relation to the course

The project demonstrates:

- horizontal replicas of a stateless service;
- separation of service and database responsibilities;
- caching as an architectural scalability pattern;
- monitoring of cache behaviour;
- a distributed-system trade-off: Redis improves latency but introduces another component.
