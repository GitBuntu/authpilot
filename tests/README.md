# AuthPilot Test Suite

This directory contains comprehensive tests for the AuthPilot prior authorization processing system.

## Test Overview

**Total Tests:** 36 tests across 4 categories
- **Model Validation:** 11 tests
- **MongoDB Service:** 8 tests (integration tests with Azure Cosmos DB)
- **Document Intelligence:** 8 tests (6 unit, 2 integration - skipped)
- **Function Tests:** 7 tests
- **Utility:** 2 tests

**Test Results (Latest Run):**
- ✅ 34 passing
- ⏭️ 2 skipped (require live Azure Document Intelligence service)
- ❌ 0 failing

## Prerequisites

### Required Configuration

Tests require `src/local.settings.json` with Azure credentials:

```json
{
  "Values": {
    "MongoDBConnectionString": "mongodb+srv://username:password@your-cluster.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256",
    "MongoDBDatabase": "authpilot",
    "MongoDBCollection": "authorizations",
    "BlobStorageConnection": "DefaultEndpointsProtocol=https;AccountName=...",
    "DocumentIntelligenceEndpoint": "https://your-docint.cognitiveservices.azure.com/",
    "DocumentIntelligenceApiKey": "your-api-key",
    "DocumentIntelligenceModelId": "prebuilt-layout"
  }
}
```

### Azure Resources

Tests connect to real Azure services:
- **Azure Cosmos DB for MongoDB** - Used for all MongoDbService tests
- **Azure Blob Storage** - Referenced in function tests (operations not executed)
- **Azure Document Intelligence** - 2 tests require live service (currently skipped)

## Running Tests

### Run All Tests

```bash
cd tests
dotnet test
```

### Run with Detailed Output

```bash
dotnet test --logger "console;verbosity=normal"
```

### Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~MongoDbServiceTests"
dotnet test --filter "FullyQualifiedName~FaxProcessorFunctionTests"
dotnet test --filter "FullyQualifiedName~ModelValidationTests"
```

### Run Single Test Method

```bash
dotnet test --filter "FullyQualifiedName~CreateAuthorizationDocumentAsync_WithValidData_CreatesDocument"
```

### Generate Code Coverage Report

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Categories

### 1. Model Validation Tests (`Models/ModelValidationTests.cs`)

**Purpose:** Validate BSON serialization/deserialization for MongoDB documents

**Tests:**
- `AuthorizationDocument` serialization and deserialization
- `ExtractedAuthorizationData` field handling
- Nullable field handling
- List field handling (CPT codes, ICD-10 codes)
- Round-trip data integrity

**Test Type:** Pure unit tests, no external dependencies

### 2. MongoDB Service Tests (`Services/MongoDbServiceTests.cs`)

**Purpose:** Validate CRUD operations against real Azure Cosmos DB

**Tests:**
- Document creation with validation
- Document retrieval by ID
- Document updates with extracted data
- Error marking functionality
- Null/invalid parameter handling
- Connection failure handling

**Test Type:** Integration tests
- ✅ Connect to real Azure Cosmos DB (`mongodb+srv://...@mongodb-authpilot-dev...`)
- ✅ Use separate test database: `authpilot_test`
- ✅ Use separate test collection: `authorizations_test`
- ✅ Clean up test documents after each test

**Important Notes:**
- Tests DO NOT affect production data (uses test database)
- Each test cleans up created documents
- Connection string loaded from `src/local.settings.json`
- Tests require valid Azure Cosmos DB credentials

### 3. Document Intelligence Tests (`Services/DocumentIntelligenceServiceTests.cs`)

**Purpose:** Validate fax document analysis and data extraction

**Tests:**
- Field parsing and mapping (21+ fields)
- Date format handling
- Array field parsing (CPT codes, ICD-10 codes)
- Null/missing field handling
- Error handling and validation

**Test Type:** Mixed
- 6 unit tests with mocked Azure SDK
- 2 integration tests (skipped - require live Azure Document Intelligence)

**Skipped Tests:**
- `AnalyzeFaxDocumentAsync_WithValidPdf_ReturnsExtractedData`
- `AnalyzeFaxDocumentAsync_WhenServiceThrowsException_PropagatesError`

To enable these tests:
1. Ensure valid Document Intelligence credentials in local.settings.json
2. Remove `[Fact(Skip = "...")]` attribute
3. Provide a valid test PDF document

### 4. Function Tests (`Functions/FaxProcessorFunctionTests.cs`)

**Purpose:** Validate Azure Function blob trigger processing logic

**Tests:**
- Successful processing flow (already-organized blobs)
- MongoDB failure handling
- Document Intelligence failure handling
- Update failure handling
- Null/empty input handling
- Logging verification

**Test Type:** Unit tests with mocked dependencies
- Mocks: IMongoDbService, IDocumentIntelligenceService, ILogger
- Real: Configuration loading from local.settings.json

**Important Notes:**
- Tests use blob names with subfolders (e.g., `test-folder/test.pdf`)
- Blob organization logic (moving files) requires real blob storage (not tested)
- Focus is on processing logic after organization

## Test Data

### Sample Documents

Test fixtures in `Fixtures/TestData.cs` provide:
- `CreateSampleAuthorizationDocument()` - Complete authorization with all fields
- `CreateSampleExtractedData()` - Extracted data with 24 fields populated
- `CreateMinimalExtractedData()` - Minimal valid data for testing nullable fields
- `CreateSampleBlobStream()` - Empty stream for testing file processing

## Database Cleanup

MongoDB integration tests automatically clean up:
- Each test creates documents with unique IDs
- `CleanupDocument(id)` helper deletes test documents after assertions
- Test database (`authpilot_test`) is separate from production

**Manual Cleanup (if needed):**
```javascript
// Connect to test database
use authpilot_test
// Remove all test documents
db.authorizations_test.deleteMany({})
```

## Troubleshooting

### Common Issues

**1. Connection String Errors**
```
MongoDB.Driver.MongoConfigurationException: Host 'invalid:99999' is not valid
```
**Solution:** Verify `MongoDBConnectionString` in `src/local.settings.json`

**2. Test Database Not Found**
```
Tests pass but can't find documents
```
**Solution:** Ensure test database exists in Azure Cosmos DB. Tests will create collections automatically.

**3. Blob Storage Connection Failures**
```
Azure.RequestFailedException: The specified blob does not exist
```
**Solution:** This is expected for function tests that skip blob organization. Tests are designed to work with organized blob paths.

**4. Document Intelligence Tests Failing**
```
Tests marked as skipped
```
**Solution:** This is expected. To enable:
- Add valid Document Intelligence credentials
- Remove `Skip` attribute from test methods
- Provide test PDF documents

### Debug Test Failures

**View Detailed Test Output:**
```bash
dotnet test --logger "console;verbosity=detailed"
```

**Run Failed Tests Only:**
```bash
dotnet test --filter "TestCategory=Failed"
```

**Debug Single Test:**
1. Open test file in VS Code
2. Set breakpoint in test method
3. Right-click test → Debug Test

## CI/CD Integration

### GitHub Actions Example

```yaml
- name: Run Tests
  run: |
    cd tests
    dotnet test --logger "trx;LogFileName=test-results.trx"
  env:
    MongoDBConnectionString: ${{ secrets.MONGODB_CONNECTION }}
    BlobStorageConnection: ${{ secrets.BLOB_CONNECTION }}
```

### Skip Integration Tests in CI

Add to test classes:
```csharp
[Fact(Skip = "Integration test - requires Azure connection")]
public async Task TestMethod() { }
```

## Code Coverage

**Current Coverage Estimate:**
- Services: ~85% (full CRUD operations tested)
- Functions: ~70% (blob organization not tested)
- Models: ~95% (all serialization paths tested)

**Generate Coverage Report:**
```bash
dotnet test --collect:"XPlat Code Coverage"
# Report: tests/TestResults/{guid}/coverage.cobertura.xml
```

**View Coverage with VS Code Extension:**
1. Install "Coverage Gutters" extension
2. Run tests with coverage
3. View inline coverage indicators

## Contributing

When adding new tests:
1. Follow existing naming conventions: `MethodName_Scenario_ExpectedResult`
2. Use AAA pattern (Arrange, Act, Assert)
3. Add XML documentation comments explaining test purpose
4. Use FluentAssertions for readable assertions
5. Clean up test data in MongoDB tests
6. Mock external dependencies appropriately

## Test Execution Times

**Typical execution times:**
- Model tests: < 1 second
- MongoDB tests: 2-4 seconds (network latency)
- Document Intelligence tests: 1-2 seconds
- Function tests: 1-2 seconds
- **Total: ~5-6 seconds**

## Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Azure Cosmos DB Testing Best Practices](https://learn.microsoft.com/azure/cosmos-db/nosql/best-practices-testing)
