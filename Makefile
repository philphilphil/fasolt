.PHONY: dev deploy test down logs bump

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

bump:
	@if [ -z "$(VERSION)" ]; then echo "Usage: make bump VERSION=0.1.3"; exit 1; fi
	@sed -i.bak -E 's|<Version>[^<]+</Version>|<Version>$(VERSION)</Version>|' fasolt.Server/fasolt.Server.csproj && rm fasolt.Server/fasolt.Server.csproj.bak
	@cd fasolt.client && npm version --no-git-tag-version $(VERSION) > /dev/null
	@echo "Bumped to $(VERSION). Next: git commit -am 'Bump version to v$(VERSION)' && git tag v$(VERSION) && git push --follow-tags"
