# HappyHeadlines

Project startup instructions in visual code:

```bash
docker compose up -d 
```

To start over from scratch:

```bash
docker compose down -v
docker compose up -d 
```

## Services

* ArticleService (load balanced): http://localhost:8080
* CommentService: http://localhost:8081
* Prometheus: http://localhost:9090
* Grafana: http://localhost:3000

Grafana login:

```text
admin / admin
```

Sometimes the Grafana password needs to be changed 

## Caching

* ArticleCache: Redis cache between ArticleService and ArticleDatabase.
* Article cache is periodically filled with global articles from the latest 14 days.
* CommentCache: Redis cache between CommentService and CommentDatabase.
* Comment cache uses cache-aside and keeps the 30 most recently accessed articles using LRU.
* Cache hits and misses are exposed through Prometheus metrics.
* Grafana shows the cache hit ratios.

## Cache check

Article cache:

```bash
curl.exe -i http://localhost:8080/api/articles/global
```

Article requests can be inspected using the `X-Cache` response header.

Comment cache:

```bash
curl.exe -i http://localhost:8081/api/comments/article/{ARTICLE_ID}
```

The first request should normally be a `MISS`. Repeating the request should give a `HIT`.

## Documentation

* `docs/architecture.md` – architecture and design decisions
* `monitoring/grafana/dashboards/happyheadlines-cache.json` – Grafana cache dashboard


# Used commands for demo 
All articles: curl.exe http://localhost:8080/api/articles/global

Specific article: curl.exe http://localhost:8080/api/articles/article/{ARTICLE_ID}

Check article cache: docker compose exec article-cache redis-cli KEYS "*"

Comment check: curl.exe -i http://localhost:8081/api/comments/article/b4657958-a922-4d39-a216-a0be17e46671

Check comment cache: docker compose exec comment-cache redis-cli KEYS "*"