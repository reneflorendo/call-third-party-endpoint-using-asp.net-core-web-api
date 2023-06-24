using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.Retry;

using Mecca.Api.Entities;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Mecca.Api.Controllers
{ 
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private const int MaxRetries = 3;
        private readonly ILogger<ProductController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public ProductController(ILogger<ProductController> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _retryPolicy = CreateRetryPolicy();
        }

        /// <summary>
        /// Retrieved from a third party api but will have outdated price information so you will be required to update all prices for each product in the list by calling the price api.
        /// </summary>
        /// <param name="brand"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Products([FromQuery] string brand)
        {
            try
            {
                if (string.IsNullOrEmpty(brand))
                    return BadRequest("Brand parameter is required.");

                var productsUrl = $"http://makeup-api.herokuapp.com/api/v1/products.json?brand={brand}";
                var httpClient = _httpClientFactory.CreateClient();

                var response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var httpResponse = await httpClient.GetAsync(productsUrl);
                    httpResponse.EnsureSuccessStatusCode();
                    return httpResponse;
                });

                var products = await response.Content.ReadFromJsonAsync<Product[]>();

                if (products==null)
                    return NotFound("No products found for the specified brand.");

                var priceTasks = products.Select(async product =>
                {
                    var priceUrl = $"https://vxc1lmoi82.execute-api.ap-southeast-2.amazonaws.com/Prod/api/v1/products/{product.Id}/price";
                    var priceResponse = await _retryPolicy.ExecuteAsync(async () =>
                    {
                        var priceHttpResponse = await httpClient.GetAsync(priceUrl);
                        priceHttpResponse.EnsureSuccessStatusCode();
                        return priceHttpResponse;
                    });
                    var priceData = await priceResponse.Content.ReadFromJsonAsync<ProductPrice>();

                    if (priceData != null)
                        product.Price = priceData.Price;
                });

                await Task.WhenAll(priceTasks);

                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching products.");
                return StatusCode(500, "An error occurred while fetching products.");
            }
        }

        /// <summary>
        /// Creates an instance of an asynchronous retry policy using the Polly library. The retry policy is used to handle failures and retry HTTP requests.
        /// </summary>
        /// <returns></returns>
        private AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy()
        {
            return Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(response => !response.IsSuccessStatusCode)
                .WaitAndRetryAsync(MaxRetries, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (result, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Request failed with '{result.Result?.StatusCode}'. Retry attempt {retryCount} in {timeSpan.TotalSeconds} seconds.");
                    });
        }
    }
   
}
