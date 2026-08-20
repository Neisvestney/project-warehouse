# Validation

## How Validation Works

All validation errors — regardless of source — are converted to `AppProblemDetails` by a single `InvalidModelStateResponseFactory` registered in `Program.cs`. The response format matches [errors.md](errors.md).

## Sources of Validation Errors

### 1. Data Annotations (`[Required]`, `[MaxLength]`, etc.)

Standard ASP.NET Core model validation. Annotations produce `ModelState` entries, which `ModelStateErrorMapper` converts to `ErrorCode` values via message text heuristics:

| Message contains | ErrorCode |
|-----------------|-----------|
| "required" | `Required` |
| "maximum length" / "too long" | `TooLong` |
| "minimum length" / "too short" | `TooShort` |
| "invalid" / "not valid" | `InvalidFormat` |
| anything else | `ValidationError` |

### 2. `[JsonRequired]` — Missing/null fields

Non-nullable reference type properties in DTOs should use `[JsonRequired]`:

```csharp
public class LoginRequest
{
    [JsonRequired] public string Username { get; init; } = null!;
    [JsonRequired] public string Password { get; init; } = null!;
}
```

When the field is absent or `null` in the JSON body, STJ throws a `JsonException` before binding. `ModelStateErrorMapper` handles it:

- `JsonException` with `Path != null` and message contains "null"/"required" → `ErrorCode.Required`
- `JsonException` with `Path != null` but type mismatch (e.g. string where Guid expected) → `ErrorCode.InvalidFormat`
- `JsonException` with `Path == null` (entire body is malformed JSON) → `ErrorCode.InvalidJson`

FluentValidation is deliberately not used. Its ASP.NET Core integration copies only `ValidationFailure.ErrorMessage` into `ModelState`, dropping `WithErrorCode`, so every validator would have to smuggle the `ErrorCode` name through `WithMessage` (or a custom result factory would have to replace the ModelState path entirely). Data annotations plus `[JsonRequired]` cover the current needs without that.

## Field Name Normalization

ModelState keys use the binding model's property names (Pascal or camel case depending on source). `ModelStateErrorMapper.NormalizeField` ensures:

- `""` (empty string, top-level body error) → `"root"`
- `"Username"` → `"username"` (first char lowercased)
- `"$['User']['Name']"` → kept as-is (path notation from JSON)
