using Moq;
using VseinstrumentiParser.Services;
using Polly;

namespace VseinstrumentiParser.Tests.UnitTests
{
    public class HtmlLoaderTests
    {
        private readonly Mock<RequestPolicyExecutor> _policyExecutorMock;
        private readonly HttpClientSettings _settings;
        private readonly HtmlLoader _htmlLoader;

        public HtmlLoaderTests()
        {
            _policyExecutorMock = new Mock<RequestPolicyExecutor>();
            _settings = new HttpClientSettings
            {
                TimeoutSeconds = 30,
                MaxRetryAttempts = 3,
                InitialRetryDelayMs = 1000
            };
            
            _htmlLoader = new HtmlLoader(
                _policyExecutorMock.Object,
                _settings,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<HtmlLoader>());
        }

        [Fact]
        public async Task LoadHtmlAsync_SuccessfulRequest_ReturnsHtml()
        {
            // Arrange
            var url = "https://example.com/product/123";
            var html = "<html><body><h1>Test</h1></body></html>";
            
            _policyExecutorMock
                .Setup(x => x.ExecuteGetAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RequestResult
                {
                    Success = true,
                    Content = html,
                    Duration = TimeSpan.FromMilliseconds(150)
                });

            // Act
            var result = await _htmlLoader.LoadHtmlAsync(url);

            // Assert
            Assert.Equal(html, result);
            _policyExecutorMock.Verify(x => x.ExecuteGetAsync(url, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LoadHtmlAsync_RequestFails_ThrowsHttpRequestException()
        {
            // Arrange
            var url = "https://example.com/product/404";
            var exception = new HttpRequestException("Connection timeout");
            
            _policyExecutorMock
                .Setup(x => x.ExecuteGetAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RequestResult
                {
                    Success = false,
                    Exception = exception,
                    Duration = TimeSpan.FromMilliseconds(30000)
                });

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _htmlLoader.LoadHtmlAsync(url));
        }

        [Fact]
        public async Task LoadHtmlAsync_CircuitBreakerOpen_ThrowsException()
        {
            // Arrange
            var url = "https://example.com/product/503";
            var exception = new CircuitBreakerOpenException("Circuit is open");
            
            _policyExecutorMock
                .Setup(x => x.ExecuteGetAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RequestResult
                {
                    Success = false,
                    Exception = exception,
                    Duration = TimeSpan.FromMilliseconds(10)
                });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _htmlLoader.LoadHtmlAsync(url));
            Assert.IsType<CircuitBreakerOpenException>(ex.InnerException);
        }

        [Fact]
        public async Task LoadHtmlAsync_WithCustomHeaders_HeadersApplied()
        {
            // Arrange
            var url = "https://example.com/secure";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token123" },
                { "X-Custom-Header", "value" }
            };
            var html = "<html><body>Secure content</body></html>";
            
            _policyExecutorMock
                .Setup(x => x.ExecuteGetAsync(url, headers, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RequestResult
                {
                    Success = true,
                    Content = html
                });

            // Act
            var result = await _htmlLoader.LoadHtmlAsync(url, headers);

            // Assert
            Assert.Equal(html, result);
            _policyExecutorMock.Verify(x => x.ExecuteGetAsync(url, headers, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LoadHtmlAsync_EmptyContent_ReturnsEmptyString()
        {
            // Arrange
            var url = "https://example.com/empty";
            
            _policyExecutorMock
                .Setup(x => x.ExecuteGetAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RequestResult
                {
                    Success = true,
                    Content = ""
                });

            // Act
            var result = await _htmlLoader.LoadHtmlAsync(url);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task LoadHtmlAsync_LargeContent_ReturnsAllContent()
        {
            // Arrange
            var url = "https://example.com/large";
            var largeHtml = new string('x', 1024 * 1024); // 1MB
            
            _policyExecutorMock
                .Setup(x => x.ExecuteGetAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RequestResult
                {
                    Success = true,
                    Content = largeHtml,
                    Duration = TimeSpan.FromMilliseconds(2000)
                });

            // Act
            var result = await _htmlLoader.LoadHtmlAsync(url);

            // Assert
            Assert.Equal(largeHtml.Length, result.Length);
        }

        [Fact]
        public async Task LoadHtmlAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var url = "https://example.com/slow";
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            _policyExecutorMock
                .Setup(x => x.ExecuteGetAsync(url, cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => 
                _htmlLoader.LoadHtmlAsync(url, cts.Token));
        }

        [Fact]
        public async Task LoadHtmlAsync_TimeoutException_ThrowsHttpRequestException()
        {
            // Arrange
            var url = "https://example.com/slow-server";
            var exception = new TimeoutRejectedException("Request timed out");
            
            _policyExecutorMock
                .Setup(x => x.ExecuteGetAsync(url, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RequestResult
                {
                    Success = false,
                    Exception = exception,
                    Duration = TimeSpan.FromSeconds(30)
                });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _htmlLoader.LoadHtmlAsync(url));
            Assert.IsType<TimeoutRejectedException>(ex.InnerException);
        }

        [Fact]
        public void Constructor_NullPolicyExecutor_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new HtmlLoader(
                null!,
                _settings,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<HtmlLoader>()));
        }

        [Fact]
        public void Constructor_NullSettings_ThrowsArgumentNullException()
        {
            // Arrange
            var policyExecutor = new Mock<RequestPolicyExecutor>().Object;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new HtmlLoader(
                policyExecutor,
                null!,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<HtmlLoader>()));
        }
    }
}
