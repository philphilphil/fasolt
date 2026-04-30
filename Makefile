.PHONY: dev deploy test down logs

dev:
	./scripts/dev.sh

deploy:
	git pull && docker compose -f docker-compose.prod.yml up -d --build

test:
	dotnet test

down:
	docker compose down

logs:
	docker compose logs -f
