using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 128 * 1024 * 1024);
var settings = LicenseSettings.FromEnvironment();
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<LicenseStore>();
builder.Services.AddCors(options => options.AddPolicy("endless-site", policy =>
    policy.WithOrigins(settings.SiteOrigin).WithMethods("GET", "POST").WithHeaders("Authorization", "Content-Type")));
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("code-redemption", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("website-account", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();
app.UseCors("endless-site");
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Endless Tweaks License API" }));
app.MapGet("/api/license/config", (LicenseSettings config) => Results.Ok(new { codeRedemptionAvailable = config.IsReady }));

app.MapGet("/api/admin/status", (HttpContext context, LicenseSettings config) =>
{
    if (!AdminAuthorized(context, config.AdminSecret)) return Results.Unauthorized();
    if (!config.IsReady) return Results.Json(new { error = "The license service is not ready to issue codes." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    return Results.Ok(new { authorized = true });
});

app.MapPost("/api/license/redeem", async Task<IResult> (RedeemCodeRequest request, LicenseSettings config, LicenseStore store, TokenService tokens) =>
{
    if (!config.IsReady) return Results.Json(new { error = "The license service is not configured yet." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    var username = NormalizeUsername(request.Username);
    if (!ValidDeviceId(request.DeviceId) || string.IsNullOrWhiteSpace(request.Code) || username is null)
        return Results.BadRequest(new { error = "Enter your username and a valid one-time Pro code." });

    if (!await store.RedeemAsync(request.Code, request.DeviceId!, username))
        return Results.Json(new { error = "That username and code do not match, or the code has already been used on another device." }, statusCode: StatusCodes.Status401Unauthorized);

    return Results.Ok(tokens.CreateDeviceSession(request.DeviceId!));
}).RequireRateLimiting("code-redemption");

app.MapPost("/api/account/register", async Task<IResult> (WebsiteAccountRequest request, LicenseSettings config, LicenseStore store, TokenService tokens) =>
{
    if (!config.IsReady) return Results.Json(new { error = "The secure download service is not configured yet." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    var username = NormalizeUsername(request.Username);
    if (username is null || request.Password is not { Length: >= 8 and <= 256 } || string.IsNullOrWhiteSpace(request.Code))
        return Results.BadRequest(new { error = "Enter a username, a password with at least 8 characters, and your Lifetime Pro code." });

    var normalizedCode = request.Code.Trim();
    if (!await ProCodeMatchesAsync(normalizedCode, username, store))
        return Results.Json(new { error = "That username and Lifetime Pro code could not be verified." }, statusCode: StatusCodes.Status401Unauthorized);
    if (!await store.CreateWebsiteAccountAsync(username, request.Password, HashWebsiteCode(normalizedCode)))
        return Results.Conflict(new { error = "A website account already exists for that username. Choose Sign in instead." });

    var expiry = DateTimeOffset.UtcNow.AddMinutes(15);
    return Results.Ok(new { accessToken = tokens.CreateDownloadSession(username, expiry), expiresUtc = expiry });
}).RequireRateLimiting("website-account");

app.MapPost("/api/account/login", async Task<IResult> (WebsiteAccountRequest request, LicenseSettings config, LicenseStore store, TokenService tokens) =>
{
    if (!config.IsReady) return Results.Json(new { error = "The secure download service is not configured yet." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    var username = NormalizeUsername(request.Username);
    if (username is null || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Code))
        return Results.BadRequest(new { error = "Enter your username, password, and Lifetime Pro code." });

    var normalizedCode = request.Code.Trim();
    if (!await store.VerifyWebsiteAccountAsync(username, request.Password, HashWebsiteCode(normalizedCode)) ||
        !await ProCodeMatchesAsync(normalizedCode, username, store))
        return Results.Json(new { error = "Sign in details or Pro access could not be verified." }, statusCode: StatusCodes.Status401Unauthorized);

    var expiry = DateTimeOffset.UtcNow.AddMinutes(15);
    return Results.Ok(new { accessToken = tokens.CreateDownloadSession(username, expiry), expiresUtc = expiry });
}).RequireRateLimiting("website-account");

app.MapGet("/api/download/endless-zero", (HttpContext context, TokenService tokens, LicenseSettings config) =>
{
    var username = tokens.ReadDownloadUsername(context);
    if (username is null) return Results.Unauthorized();

    var path = Path.GetFullPath(config.EndlessZeroPackagePath);
    if (!File.Exists(path)) return Results.Json(new { error = "The Endless Zero download is not configured yet." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(path, "application/zip", "EndlessZero-Consumer-win-x64.zip", enableRangeProcessing: true);
});

app.MapPut("/api/admin/package", async (HttpContext context, LicenseSettings config) =>
{
    if (!AdminAuthorized(context, config.AdminSecret)) return Results.Unauthorized();
    if (!string.Equals(context.Request.ContentType?.Split(';')[0], "application/zip", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Send the ZIP with Content-Type: application/zip." });
    if (context.Request.ContentLength is > 128L * 1024 * 1024)
        return Results.BadRequest(new { error = "The package must be smaller than 128 MB." });

    var destination = Path.GetFullPath(config.EndlessZeroPackagePath);
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    var temporary = destination + ".uploading";
    try
    {
        await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            await context.Request.Body.CopyToAsync(output, context.RequestAborted);
        File.Move(temporary, destination, overwrite: true);
        return Results.Ok(new { uploaded = true, fileName = "EndlessZero-Consumer-win-x64.zip", size = new FileInfo(destination).Length });
    }
    catch
    {
        if (File.Exists(temporary)) File.Delete(temporary);
        throw;
    }
});

app.MapGet("/api/license/status", async Task<IResult> (HttpContext context, TokenService tokens, LicenseStore store) =>
{
    var deviceId = tokens.ReadDeviceId(context);
    if (deviceId is null) return Results.Unauthorized();
    var license = await store.GetActiveLicenseAsync(deviceId);
    if (license is null) return Results.Forbid();
    return Results.Ok(new { deviceId, username = license.Username, isPro = true, plan = "Lifetime Pro" });
});

app.MapPost("/api/admin/codes", async Task<IResult> (CreateCodesRequest request, HttpContext context, LicenseSettings config, LicenseStore store) =>
{
    if (!AdminAuthorized(context, config.AdminSecret)) return Results.Unauthorized();
    if (!config.IsReady) return Results.Json(new { error = "Set ENDLESS_TOKEN_SECRET before issuing codes." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    if (request.Count is < 1 or > 50) return Results.BadRequest(new { error = "Count must be between 1 and 50." });
    var username = NormalizeUsername(request.Username);
    if (username is null)
        return Results.BadRequest(new { error = "Enter a username between 3 and 32 characters using letters, numbers, periods, underscores, or hyphens." });
    var codes = await store.CreateCodesAsync(request.Count, username);
    return Results.Ok(new { codes = codes.Select(code => new { username, code }), count = codes.Count, note = "Each code is tied to this username and activates one device." });
});

app.MapPost("/api/admin/devices/{deviceId}/revoke", async Task<IResult> (string deviceId, HttpContext context, LicenseSettings config, LicenseStore store) =>
{
    if (!AdminAuthorized(context, config.AdminSecret)) return Results.Unauthorized();
    if (!ValidDeviceId(deviceId)) return Results.BadRequest(new { error = "Invalid device ID." });
    return await store.RevokeAsync(deviceId)
        ? Results.Ok(new { revoked = true, deviceId })
        : Results.NotFound(new { error = "No active Pro license exists for that device." });
});

app.Run();

static bool ValidDeviceId(string? value) => value is { Length: 32 } && value.All(Uri.IsHexDigit);

static string? NormalizeUsername(string? value)
{
    var username = value?.Trim();
    if (username is not { Length: >= 3 and <= 32 } || username.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-' or '.'))) return null;
    return username;
}

static async Task<bool> ProCodeMatchesAsync(string code, string username, LicenseStore store) =>
    code.StartsWith("BT1.", StringComparison.Ordinal)
        ? OwnerCodeVerifier.MatchesUsername(code, username)
        : await store.IsDownloadCodeValidAsync(code, username);

static string HashWebsiteCode(string input)
{
    var normalized = input.StartsWith("BT1.", StringComparison.Ordinal)
        ? input.Trim()
        : new string(input.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
}

static bool AdminAuthorized(HttpContext context, string configuredSecret)
{
    if (configuredSecret.Length < 32 || !context.Request.Headers.TryGetValue("X-Admin-Secret", out var value)) return false;
    var provided = Encoding.UTF8.GetBytes(value.ToString());
    var expected = Encoding.UTF8.GetBytes(configuredSecret);
    return provided.Length == expected.Length && CryptographicOperations.FixedTimeEquals(provided, expected);
}

internal sealed record LicenseSettings(string TokenSecret, string AdminSecret, string DataPath, string SiteOrigin, string EndlessZeroPackagePath)
{
    public bool IsReady => TokenSecret.Length >= 32;

    public static LicenseSettings FromEnvironment() => new(
        Environment.GetEnvironmentVariable("ENDLESS_TOKEN_SECRET") ?? "",
        Environment.GetEnvironmentVariable("ENDLESS_ADMIN_SECRET") ?? "",
        Environment.GetEnvironmentVariable("ENDLESS_LICENSE_PATH") ?? Path.Combine(AppContext.BaseDirectory, "data", "licenses.json"),
        (Environment.GetEnvironmentVariable("ENDLESS_SITE_ORIGIN") ?? "https://endlesstweaks.github.io").TrimEnd('/'),
        Environment.GetEnvironmentVariable("ENDLESS_ZERO_PACKAGE_PATH") ?? Path.Combine(AppContext.BaseDirectory, "downloads", "EndlessZero-Consumer-win-x64.zip"));
}

internal sealed class TokenService(LicenseSettings settings)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public object CreateDeviceSession(string deviceId)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { sub = deviceId }));
        var unsigned = $"{header}.{payload}";
        var signature = Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.TokenSecret), Encoding.ASCII.GetBytes(unsigned)));
        return new { accessToken = $"{unsigned}.{signature}", expiresUtc = (DateTimeOffset?)null, deviceId, isPro = true, plan = "Lifetime Pro" };
    }

    public string CreateDownloadSession(string username, DateTimeOffset expiresUtc)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { sub = username, scope = "endless-zero-download", exp = expiresUtc.ToUnixTimeSeconds() }));
        var unsigned = $"{header}.{payload}";
        var signature = Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.TokenSecret), Encoding.ASCII.GetBytes(unsigned)));
        return $"{unsigned}.{signature}";
    }

    public string? ReadDownloadUsername(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = authorization[7..].Split('.');
        if (parts.Length != 3) return null;
        try
        {
            var unsigned = $"{parts[0]}.{parts[1]}";
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.TokenSecret), Encoding.ASCII.GetBytes(unsigned));
            if (!CryptographicOperations.FixedTimeEquals(expected, FromBase64Url(parts[2]))) return null;
            using var payload = JsonDocument.Parse(FromBase64Url(parts[1]));
            var root = payload.RootElement;
            if (!root.TryGetProperty("scope", out var scope) || scope.GetString() != "endless-zero-download" ||
                !root.TryGetProperty("exp", out var expiry) || expiry.GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
                !root.TryGetProperty("sub", out var subject)) return null;
            var username = subject.GetString();
            return username is { Length: >= 3 and <= 32 } && username.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.')
                ? username
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException) { return null; }
    }

    public string? ReadDeviceId(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = authorization[7..].Split('.');
        if (parts.Length != 3) return null;
        try
        {
            var unsigned = $"{parts[0]}.{parts[1]}";
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.TokenSecret), Encoding.ASCII.GetBytes(unsigned));
            var actual = FromBase64Url(parts[2]);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual)) return null;
            using var payload = JsonDocument.Parse(FromBase64Url(parts[1]));
            var root = payload.RootElement;
            if (root.TryGetProperty("exp", out var expiry) && expiry.GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
            if (!root.TryGetProperty("sub", out var subject)) return null;
            var deviceId = subject.GetString();
            return LicenseValidation.ValidDeviceId(deviceId) ? deviceId : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException) { return null; }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
}

internal sealed class LicenseStore(LicenseSettings settings)
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int WebsitePasswordIterations = 310_000;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<List<string>> CreateCodesAsync(int count, string username)
    {
        await _gate.WaitAsync();
        try
        {
            var state = await ReadAsync();
            var codes = new List<string>(count);
            for (var index = 0; index < count; index++)
            {
                string code;
                string hash;
                do
                {
                    code = GenerateCode();
                    hash = HashCode(code);
                } while (state.Codes.Any(existing => existing.Hash == hash));
                state.Codes.Add(new RedeemCodeRecord { Hash = hash, CreatedUtc = DateTime.UtcNow, Username = username });
                codes.Add(FormatCode(code));
            }
            await WriteAsync(state);
            return codes;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> RedeemAsync(string input, string deviceId, string username)
    {
        var normalized = new string(input.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (normalized.Length != 12 || normalized.Any(ch => !Alphabet.Contains(ch))) return false;
        var hash = HashCode(normalized);
        await _gate.WaitAsync();
        try
        {
            var state = await ReadAsync();
            var code = state.Codes.FirstOrDefault(candidate => FixedTimeHashEquals(candidate.Hash, hash));
            if (code is null) return false;
            if (!code.Username.Equals(username, StringComparison.OrdinalIgnoreCase)) return false;
            if (code.UsedUtc is not null) return code.UsedByDeviceId == deviceId && state.Devices.Any(device => device.DeviceId == deviceId && device.IsActive && device.CodeHash == hash && device.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (state.Devices.Any(device => device.DeviceId == deviceId && device.IsActive)) return false;
            code.UsedUtc = DateTime.UtcNow;
            code.UsedByDeviceId = deviceId;
            state.Devices.Add(new DeviceLicenseRecord { DeviceId = deviceId, CodeHash = hash, Username = username, RedeemedUtc = DateTime.UtcNow, IsActive = true });
            await WriteAsync(state);
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> IsDownloadCodeValidAsync(string input, string username)
    {
        var normalized = new string(input.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        if (normalized.Length != 12 || normalized.Any(ch => !Alphabet.Contains(ch))) return false;
        var hash = HashCode(normalized);
        await _gate.WaitAsync();
        try
        {
            var state = await ReadAsync();
            var code = state.Codes.FirstOrDefault(candidate => FixedTimeHashEquals(candidate.Hash, hash));
            if (code is null || !code.Username.Equals(username, StringComparison.OrdinalIgnoreCase)) return false;
            if (code.UsedUtc is null) return true;
            return state.Devices.Any(device => device.CodeHash == hash && device.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && device.IsActive);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> CreateWebsiteAccountAsync(string username, string password, string proCodeHash)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var passwordHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, WebsitePasswordIterations, HashAlgorithmName.SHA256, 32);
        await _gate.WaitAsync();
        try
        {
            var state = await ReadAsync();
            if (state.WebsiteAccounts.Any(account => account.Username.Equals(username, StringComparison.OrdinalIgnoreCase))) return false;
            state.WebsiteAccounts.Add(new WebsiteAccountRecord
            {
                Username = username,
                Salt = Convert.ToBase64String(salt),
                PasswordHash = Convert.ToBase64String(passwordHash),
                PasswordIterations = WebsitePasswordIterations,
                ProCodeHash = proCodeHash,
                CreatedUtc = DateTime.UtcNow
            });
            await WriteAsync(state);
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> VerifyWebsiteAccountAsync(string username, string password, string proCodeHash)
    {
        await _gate.WaitAsync();
        try
        {
            var state = await ReadAsync();
            var account = state.WebsiteAccounts.FirstOrDefault(existing => existing.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (account is null || account.PasswordIterations is < 100_000 or > 1_000_000 || !FixedTimeHashEquals(account.ProCodeHash, proCodeHash)) return false;
            var salt = Convert.FromBase64String(account.Salt);
            var expected = Convert.FromBase64String(account.PasswordHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, account.PasswordIterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException) { return false; }
        finally { _gate.Release(); }
    }

    public async Task<DeviceLicenseRecord?> GetActiveLicenseAsync(string deviceId)
    {
        await _gate.WaitAsync();
        try { return (await ReadAsync()).Devices.FirstOrDefault(device => device.DeviceId == deviceId && device.IsActive); }
        finally { _gate.Release(); }
    }

    public async Task<bool> RevokeAsync(string deviceId)
    {
        await _gate.WaitAsync();
        try
        {
            var state = await ReadAsync();
            var license = state.Devices.FirstOrDefault(device => device.DeviceId == deviceId && device.IsActive);
            if (license is null) return false;
            license.IsActive = false;
            license.RevokedUtc = DateTime.UtcNow;
            await WriteAsync(state);
            return true;
        }
        finally { _gate.Release(); }
    }

    private async Task<LicenseState> ReadAsync()
    {
        var path = Path.GetFullPath(settings.DataPath);
        if (!File.Exists(path)) return new LicenseState();
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<LicenseState>(stream, JsonOptions) ?? new LicenseState();
    }

    private async Task WriteAsync(LicenseState state)
    {
        var path = Path.GetFullPath(settings.DataPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions);
        File.Move(temp, path, overwrite: true);
    }

    private static string GenerateCode()
    {
        Span<char> value = stackalloc char[12];
        for (var i = 0; i < value.Length; i++) value[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(value);
    }

    private static string FormatCode(string code) => $"{code[..4]}-{code[4..8]}-{code[8..]}";
    private static string HashCode(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static bool FixedTimeHashEquals(string first, string second)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(first), Convert.FromHexString(second)); }
        catch (FormatException) { return false; }
    }
}

internal sealed class LicenseState
{
    public List<RedeemCodeRecord> Codes { get; set; } = new();
    public List<DeviceLicenseRecord> Devices { get; set; } = new();
    public List<WebsiteAccountRecord> WebsiteAccounts { get; set; } = new();
}

internal sealed class WebsiteAccountRecord
{
    public string Username { get; set; } = "";
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int PasswordIterations { get; set; }
    public string ProCodeHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

internal sealed class RedeemCodeRecord
{
    public string Hash { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime? UsedUtc { get; set; }
    public string? UsedByDeviceId { get; set; }
}

internal sealed class DeviceLicenseRecord
{
    public string DeviceId { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime RedeemedUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public bool IsActive { get; set; }
}

internal sealed record RedeemCodeRequest(string? Username, string? Code, string? DeviceId);
internal sealed record CreateCodesRequest(int Count, string? Username);
internal sealed record WebsiteAccountRequest(string? Username, string? Password, string? Code);

internal static class OwnerCodeVerifier
{
    private const string ProductId = "bones-tweaks-pro";
    private const string OwnerPublicKey = "MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAt/3sNAF1o8eTT+6aVJum8jILQRVk7f9lG9269P/nQCRzNgsNQ4h3ptyN+VlQFBTWXKHVc6cOnWduyRtzxEQG+xq+Dn0zf2cgBu1mpBROq0bkxoDpUfPbFXcIAl4uln8wSniz/S2PMMgBQ0NFRCL5Pon4Nv2rgFqwofz/ufZJ04S0l4/71Y5k0iGgiiW5zvpDZQ35GCfgyyYxeI+9ZV/BEY36ZLofT1/xwnt7medj66gUmzGUo+sxgzdRTleUSt8cXM1fGb4Pb2R87wcqRze+LPCILQqmXhfrA5Y4x8YyPApW6+4oTy6h+WgpjsgxHpkH/TSqEwDNy2yKG1pn3ecrChpDMiyDeQat4LJJUTDEY41sE9UbvO9nn2DBgRl9TnIY3QxlkJbjhOEcDkQb6ScLAoraAhFkjrCFTg0bUjQKAxUwfpp9+9CvlUZryi8l8XGjfLuJeLTkWa86JYPVuxnXF18w5epQ8oznbfVJYS8qIG8c9rWxMZCSVtcweUm2TI7DAgMBAAE=";

    public static bool MatchesUsername(string token, string username)
    {
        if (token.Length > 8192) return false;
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[0] != "BT1") return false;
        try
        {
            var payload = FromBase64Url(parts[1]);
            var signature = FromBase64Url(parts[2]);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(OwnerPublicKey), out _);
            if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return false;
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            return root.GetProperty("product").GetString() == ProductId &&
                   root.GetProperty("user").GetString()?.Equals(username, StringComparison.OrdinalIgnoreCase) == true &&
                   root.GetProperty("serial").GetString() is { Length: 32 } serial && Guid.TryParseExact(serial, "N", out _) &&
                   root.GetProperty("expires").ValueKind == JsonValueKind.Null;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        { return false; }
    }

    private static byte[] FromBase64Url(string value)
    {
        if (value.Length == 0 || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))) throw new FormatException();
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '='));
    }
}

internal static class LicenseValidation
{
    public static bool ValidDeviceId(string? value) => value is { Length: 32 } && value.All(Uri.IsHexDigit);
}
