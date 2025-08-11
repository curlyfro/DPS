# Claude Code Implementation Prompt: Document Processing Application

## Project Overview
You are building a comprehensive document processing application using Blazor, ASP.NET Core 8/9, Amazon Bedrock AI, and SQL Server. This is a complex enterprise application that requires systematic implementation with regular testing checkpoints.

## Critical Instructions for Claude Code

### Working Method
1. **Create and maintain a `PROGRESS_TRACKER.md` file** in the project root that you update after EVERY significant change
2. **Run the application at each milestone** to verify functionality
3. **Use a layered implementation approach** - complete each layer before moving to the next
4. **Commit to git after each milestone** with descriptive messages
5. **If you lose context**, read the PROGRESS_TRACKER.md file to regain focus

### Initial Setup Commands
```bash
# Create solution structure
dotnet new sln -n DocumentProcessor
dotnet new blazor -n DocumentProcessor.Web -o src/DocumentProcessor.Web --interactivity Auto
dotnet new classlib -n DocumentProcessor.Core -o src/DocumentProcessor.Core
dotnet new classlib -n DocumentProcessor.Infrastructure -o src/DocumentProcessor.Infrastructure
dotnet new classlib -n DocumentProcessor.Application -o src/DocumentProcessor.Application
dotnet new xunit -n DocumentProcessor.Tests -o tests/DocumentProcessor.Tests

# Add projects to solution
dotnet sln add src/DocumentProcessor.Web/DocumentProcessor.Web.csproj
dotnet sln add src/DocumentProcessor.Core/DocumentProcessor.Core.csproj
dotnet sln add src/DocumentProcessor.Infrastructure/DocumentProcessor.Infrastructure.csproj
dotnet sln add src/DocumentProcessor.Application/DocumentProcessor.Application.csproj
dotnet sln add tests/DocumentProcessor.Tests/DocumentProcessor.Tests.csproj

# Set up project references
dotnet add src/DocumentProcessor.Web reference src/DocumentProcessor.Application
dotnet add src/DocumentProcessor.Application reference src/DocumentProcessor.Core
dotnet add src/DocumentProcessor.Application reference src/DocumentProcessor.Infrastructure
dotnet add src/DocumentProcessor.Infrastructure reference src/DocumentProcessor.Core

# Initialize git
git init
echo "bin/" > .gitignore
echo "obj/" >> .gitignore
echo "*.user" >> .gitignore
echo ".vs/" >> .gitignore
git add .
git commit -m "Initial solution structure"
```

## PROGRESS_TRACKER.md Template

Create this file immediately and update it continuously:

```markdown
# Document Processing Application - Progress Tracker

## Current Status
**Current Layer:** [LAYER_NAME]
**Current Task:** [SPECIFIC_TASK]
**Last Milestone Completed:** [MILESTONE_NUMBER]
**Application Runs Successfully:** [YES/NO]
**Last Test Run:** [TIMESTAMP]

## Completed Layers
- [ ] Layer 1: Core Domain Models and Interfaces
- [ ] Layer 2: Database Setup and Migrations
- [ ] Layer 3: Repository Pattern Implementation
- [ ] Layer 4: Document Source Abstraction
- [ ] Layer 5: Basic File Upload (Local Storage)
- [ ] Layer 6: AI Integration Abstraction
- [ ] Layer 7: Amazon Bedrock Integration
- [ ] Layer 8: Background Processing
- [ ] Layer 9: Blazor UI Components
- [ ] Layer 10: Security and Authorization

## Detailed Progress

### Layer 1: Core Domain Models ✅/❌
- [ ] Document entity
- [ ] DocumentType entity
- [ ] Classification entity
- [ ] ProcessingQueue entity
- [ ] Core interfaces defined
- [ ] **MILESTONE 1:** Project builds successfully

### Layer 2: Database Setup ✅/❌
- [ ] ApplicationDbContext created
- [ ] Connection string configured
- [ ] Initial migration created
- [ ] Database created successfully
- [ ] **MILESTONE 2:** Database connects and migrations run

[Continue for all layers...]

## Issues Encountered
1. [Issue description] - [Resolution]

## Next Steps
1. [Immediate next task]
2. [Following task]

## Important Notes
- [Any critical information for resuming work]
```

## Implementation Layers with Milestones

### LAYER 1: Core Domain Models and Interfaces
**Goal:** Establish the foundation with domain entities and contracts

1. Create domain entities in `DocumentProcessor.Core/Entities/`:
   - Document.cs
   - DocumentType.cs
   - Classification.cs
   - ProcessingQueue.cs
   - DocumentMetadata.cs

2. Create interfaces in `DocumentProcessor.Core/Interfaces/`:
   - IDocumentRepository.cs
   - IDocumentProcessor.cs
   - IDocumentClassifier.cs
   - IDocumentSourceProvider.cs
   - IAIProcessor.cs

**MILESTONE 1:** Run `dotnet build` - Solution must compile without errors
```bash
dotnet build
# Expected: Build succeeded. 0 Warning(s), 0 Error(s)
```

### LAYER 2: Database Infrastructure
**Goal:** Set up SQL Server database with Entity Framework Core

1. Install required packages:
```bash
dotnet add src/DocumentProcessor.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/DocumentProcessor.Infrastructure package Microsoft.EntityFrameworkCore.Tools
dotnet add src/DocumentProcessor.Web package Microsoft.EntityFrameworkCore.Design
```

2. Create ApplicationDbContext with configurations
3. Add connection string to appsettings.json
4. Configure temporal tables and partitioning
5. Create initial migration

**MILESTONE 2:** Run migrations and verify database
```bash
dotnet ef migrations add InitialCreate -p src/DocumentProcessor.Infrastructure -s src/DocumentProcessor.Web
dotnet ef database update -p src/DocumentProcessor.Infrastructure -s src/DocumentProcessor.Web
dotnet run --project src/DocumentProcessor.Web
# Navigate to https://localhost:5001 - app should load
```

### LAYER 3: Repository Pattern Implementation
**Goal:** Implement data access layer with Unit of Work

1. Create generic repository in Infrastructure
2. Implement specific repositories
3. Create Unit of Work pattern
4. Register services in Program.cs

**MILESTONE 3:** Test repository operations
```bash
# Create a test endpoint or unit test
# Verify CRUD operations work
dotnet test
dotnet run --project src/DocumentProcessor.Web
```

### LAYER 4: Document Source Abstraction
**Goal:** Create abstracted document source providers

1. Implement IDocumentSourceProvider interface
2. Create LocalFileSystemProvider
3. Create MockS3Provider (for testing without AWS)
4. Create FileShareProvider
5. Implement strategy pattern for source selection

**MILESTONE 4:** Upload a test file through each provider
```bash
dotnet run --project src/DocumentProcessor.Web
# Test file upload from different sources
# Verify files are stored correctly
```

### LAYER 5: Basic File Upload UI
**Goal:** Create Blazor components for file upload

1. Create DocumentUpload.razor component
2. Implement drag-and-drop functionality
3. Add progress tracking
4. Create document list component
5. Add basic validation

**MILESTONE 5:** Upload files through UI
```bash
dotnet run --project src/DocumentProcessor.Web
# Upload various file types
# Verify progress bar works
# Check file appears in list
```

### LAYER 6: AI Integration Abstraction
**Goal:** Create AI processor abstraction layer

1. Define AI processor interfaces
2. Create mock AI processor for testing
3. Implement classification logic structure
4. Add intent detection framework
5. Create processing queue manager

**MILESTONE 6:** Test with mock AI processor
```bash
dotnet run --project src/DocumentProcessor.Web
# Upload document
# Verify mock classification works
# Check processing queue populates
```

### LAYER 7: Amazon Bedrock Integration
**Goal:** Integrate real AI processing with Amazon Bedrock

1. Install AWS SDK:
```bash
dotnet add src/DocumentProcessor.Infrastructure package AWSSDK.BedrockRuntime
dotnet add src/DocumentProcessor.Infrastructure package AWSSDK.Bedrock
```

2. Create BedrockDocumentProcessor
3. Implement Converse API integration
4. Add model selection logic
5. Implement cost optimization strategies

**MILESTONE 7:** Process document with Bedrock
```bash
# Configure AWS credentials
dotnet run --project src/DocumentProcessor.Web
# Upload document
# Verify AI classification occurs
# Check results in database
```

### LAYER 8: Background Processing
**Goal:** Implement async processing with channels

1. Create DocumentProcessingChannel
2. Implement BackgroundService
3. Add retry logic with Polly
4. Create processing status tracker
5. Implement SignalR for real-time updates

**MILESTONE 8:** Test background processing
```bash
dotnet run --project src/DocumentProcessor.Web
# Upload multiple documents
# Verify queue processing
# Check real-time status updates
```

### LAYER 9: Complete UI Implementation
**Goal:** Build full-featured Blazor interface

1. Create document type management UI
2. Add classification review interface
3. Implement search and filter
4. Create dashboard with statistics
5. Add document preview component

**MILESTONE 9:** Full UI walkthrough
```bash
dotnet run --project src/DocumentProcessor.Web
# Test all UI features
# Verify responsive design
# Check all CRUD operations
```

### LAYER 10: Security and Production Features
**Goal:** Add security, monitoring, and optimization

1. Implement authentication/authorization
2. Add input validation and sanitization
3. Configure logging with Serilog
4. Add health checks
5. Implement caching layer
6. Add performance monitoring

**MILESTONE 10:** Production readiness check
```bash
# Run security scan
dotnet run --project src/DocumentProcessor.Web
# Test authorization
# Check health endpoints: /health
# Verify logging works
# Run load test
```

## Testing Strategy at Each Layer

After each layer completion:
1. **Unit Tests:** Write tests for new functionality
2. **Integration Tests:** Test component interactions
3. **Manual Testing:** Run application and verify features
4. **Update Progress Tracker:** Document completion and issues

## Recovery Instructions

If you lose context or need to resume:
1. Read `PROGRESS_TRACKER.md` to understand current state
2. Run `git status` to see uncommitted changes
3. Run `dotnet build` to check for compilation errors
4. Run `dotnet test` to verify test status
5. Check the "Next Steps" section in tracker
6. Continue from last incomplete task

## Critical Success Patterns

1. **Always update PROGRESS_TRACKER.md** before moving to next task
2. **Commit working code** after each successful milestone
3. **Run the application** at every milestone - don't just compile
4. **Create simple test data** to verify functionality
5. **Document any deviations** from the plan in the tracker
6. **If stuck**, create a minimal working version first, then enhance

## Final Validation Checklist

Before considering the project complete:
- [ ] All 10 layers implemented
- [ ] All milestones pass
- [ ] Application runs without errors
- [ ] Can upload document from all sources
- [ ] AI classification works
- [ ] Background processing functions
- [ ] UI is responsive and functional
- [ ] Security measures in place
- [ ] All tests pass
- [ ] Documentation is complete

## Start Implementation

Begin with Layer 1 and systematically work through each layer. Remember to:
1. Update progress tracker continuously
2. Test at each milestone
3. Commit working code frequently
4. Document any issues or changes

Good luck! Start by creating the PROGRESS_TRACKER.md file, then begin Layer 1.