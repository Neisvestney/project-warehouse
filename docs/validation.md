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

### 3. FluentValidation (future)

When `FluentValidation.AspNetCore` is added:

```csharp
RuleFor(x => x.Username)
    .NotEmpty()
        .WithErrorCode(nameof(ErrorCode.Required))
        .WithMessage("Username is required.")
    .MaximumLength(50)
        .WithErrorCode(nameof(ErrorCode.TooLong))
        .WithMessage("Username must not exceed 50 characters.");
```

`WithErrorCode` sets a string value on the `ValidationFailure`. `ModelStateErrorMapper` runs `Enum.TryParse<ErrorCode>(errorMessage)` as the first check — since FV puts the error message in the ModelState error, this only works when the `WithMessage` text exactly matches an `ErrorCode` name. The correct pattern is to **use `WithErrorCode`** (not `WithMessage`) for the code, and `WithMessage` for the human text.

Internally, FV with `.WithErrorCode(nameof(ErrorCode.Required))` puts:
- `ModelError.ErrorMessage = "Username is required."` (the message)
- The error code is in `ValidationFailure.ErrorCode` — but FV's ModelState integration only copies the message, not the code, into `ModelError.ErrorMessage`.

**Workaround until FV integration is customized**: in FV validators, set `.WithMessage(nameof(ErrorCode.Required))` instead of `.WithErrorCode(...)`, so the `ErrorMessage` field contains the parseable enum name. The human-readable message can go into `WithState` or be derived client-side from the code.

**Better approach (when ready)**: register a custom `IFluentValidationAutoValidationResultFactory` that reads `ValidationFailure.ErrorCode` directly and maps to `AppProblemDetails` without going through ModelState.

## Field Name Normalization

ModelState keys use the binding model's property names (Pascal or camel case depending on source). `ModelStateErrorMapper.NormalizeField` ensures:

- `""` (empty string, top-level body error) → `"root"`
- `"Username"` → `"username"` (first char lowercased)
- `"$['User']['Name']"` → kept as-is (path notation from JSON)
