# CafeMaestro Copilot Guidelines

## Project Overview
CafeMaestro is a cross-platform coffee roasting companion app built with .NET MAUI for .NET 9.  
GitHub repository: [CafeMaestro](https://github.com/sasler/CafeMaestro)

## Architecture
- Follow MVVM pattern with dependency injection.
- Business logic is located in `Services/`, UI in XAML/code-behind.
- Data models are defined in `Models/`.

## Naming Conventions
- **Classes/Methods/Properties**: PascalCase
- **Private Fields**: camelCase with underscore prefix (_fieldName)
- **Local Variables**: camelCase
- **File Names**: PascalCase (e.g., `LightTheme.xaml`, `DarkTheme.xaml`)

## Important Technical Guidelines

### UI
- Maintain a **consistent design** across screens.
- Use round `ImageButton` components where possible.
- Always use resource dictionary colors instead of hardcoded values:
  - XAML: `{StaticResource ColorName}`
  - C#: `Application.Current.Resources["ColorName"]`
  - Colors are defined in `LightTheme.xaml` and `DarkTheme.xaml`.
- Prioritize **accessibility**:
  - Support dynamic font scaling.
  - Ensure proper color contrast for readability.
  - Use responsive layouts for optimal experience across devices.

### SVG Image Handling
- .NET MAUI converts `.svg` files to `.png` at build time using `MauiImage` build actions.
- Always reference images with `.png` extension in code:
  - Project file: `<MauiImage Include="Resources\Images\logo.svg" />`
  - XAML: `<Image Source="logo.png" />`
  - C#: `ImageSource.FromFile("logo.png")`

### Data Persistence
- Use JSON for data storage via `AppDataService`.
- Store user preferences using `PreferencesService`.
- Implement validation and fallback mechanisms for read/write failures.

## Git Workflow
- Whenever starting work on a **GitHub issue**, create a **new Git branch** specific to that issue.
  - Branch naming convention: `issue-<number>-<short-description>` (e.g., `issue-42-fix-ui-layout`).
- **Do not create a commit until satisfied with the changes.** Ensure the code is stable, follows project guidelines, and is reviewed if needed.
- **Always update the CHANGELOG.md** when submitting a pull request:
  - Add your changes under the `[Unreleased]` section.
  - Follow the established format: Added/Changed/Fixed/Removed categories.
  - Be clear and concise about what was changed.
  - Example: `- Added version tracking functionality using .NET MAUI's built-in capabilities`
- For **commit messages and pull requests**, use [Gitmoji](https://gitmoji.dev/) for clarity and consistency.  
  - Example: `✨ Add new feature for roast tracking`
  - Helps visually categorize changes and improves commit readability.
- When working in **Agent mode**, check if there are **any outstanding issues or problems** before completing a task.
  - Address errors, warnings, or inconsistencies before finalizing the work.
- Ensure commits are meaningful and describe the changes clearly.

## Best Practices
- Follow **async/await** principles for efficient async operations.
- Use structured logging for debugging and monitoring.
- Apply proper error handling across data operations.

## Common Pitfalls
- **Hardcoding colors** instead of using resource dictionaries.
- **Referencing `.svg` directly** instead of the `.png` output.
- **Neglecting accessibility** (ensure UI elements work on all screen sizes).
- **Skipping validation** in data persistence—always handle potential failures.

## Domain Terms
- **First Crack**: When beans audibly crack during roasting.
- **Development Time**: Time between first crack and end of roast.
- **Weight Loss**: Percentage of weight lost during roasting (determines roast level).
- **Batch/Final Weight**: Before/after roasting weights.
