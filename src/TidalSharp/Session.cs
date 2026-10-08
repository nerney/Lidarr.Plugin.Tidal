using Newtonsoft.Json.Linq;
using NzbDrone.Common.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using TidalSharp.Data;
using TidalSharp.Exceptions;

namespace TidalSharp;

internal class Session
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value; RegenerateCodes sets them, no idea why it's complaining
    internal Session(IHttpClient client, int itemLimit = 1000, bool alac = true)
#pragma warning restore CS8618
    {
        _httpClient = client;
        Alac = alac;

        ItemLimit = itemLimit > 10000 ? 10000 : itemLimit;

        RegenerateCodes();
    }

    public int ItemLimit { get; init; }
    public bool Alac { get; init; }

    private IHttpClient _httpClient;

    private string _clientUniqueKey;
    private string _codeVerifier;
    private string _codeChallenge;

    public string GetPkceLoginUrl()
    {
        var parameters = new Dictionary<string, string>
        {
            { "response_type", "code" },
            { "redirect_uri", Globals.PKCE_URI_REDIRECT },
            { "client_id", Globals.CLIENT_ID_PKCE },
            { "lang", "EN" },
            { "appMode", "android" },
            { "client_unique_key", _clientUniqueKey },
            { "code_challenge", _codeChallenge },
            { "code_challenge_method", "S256" },
            { "restrict_signup", "true" }
        };

        var queryString = HttpUtility.ParseQueryString(string.Empty);
        foreach (var param in parameters)
        {
            queryString[param.Key] = param.Value;
        }

        return $"{Globals.API_PKCE_AUTH}?{queryString}";
    }

    public async Task<bool> AttemptTokenRefresh(TidalUser user, CancellationToken token = default)
    {
        var request = _httpClient.BuildRequest(Globals.API_OAUTH2_TOKEN)
                        .Post()
                        .AddFormParameter("grant_type", "refresh_token")
                        .AddFormParameter("refresh_token", user.RefreshToken)
                        .AddFormParameter("client_id", user.IsPkce ? Globals.CLIENT_ID_PKCE : Globals.CLIENT_ID)
                        .AddFormParameter("client_secret", user.IsPkce ? Globals.CLIENT_SECRET_PKCE : Globals.CLIENT_SECRET);

        var response = await _httpClient.ProcessRequestAsync(request);

        if (response.HasHttpError)
            return false;

        try
        {
            var responseStr = response.Content;
            var tokenData = JObject.Parse(responseStr).ToObject<OAuthTokenData>()!;
            await user.RefreshOAuthTokenData(tokenData, token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<OAuthTokenData?> GetOAuthDataFromRedirect(string? uri, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(uri) || !uri.StartsWith("https://"))
            throw new InvalidURLException("The provided redirect URL looks wrong: " + uri);

        var queryParams = HttpUtility.ParseQueryString(new Uri(uri).Query);
        string? code = queryParams.Get("code");
        if (string.IsNullOrEmpty(code))
            throw new InvalidURLException("Authorization code not found in the redirect URL.");

        var request = _httpClient.BuildRequest(Globals.API_OAUTH2_TOKEN)
                        .Post()
                        .AddFormParameter("code", code)
                        .AddFormParameter("client_id", Globals.CLIENT_ID_PKCE)
                        .AddFormParameter("grant_type", "authorization_code")
                        .AddFormParameter("redirect_uri", Globals.PKCE_URI_REDIRECT)
                        .AddFormParameter("scope", "r_usr+w_usr+w_sub")
                        .AddFormParameter("code_verifier", _codeVerifier)
                        .AddFormParameter("client_unique_key", _clientUniqueKey);

        var response = await _httpClient.ProcessRequestAsync(request);

        if (response.HasHttpError)
            throw new APIException($"Login failed: {response.Content}");

        try
        {
            return JObject.Parse(response.Content).ToObject<OAuthTokenData>();
        }
        catch
        {
            throw new APIException("Invalid response for the authorization code.");
        }
    }

    public void RegenerateCodes()
    {
        _clientUniqueKey = $"{BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0):x}";
        _codeVerifier = ToBase64UrlEncoded(RandomNumberGenerator.GetBytes(32));

        using var sha256 = SHA256.Create();
        _codeChallenge = ToBase64UrlEncoded(sha256.ComputeHash(Encoding.UTF8.GetBytes(_codeVerifier)));
    }

    private static string ToBase64UrlEncoded(byte[] data) => Convert.ToBase64String(data).Replace("+", "-").Replace("/", "_").TrimEnd('=');
}
