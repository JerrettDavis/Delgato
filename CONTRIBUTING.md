# Contributing to Delgato

Thank you for your interest in contributing to Delgato! This document provides guidelines and instructions for contributing.

## Code of Conduct

By participating in this project, you agree to abide by our Code of Conduct. Please be respectful and constructive in all interactions.

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Git
- Your favorite IDE (VS Code, Visual Studio, Rider)

### Setting Up Your Development Environment

1. **Fork the repository**
   ```bash
   # Click "Fork" on GitHub, then clone your fork
   git clone https://github.com/YOUR-USERNAME/delgato.git
   cd delgato
   ```

2. **Add the upstream remote**
   ```bash
   git remote add upstream https://github.com/your-org/delgato.git
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

4. **Run tests**
   ```bash
   dotnet test
   ```

## How to Contribute

### Reporting Bugs

- Use the [Bug Report template](https://github.com/your-org/delgato/issues/new?template=bug_report.yml)
- Search existing issues first to avoid duplicates
- Include reproduction steps, expected behavior, and actual behavior
- Include version information and environment details

### Suggesting Features

- Use the [Feature Request template](https://github.com/your-org/delgato/issues/new?template=feature_request.yml)
- Explain the problem you're trying to solve
- Describe your proposed solution
- Consider alternatives you've explored

### Submitting Code Changes

1. **Create a branch**
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/bug-description
   ```

2. **Make your changes**
   - Follow the coding standards (see below)
   - Write tests for new functionality
   - Update documentation as needed

3. **Run tests and formatting**
   ```bash
   dotnet test
   dotnet format
   ```

4. **Commit your changes**
   ```bash
   git commit -m "feat: add new feature description"
   # or
   git commit -m "fix: resolve bug description"
   ```

5. **Push and create a PR**
   ```bash
   git push origin feature/your-feature-name
   ```
   Then open a Pull Request on GitHub.

## Coding Standards

### C# Style Guide

- Use C# 12 features where appropriate
- Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful names for variables, methods, and classes
- Keep methods small and focused
- Use async/await for I/O operations

### Project Structure

```
Delgato.Core/           # Domain models and abstractions
Delgato.Providers/      # LLM provider implementations
Delgato.Orchestration/  # N-tree orchestration engine
Delgato.Governance/     # Audit, cost tracking, compliance
Delgato.Cli/            # Command-line interface
Delgato.Dashboard.Web/  # Blazor web dashboard
Delgato.Tests/          # Test projects
```

### Testing

We use TinyBDD-style tests. Follow this pattern:

```csharp
[Fact]
public void Provider_Should_Execute_Requests()
{
    // Given
    Given_A_Provider();
    Given_A_Valid_Request();

    // When
    When_The_Request_Is_Executed();

    // Then
    Assert.NotNull(_response);
    Assert.True(_response.IsSuccess);
}
```

### Commit Messages

We follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation changes
- `test:` - Test additions/modifications
- `refactor:` - Code refactoring
- `chore:` - Maintenance tasks

Examples:
```
feat: add support for Gemini provider
fix: resolve token counting in Claude provider
docs: update CLI reference documentation
test: add integration tests for orchestrator
```

## Pull Request Process

1. **Fill out the PR template** completely
2. **Ensure CI passes** - all tests and checks must pass
3. **Request review** from appropriate team members
4. **Address feedback** promptly and constructively
5. **Squash commits** if requested

### PR Checklist

- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] CHANGELOG updated (for significant changes)
- [ ] No breaking changes (or clearly documented)
- [ ] Code follows project style

## Documentation

- Update relevant documentation for any user-facing changes
- Use XML documentation comments for public APIs
- Keep README.md up to date

## Release Process

Releases are handled automatically through GitHub Actions when a version tag is pushed. Contributors don't need to manage releases.

## Getting Help

- **Questions**: Open a [Discussion](https://github.com/your-org/delgato/discussions)
- **Bugs**: Open an [Issue](https://github.com/your-org/delgato/issues)
- **Security**: See [SECURITY.md](SECURITY.md)

## Recognition

Contributors are recognized in:
- Release notes
- CONTRIBUTORS file (for significant contributions)
- Project documentation

Thank you for contributing to Delgato!
