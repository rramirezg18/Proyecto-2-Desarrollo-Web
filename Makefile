SHELL := /bin/bash

.PHONY: help hooks lint baseline dev-up dev-down env redis-ping clean
.DEFAULT_GOAL := help

help:
	@echo "Targets:"
	@echo "  make hooks       - Instala/actualiza pre-commit en este repo"
	@echo "  make lint        - Ejecuta pre-commit sobre todo el repo"
	@echo "  make baseline    - (Re)genera .secrets.baseline en la raíz"
	@echo "  make env         - Copia .env.example -> .env en report-service (si no existe)"
	@echo "  make dev-up      - Levanta Redis + contenedor placeholder del report-service"
	@echo "  make dev-down    - Detiene y limpia los contenedores de dev"
	@echo "  make redis-ping  - PING a Redis (debe responder PONG)"

hooks:
	pre-commit uninstall || true
	pre-commit install -f

lint:
	pre-commit run -a

baseline:
	detect-secrets scan > .secrets.baseline
	git add .secrets.baseline
	@echo "Baseline actualizado. Recuerda commitearlo si cambió."

env:
	@test -f report-service/.env || cp report-service/.env.example report-service/.env
	@echo "report-service/.env listo (revisa y ajusta variables)."

dev-up:
	docker compose -f report-service/ops/compose.dev.yml up -d

dev-down:
	docker compose -f report-service/ops/compose.dev.yml down --remove-orphans

redis-ping:
	docker exec -it redis redis-cli PING || echo "Redis no está arriba; corre 'make dev-up'"

clean:
	find . -type d -name "__pycache__" -prune -exec rm -rf {} +
	find . -type f -name "*.py[co]" -delete
smoke:
	bash report-service/scripts/smoke.sh
