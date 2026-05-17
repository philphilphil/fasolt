.PHONY: dev deploy test down logs bump ios ios-clean ios-archive

IOS_SIMULATOR ?= iPhone 17 Pro
IOS_BUNDLE_ID := com.fasolt.app
IOS_DERIVED   := fasolt.ios/build
IOS_APP       := $(IOS_DERIVED)/Build/Products/Debug-iphonesimulator/Fasolt.app

dev:
	./scripts/dev.sh

ios-run:
	@xcrun simctl boot "$(IOS_SIMULATOR)" 2>/dev/null || true
	@open -a Simulator
	xcodebuild -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -configuration Debug \
		-destination 'platform=iOS Simulator,name=$(IOS_SIMULATOR)' \
		-derivedDataPath $(IOS_DERIVED) build
	xcrun simctl install "$(IOS_SIMULATOR)" "$(IOS_APP)"
	@xcrun simctl terminate "$(IOS_SIMULATOR)" $(IOS_BUNDLE_ID) 2>/dev/null || true
	xcrun simctl launch "$(IOS_SIMULATOR)" $(IOS_BUNDLE_ID)

ios-clean:
	rm -rf $(IOS_DERIVED)

ios-archive:
	$(eval IOS_ARCHIVE_DIR := $(HOME)/Library/Developer/Xcode/Archives/$(shell date +%Y-%m-%d))
	$(eval IOS_ARCHIVE := $(IOS_ARCHIVE_DIR)/Fasolt-$(shell date +%H%M%S).xcarchive)
	@mkdir -p "$(IOS_ARCHIVE_DIR)"
	xcodebuild -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -configuration Release \
		-destination 'generic/platform=iOS' \
		-archivePath "$(IOS_ARCHIVE)" archive
	open "$(IOS_ARCHIVE)"

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
	@git commit -am "Bump version to v$(VERSION)"
	@git tag -a v$(VERSION) -m "v$(VERSION)"
	@git push --follow-tags
	@echo "Bumped to $(VERSION) and pushed."
