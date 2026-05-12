# Running Unit Tests

## Prerequisites
- .NET 8 SDK installed
- Visual Studio or VS Code with C# extensions

## Build and Run Tests

### Using dotnet CLI:

```bash
cd g:\My Drive\src\c#\payments\payments.tests
dotnet test
```

### Run specific test class:

```bash
dotnet test --filter "ClassName=PaymentQueueTests"
dotnet test --filter "ClassName=PaymentProcessorTests"
dotnet test --filter "ClassName=PaymentsControllerTests"
```

### Run with verbose output:

```bash
dotnet test -v normal
```

### Run with code coverage:

```bash
dotnet add package coverlet.collector
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

## Test Organization

### PaymentQueueTests (`Services/PaymentQueueTests.cs`)
- **EnqueueAsync_AddsNewPaymentRequest**: Verifies new payments are queued
- **EnqueueAsync_IgnoresDuplicateExternalId**: Ensures idempotency
- **DequeueNextAsync_ReturnsOldestQueueItem**: FIFO queue behavior
- **MarkProcessedAsync_UpdatesProcessedAt**: Queue completion tracking

### PaymentProcessorTests (`Services/PaymentProcessorTests.cs`)
- **ProcessNextAsync_SkipsWhenQueueIsEmpty**: Empty queue handling
- **ProcessNextAsync_CompletesPaymentSuccessfully**: Happy path processing
- **ProcessNextAsync_SkipsAlreadyCompletedPayment**: Prevents duplicate processing
- **ProcessNextAsync_RetriesFailedPayment**: Failure retry logic
- **ProcessNextAsync_MovesToDeadLetterAfterMaxRetries**: Dead letter handling
- **ProcessNextAsync_HandlesReferenceMissingPaymentRequest**: Orphaned item cleanup

### PaymentsControllerTests (`Controllers/PaymentsControllerTests.cs`)
- **CreatePayment_AcceptsValidPaymentRequest**: Valid submission
- **CreatePayment_EnqueuesPaymentForProcessing**: Queue integration
- **CreatePayment_HashesCardNumber**: Payment security (no plain card storage)
- **CreatePayment_ReturnConflictForDuplicateExternalId**: Idempotency enforcement
- **CreatePayment_ReturnsValidationErrorForInvalidAmount**: Input validation
- **CreatePayment_ReturnsValidationErrorForInvalidCurrency**: Input validation
- **GetPaymentStatus_ReturnsPaymentStatus**: Status retrieval
- **GetPaymentStatus_ReturnsNotFoundForMissingPayment**: 404 handling
- **GetPaymentStatus_IncludesRetryCountInResponse**: Response completeness

## Key Testing Patterns

1. **In-Memory Database**: Tests use `EF Core InMemoryDatabase` to avoid PostgreSQL dependency
2. **Mocking**: `Moq` for logger mocks
3. **Idempotency Testing**: Duplicate ExternalId scenarios verified
4. **Retry Logic**: Tests validate max retry count (3) and dead-letter transitions
5. **Security**: Card numbers hashed before storage verification

## Test Statistics
- Total Tests: 18
- Coverage Areas: Queue, Processor, Controller, DTOs
- Test Framework: xUnit
- Mocking Library: Moq
