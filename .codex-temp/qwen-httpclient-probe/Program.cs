using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text;

await ProbeAsync("default", CreateDefaultClient());
Console.WriteLine(new string('-', 50));
await ProbeAsync("tls12-http11", CreateTls12Http11Client());
Console.WriteLine(new string('-', 50));
await ProbeJsonAsync("tls12-http11-postasjson", CreateTls12Http11Client());
Console.WriteLine(new string('-', 50));
await ProbeWebRequestAsync();
Console.WriteLine(new string('-', 50));
await ProbeAsync("winhttp", CreateWinHttpClient());

static HttpClient CreateDefaultClient()
{
    return new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
}

static HttpClient CreateTls12Http11Client()
{
    var handler = new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        SslOptions =
        {
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        }
    };

    return new HttpClient(handler)
    {
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        Timeout = TimeSpan.FromSeconds(15)
    };
}

static HttpClient CreateWinHttpClient()
{
    var handler = new WinHttpHandler()
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    };

    return new HttpClient(handler)
    {
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        Timeout = TimeSpan.FromSeconds(15)
    };
}

static async Task ProbeAsync(string label, HttpClient client)
{
    Console.WriteLine(label);
    var request = new HttpRequestMessage(HttpMethod.Post, "https://cn-hongkong.dashscope.aliyuncs.com/compatible-mode/v1/chat/completions");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-key");
    request.Content = new StringContent(
        """
        {"model":"qwen-plus","messages":[{"role":"user","content":"hi"}]}
        """,
        Encoding.UTF8,
        "application/json");

    try
    {
        using var response = await client.SendAsync(request);
        Console.WriteLine((int)response.StatusCode);
        Console.WriteLine(await response.Content.ReadAsStringAsync());
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.GetType().FullName);
        Console.WriteLine(ex.Message);

        var inner = ex.InnerException;
        while (inner is not null)
        {
            Console.WriteLine("INNER");
            Console.WriteLine(inner.GetType().FullName);
            Console.WriteLine(inner.Message);
            inner = inner.InnerException;
        }
    }
}

static async Task ProbeJsonAsync(string label, HttpClient client)
{
    Console.WriteLine(label);
    try
    {
        using var response = await client.PostAsJsonAsync(
            "https://cn-hongkong.dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
            new
            {
                model = "qwen-plus",
                messages = new[]
                {
                    new { role = "user", content = "hi" }
                }
            });
        Console.WriteLine((int)response.StatusCode);
        Console.WriteLine(await response.Content.ReadAsStringAsync());
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.GetType().FullName);
        Console.WriteLine(ex.Message);

        var inner = ex.InnerException;
        while (inner is not null)
        {
            Console.WriteLine("INNER");
            Console.WriteLine(inner.GetType().FullName);
            Console.WriteLine(inner.Message);
            inner = inner.InnerException;
        }
    }
}

static async Task ProbeWebRequestAsync()
{
    Console.WriteLine("httpwebrequest");
    var request = WebRequest.CreateHttp("https://cn-hongkong.dashscope.aliyuncs.com/compatible-mode/v1/chat/completions");
    request.Method = "POST";
    request.ContentType = "application/json";
    request.Headers["Authorization"] = "Bearer invalid-key";

    var bytes = Encoding.UTF8.GetBytes("""{"model":"qwen-plus","messages":[{"role":"user","content":"hi"}]}""");
    using (var reqStream = await request.GetRequestStreamAsync())
    {
        await reqStream.WriteAsync(bytes);
    }

    try
    {
        using var response = (HttpWebResponse)await request.GetResponseAsync();
        Console.WriteLine((int)response.StatusCode);
        using var reader = new StreamReader(response.GetResponseStream()!);
        Console.WriteLine(await reader.ReadToEndAsync());
    }
    catch (WebException ex)
    {
        Console.WriteLine(ex.GetType().FullName);
        Console.WriteLine(ex.Message);
        if (ex.Response is HttpWebResponse httpResponse)
        {
            Console.WriteLine((int)httpResponse.StatusCode);
            using var reader = new StreamReader(httpResponse.GetResponseStream()!);
            Console.WriteLine(await reader.ReadToEndAsync());
        }

        if (ex.InnerException is not null)
        {
            Console.WriteLine("INNER");
            Console.WriteLine(ex.InnerException.GetType().FullName);
            Console.WriteLine(ex.InnerException.Message);
        }
    }
}
