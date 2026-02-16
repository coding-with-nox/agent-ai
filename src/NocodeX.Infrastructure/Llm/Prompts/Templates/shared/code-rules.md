## Code Generation Rules

1. No placeholder code — every method must be fully implemented
2. Maximum 300 lines per file — split into multiple files if needed
3. XML documentation comments on all public types and members
4. Strictly typed — no `object`, no `dynamic`
5. Follow Clean Architecture dependency rules
6. Handle all error cases: timeout, null, invalid input
7. Use async/await for I/O operations
8. Use cancellation tokens in all async methods
9. Prefer records for DTOs and value objects
10. Use sealed classes where inheritance is not needed
