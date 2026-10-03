# HappyHeadlines

Semester project for Development of Large Systems (E2026).

This repository contains a small, runnable implementation focused on the compulsory assignment about caching.

## What is implemented

- `ArticleService` – REST API for articles.
- `CommentService` – REST API for comments.
- PostgreSQL – persistence for articles and comments.
- Redis – separate cache for articles and comments.
- `ArticleCacheWarmer` – background process that refreshes global articles from the latest 14 days.
- Comment cache with a maximum of 30 article entries and LRU eviction.
- Prometheus metrics for cache hits and misses.
- Grafana dashboard showing cache hit ratios.
- NGINX load balancer in front of three ArticleService replicas.
- Docker Compose for the complete local environment.

The implementation intentionally keeps the solution small so the caching requirements can be demonstrated clearly.

## Architecture

```text
                         +----------------+
                         |     NGINX      |
                         | load balancer  |
                         +-------+--------+
                                 |
                    +------------+------------+
                    |            |            |
                    v            v            v
                 Article      Article      Article
                 Service #1   Service #2   Service #3
                    |            |            |
                    +------------+------------+
                                 |
                          +------v------+
                          | Article     |
                          | Redis       |
                          +------+------+
                                 |
                          +------v------+
                          | PostgreSQL  |
                          | Articles    |
                          +-------------+

Reader -> CommentService -> Comment Redis -> Comment PostgreSQL

                    +----------------------+
                    | Prometheus -> Grafana|
                    +----------------------+
```

## Run

Requirements:

- Docker Desktop
- Docker Compose

From the repository root:

```bash
docker compose up --build -d
```

Check containers:

```bash
docker compose ps
```

Useful URLs:

- Article API: `http://localhost:8080`
- Comment API: `http://localhost:8081`
- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3000`
- Grafana login: `admin` / `admin`

## Article cache

The ArticleCache is warmed in the background every 60 seconds.

Only global articles from the latest 14 days are loaded by the warmer.

Try:

```bash
curl.exe -i http://localhost:8080/api/articles/global
```

The response contains:

```text
X-Cache: HIT
```

or:

```text
X-Cache: MISS
```

The first request can be a miss. After the background warmer has run, recent global articles should be cached.

## Comment cache

Create an article first:

```bash
curl.exe -X POST http://localhost:8080/api/articles ^
  -H "Content-Type: application/json" ^
  -d "{\"title\":\"Happy news\",\"content\":\"A positive story\",\"publishedUtc\":\"2026-10-03T10:00:00Z\",\"region\":\"Global\"}"
```

Then use the returned article ID:

```bash
curl.exe -i http://localhost:8081/api/comments/article/{ARTICLE_ID}
```

The first request should normally be:

```text
X-Cache: MISS
```

Repeat it:

```bash
curl.exe -i http://localhost:8081/api/comments/article/{ARTICLE_ID}
```

The next request should be:

```text
X-Cache: HIT
```

## Metrics and dashboard

Prometheus scrapes `/metrics` from the services.

The following counters are exposed:

- `hh_cache_hits_total{cache="article"}`
- `hh_cache_misses_total{cache="article"}`
- `hh_cache_hits_total{cache="comment"}`
- `hh_cache_misses_total{cache="comment"}`

Grafana is provisioned automatically with the dashboard:

**HappyHeadlines - Cache Hit Ratio**

The dashboard shows:

- Article cache hit ratio
- Comment cache hit ratio
- Cache hits and misses over time

## Design decisions

### ArticleCache

The assignment requires an offline process to periodically fill the cache with articles from the latest 14 days.

`ArticleCacheWarmer` is implemented as a hosted background service. It runs every 60 seconds and refreshes the Redis cache.

### CommentCache

The comment cache uses cache-aside:

1. Check Redis.
2. On hit, return the cached comments.
3. On miss, read PostgreSQL.
4. Store the result in Redis.
5. Return the result.

The cache keeps at most 30 article IDs. A Redis sorted set stores the last access time. When more than 30 articles are cached, the oldest entry is removed.

### Failure behaviour

Redis is treated as an optimisation rather than the source of truth.

If Redis is unavailable:

- reads fall back to PostgreSQL;
- writes continue to PostgreSQL;
- the client receives the database result rather than a cache error.

This is a fail-open design.

## Scope

The repository is intentionally focused on the compulsory caching assignment. The previous lecture topics are represented in the architecture and documentation where relevant, but unnecessary implementation complexity has been avoided so the cache behaviour can be tested and presented reliably.
