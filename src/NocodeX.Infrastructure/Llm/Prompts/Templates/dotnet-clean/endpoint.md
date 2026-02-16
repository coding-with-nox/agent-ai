## Task
Generate a {{stack.framework}} API endpoint.

{{task.description}}

## Requirements
- Create a Controller or Minimal API endpoint
- Follow {{stack.conventions}} architecture
- Include request/response DTOs if needed
- Add input validation using FluentValidation
- Use MediatR for CQRS command/query dispatch
- Include appropriate HTTP status codes
- Add XML doc comments on all public members

## Output
Generate the following files:
1. The endpoint/controller class
2. The MediatR command/query record
3. The MediatR handler
4. A FluentValidation validator (if applicable)

Wrap each file in `<code filepath="...">` tags.
