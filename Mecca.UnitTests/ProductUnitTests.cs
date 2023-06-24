using Moq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mecca.Api.Controllers;
using Moq.Protected;

namespace Mecca.UnitTests
{

    [TestFixture]
    public class ProductsControllerTests
    {
        private Mock<IHttpClientFactory> _httpClientFactoryMock;
        private Mock<ILogger<ProductController>> _loggerMock;

        [SetUp]
        public void Setup()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<ProductController>>();
        }

        [Test]
        public async Task Products_WithValidBrand_ReturnsOkResult()
        {
            // Arrange
            var brand = "exampleBrand";

            // Create a mock HttpClientHandler to intercept and provide responses
            var httpClientHandlerMock = new Mock<HttpClientHandler>();
            httpClientHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("[]") // Mock empty response for testing
                });

            var httpClient = new HttpClient(httpClientHandlerMock.Object);

            // Create a mock IHttpClientFactory to return the mock HttpClient
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var controller = new ProductController(_loggerMock.Object, httpClientFactoryMock.Object);

            // Act
            var result = await controller.Products(brand);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
        }

        [Test]
        public async Task Products_WithEmptyBrand_ReturnsBadRequest()
        {
            // Arrange
            string brand = null; // or empty string
            var controller = new ProductController(_loggerMock.Object, _httpClientFactoryMock.Object);

            // Act
            var result = await controller.Products(brand);

            // Assert
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
        }
       

        // Add more unit tests for different scenarios as needed

        // Helper class to mock HttpClient requests
        public class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

            public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
            {
                _handlerFunc = handlerFunc;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return await _handlerFunc(request, cancellationToken);
            }
        }
    }
}