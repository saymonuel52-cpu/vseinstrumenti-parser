using Moq;
using Polly;

namespace VseinstrumentiParser.Tests.UnitTests
{
    public class RequestPolicyExecutorTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClientSettings _settings;
        private readonly RequestPolicyExecutor _executor;

        public RequestPolicyExecutorTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _settings = new HttpClientSettings
            {
                TimeoutSeconds = 5,
                MaxRetryAttempts = 2,
                InitialRetryDelayMs = 100,
                MaxRetryDelayMs = 1000,
                CircuitBreakerExceptionCount = 2,
                CircuitBreakerDurationMinutes = 1
            };
            
            _executor = new RequestPolicyExecutor(
                _httpClientFactoryMock.Object,
                _settings,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<RequestPolicyExecutor>());
        }

        [Fact]
        public async Task ExecuteGetAsync_SuccessfulResponse_ReturnsSuccessResult()
        {
            // Arrange
            var url = "https://example.com/api/data";
            var responseContent = "{\"status\":\"ok\"}";
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock
                .Setup(x => x.CreateClient("ParserClient"))
                .Returns(httpClient);

            _httpMessageHandlerMock
                .SetupHandler(HttpMethod.Get, url)
                .Returns(HttpStatusCode.OK)
                .ReturnsStringContent(responseContent);

            // Act
            var result = await _executor.ExecuteGetAsync(url);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(responseContent, result.Content);
            Assert.Equal(url, result.Url);
            Assert.Equal(0, result.RetryCount);
        }

        [Fact]
        public async Task ExecuteGetAsync_ServerError_RetryAttemptsMade()
        {
            // Arrange
            var url = "https://example.com/api/error";
            var retryCount = 0;
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock
                .Setup(x => x.CreateClient("ParserClient"))
                .Returns(httpClient);

            _httpMessageHandlerMock
                .SetupHandler(HttpMethod.Get, url)
                .Returns(HttpStatusCode.InternalServerError)
                .Times(2); // Возвращаем ошибку 2 раза

            // Act
            var result = await _executor.ExecuteGetAsync(url);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public async Task ExecuteGetAsync_NotFound_ReturnsFailedResult()
        {
            // Arrange
            var url = "https://example.com/api/not-found";
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock
                .Setup(x => x.CreateClient("ParserClient"))
                .Returns(httpClient);

            _httpMessageHandlerMock
                .SetupHandler(HttpMethod.Get, url)
                .Returns(HttpStatusCode.NotFound);

            // Act
            var result = await _executor.ExecuteGetAsync(url);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public async Task ExecuteGetAsync_SuccessfulAfterRetry_ReturnsSuccessResult()
        {
            // Arrange
            var url = "https://example.com/api/retry-success";
            var callCount = 0;
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock
                .Setup(x => x.CreateClient("ParserClient"))
                .Returns(httpClient);

            _httpMessageHandlerMock
                .SetupHandler(HttpMethod.Get, url)
                .Returns(() =>
                {
                    callCount++;
                    return callCount == 1 
                        ? HttpStatusCode.ServiceUnavailable 
                        : HttpStatusCode.OK;
                })
                .ReturnsStringContent("Success after retry");

            // Act
            var result = await _executor.ExecuteGetAsync(url);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Success after retry", result.Content);
        }

        [Fact]
        public async Task ExecuteGetAsync_WithCustomHeaders_HeadersIncluded()
        {
            // Arrange
            var url = "https://example.com/api/secure";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token123" },
                { "X-Custom-Header", "custom-value" }
            };
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock
                .Setup(x => x.CreateClient("ParserClient"))
                .Returns(httpClient);

            _httpMessageHandlerMock
                .SetupHandlerWithHeaders(HttpMethod.Get, url, headers)
                .Returns(HttpStatusCode.OK)
                .ReturnsStringContent("Secure content");

            // Act
            var result = await _executor.ExecuteGetAsync(url, headers);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Secure content", result.Content);
        }

        [Fact]
        public async Task ExecuteGetAsync_Timeout_ReturnsTimeoutResult()
        {
            // Arrange
            var url = "https://example.com/api/slow";
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                Timeout = TimeSpan.FromMilliseconds(100)
            };
            _httpClientFactoryMock
                .Setup(x => x.CreateClient("ParserClient"))
                .Returns(httpClient);

            _httpMessageHandlerMock
                .SetupHandler(HttpMethod.Get, url)
                .Returns(() =>
                {
                    Thread.Sleep(200); // Задержка больше таймаута
                    return HttpStatusCode.OK;
                });

            // Act
            var result = await _executor.ExecuteGetAsync(url);

            // Assert
            Assert.False(result.Success);
            Assert.IsType<TimeoutRejectedException>(result.Exception);
        }

        [Fact]
        public async Task ExecuteGetAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var url = "https://example.com/api/slow";
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock
                .Setup(x => x.CreateClient("ParserClient"))
                .Returns(httpClient);

            _httpMessageHandlerMock
                .SetupHandler(HttpMethod.Get, url)
                .Returns(HttpStatusCode.OK);

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
                _executor.ExecuteGetAsync(url, cts.Token));
        }

        [Fact]
        public async Task CheckUrlAsync_ValidUrl_ReturnsTrue()
        {
            // Arrange
            var url = "https://example.com/api/health";
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock
                .Setup(x => x.CreateClient("ParserClient"))
                .Returns(httpClient);

            _httpMessageHandlerMock
                .SetupHandler(HttpMethod.Get, url)
                .Returns(HttpStatusCode.OK);

            // Act
            var result = await _executor.CheckUrlAsync(url);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CheckUrlAsync_InvalidUrl_ReturnsFalse()
        {
            // Arrange
            var url = "https://example.com/api/404";
            
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock
                .Setup(x => x.CreateClient("ParserClient"))
                .Returns(httpClient);

            _httpMessageHandlerMock
                .SetupHandler(HttpMethod.Get, url)
                .Returns(HttpStatusCode.NotFound);

            // Act
            var result = await _executor.CheckUrlAsync(url);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Constructor_NullHttpClientFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RequestPolicyExecutor(
                null!,
                _settings,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<RequestPolicyExecutor>()));
        }

        [Fact]
        public void Constructor_NullSettings_ThrowsArgumentNullException()
        {
            // Arrange
            var factory = new Mock<IHttpClientFactory>().Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RequestPolicyExecutor(
                factory,
                null!,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<RequestPolicyExecutor>()));
        }
    }

    /// <summary>
    /// Расширения для моков HTTP-запросов
    /// </summary>
    public static class HttpMessageHandlerMockExtensions
    {
        public static MockSequence SetupHandler(
            this Mock<HttpMessageHandler> mock,
            HttpMethod method,
            string url,
            params HttpStatusCode[] statuses)
        {
            var sequence = new MockSequence();
            var uri = new Uri(url);
            
            foreach (var status in statuses)
            {
                mock.InSequence(sequence)
                    .Setup(x => x.SendAsync(
                        It.Is<HttpRequestMessage>(m => m.Method == method && m.RequestUri == uri),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new HttpResponseMessage(status));
            }
            
            return sequence;
        }

        public static Mock<HttpMessageHandler> SetupHandler(
            this Mock<HttpMessageHandler> mock,
            HttpMethod method,
            string url)
        {
            var uri = new Uri(url);
            return mock.Setup(x => x.SendAsync(
                It.Is<HttpRequestMessage>(m => m.Method == method && m.RequestUri == uri),
                It.IsAny<CancellationToken>()));
        }

        public static Mock<HttpMessageHandler> SetupHandlerWithHeaders(
            this Mock<HttpMessageHandler> mock,
            HttpMethod method,
            string url,
            Dictionary<string, string> expectedHeaders)
        {
            var uri = new Uri(url);
            return mock.Setup(x => x.SendAsync(
                It.Is<HttpRequestMessage>(m => 
                    m.Method == method && 
                    m.RequestUri == uri &&
                    expectedHeaders.All(h => m.Headers.Contains(h.Key) && m.Headers.GetValues(h.Key).FirstOrDefault() == h.Value)),
                It.IsAny<CancellationToken>()));
        }

        public static Mock<HttpMessageHandler> Returns(this Mock<HttpMessageHandler> mock, Func<HttpStatusCode> statusFunc)
        {
            mock.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new HttpResponseMessage(statusFunc()));
            return mock;
        }

        public static Mock<HttpMessageHandler> Returns(this Mock<HttpMessageHandler> mock, HttpStatusCode status)
        {
            mock.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(status));
            return mock;
        }

        public static Mock<HttpMessageHandler> ReturnsStringContent(
            this Mock<HttpMessageHandler> mock,
            string content)
        {
            mock.Setup(x => x.SendAsync(
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content)
                });
            return mock;
        }
    }
}
