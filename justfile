# Justfile for running CI checks locally

# Show all available commands
default:
    @just --list

# Run all gate pipeline checks
gate: check bazel
    @echo "✓ All gate checks passed!"

# Run check pipeline (pre-commit + go linters)
check:
    @pre-commit run --all-files --color always --show-diff-on-failure
    @golangci-lint run --timeout 5m ./...
    @go vet ./...

# Run bazel build and test
bazel:
    bazelisk build //...
    bazelisk test //... --test_output=errors

# Run bazel build only
build:
    bazelisk build //...

# Run bazel test only
test:
    bazelisk test //... --test_output=errors

# Run pre-commit on all files
lint:
    pre-commit run --all-files --color always --show-diff-on-failure

# Run Go linters
lint-go:
    golangci-lint run --timeout 5m ./...
    go vet ./...

# Auto-fix formatting issues
fix:
    @echo "→ Running clang-format..."
    @find . -type f \( -name "*.cc" -o -name "*.h" -o -name "*.cpp" -o -name "*.hpp" \) -exec clang-format -i {} +
    @echo "→ Running go fmt..."
    @go fmt ./...
    @echo "→ Running goimports..."
    @goimports -w .
    @echo "→ Running nixpkgs-fmt..."
    @nixpkgs-fmt .
    @echo "→ Running shfmt..."
    @find . -type f -name "*.sh" -exec shfmt -i 4 -s -w {} +
    @echo "✓ All formatting fixed!"

# Clean bazel cache
clean:
    bazelisk clean --expunge

# Install pre-commit hooks
install:
    pre-commit install
