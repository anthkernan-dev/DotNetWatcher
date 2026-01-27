# Contributing to DotNetWatcher

Thank you for your interest in contributing to DotNetWatcher! We welcome contributions from the community.

## How to Contribute

### Reporting Bugs

If you find a bug, please create an issue with:
- A clear, descriptive title
- Steps to reproduce the behavior
- Expected vs actual behavior
- Screenshots if applicable
- Your environment (.NET version, Windows version)

### Suggesting Features

Feature requests are welcome! Please create an issue describing:
- The problem you're trying to solve
- Your proposed solution
- Any alternatives you've considered

### Pull Requests

1. **Fork the repository** and create your branch from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes**:
   - Follow existing code style and conventions
   - Keep changes focused and atomic
   - Add tests if applicable

3. **Test your changes**:
   ```bash
   dotnet build
   dotnet test
   ```

4. **Commit your changes**:
   - Write clear, descriptive commit messages
   - Reference issues if applicable (e.g., "Fixes #123")

5. **Push to your fork** and submit a pull request:
   ```bash
   git push origin feature/your-feature-name
   ```

6. **Wait for review**:
   - Address any feedback from maintainers
   - Keep your branch up to date with `main`

### Development Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/anthkernan-dev/DotNetWatcher.git
   cd DotNetWatcher
   ```

2. Install .NET 10 SDK or later

3. Build the solution:
   ```bash
   dotnet build
   ```

4. Run tests:
   ```bash
   dotnet test
   ```

5. Run the application:
   ```bash
   dotnet run --project DotNetWatcher
   ```

### Code Style

- Follow standard C# conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and concise

### Testing

- Add unit tests for new features
- Ensure all existing tests pass
- Aim for good test coverage on core logic

### Documentation

- Update README.md if adding user-facing features
- Add XML comments for public APIs
- Update inline comments for complex logic

## Code of Conduct

This project adheres to a Code of Conduct that all contributors are expected to follow. Please read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) before contributing.

## Questions?

Feel free to create an issue for any questions about contributing!

## License

By contributing to DotNetWatcher, you agree that your contributions will be licensed under the MIT License.
