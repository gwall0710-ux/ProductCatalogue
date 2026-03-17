using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using ProductCatalogue.API.Controllers;
using ProductCatalogue.API.Infrastructure;
using ProductCatalogue.Core.DTOs;
using ProductCatalogue.Core.Entities;
using ProductCatalogue.Core.Interfaces;
using Xunit;

namespace ProductCatalogue.Tests.Controllers;

public sealed class ProductsControllerTests
{
    private readonly IProductRepository _repo      = Substitute.For<IProductRepository>();
    private readonly IInventoryService  _inventory = Substitute.For<IInventoryService>();
    private readonly ILogger<ProductsController> _logger =
        Substitute.For<ILogger<ProductsController>>();

    private ProductsController CreateController() =>
        new(_repo, _inventory,
            new CreateProductRequestValidator(),
            new UpdateProductRequestValidator(),
            _logger);

    // ── Test 1 ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetById_WhenProductAndInventoryAvailable_ReturnsEnrichedOkResponse()
    {
        var product   = Product.Create("Test Headphones", "Great sound quality.");
        var inventory = InventoryResult.Success(49.99m, 120, "GBP");

        _repo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(product);
        _inventory.GetInventoryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(inventory);

        var result = await CreateController().GetById(1, CancellationToken.None);

        var ok  = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<EnrichedProductResponse>().Subject;

        dto.InventoryStatus.Should().Be("Live");
        dto.Price.Should().Be(49.99m);
        dto.StockLevel.Should().Be(120);
        dto.StockStatus.Should().Be("InStock");
        dto.InventoryWarning.Should().BeNull();
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetById_WhenInventoryUnavailable_Returns200WithWarningNotError()
    {
        var product   = Product.Create("Widget", "A product.");
        var inventory = InventoryResult.Unavailable("Connection timeout after 3 retries.");

        _repo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(product);
        _inventory.GetInventoryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(inventory);

        var result = await CreateController().GetById(1, CancellationToken.None);

        var ok  = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<EnrichedProductResponse>().Subject;

        dto.InventoryStatus.Should().Be("Unavailable");
        dto.Price.Should().BeNull();
        dto.StockLevel.Should().BeNull();
        dto.InventoryWarning.Should().NotBeNullOrWhiteSpace();
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetById_WhenProductDoesNotExist_Returns404AndSkipsInventory()
    {
        _repo.GetByIdAsync(99, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await CreateController().GetById(99, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();

        await _inventory.DidNotReceive()
            .GetInventoryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // ── Test 4 ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task Create_WhenRequestIsInvalid_Returns422AndDoesNotPersist()
    {
        var request = new CreateProductRequest(string.Empty, string.Empty);

        var result = await CreateController().Create(request, CancellationToken.None);

        result.Should().BeOfType<UnprocessableEntityObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        await _repo.DidNotReceive()
            .AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    // ── Test 5 ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(0,   "OutOfStock")]
    [InlineData(5,   "LowStock")]
    [InlineData(11,  "InStock")]
    [InlineData(500, "InStock")]
    public async Task GetById_StockStatusIsCorrectlyDerived(int stockLevel, string expectedStatus)
    {
        var product   = Product.Create("Widget", "A product.");
        var inventory = InventoryResult.Success(9.99m, stockLevel, "GBP");

        _repo.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(product);
        _inventory.GetInventoryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(inventory);

        var result = await CreateController().GetById(1, CancellationToken.None);

        var ok  = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<EnrichedProductResponse>().Subject;

        dto.StockStatus.Should().Be(expectedStatus);
        dto.InventoryStatus.Should().Be("Live");
    }

    // ── Test 6 ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task Update_WhenProductExists_ReturnsOkWithUpdatedData()
    {
        var existing = Product.Create("Old Name", "Old description.");
        var request  = new UpdateProductRequest("New Name", "New description.");

        _repo.UpdateAsync(1, request.Name, request.Description, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await CreateController().Update(1, request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        await _repo.Received(1)
            .UpdateAsync(1, request.Name, request.Description, Arg.Any<CancellationToken>());
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task Update_WhenProductDoesNotExist_Returns404()
    {
        var request = new UpdateProductRequest("Name", "Description.");

        _repo.UpdateAsync(99, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await CreateController().Update(99, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── Test 8 ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task Update_WhenRequestIsInvalid_Returns422AndDoesNotPersist()
    {
        var request = new UpdateProductRequest(string.Empty, string.Empty);

        var result = await CreateController().Update(1, request, CancellationToken.None);

        result.Should().BeOfType<UnprocessableEntityObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        await _repo.DidNotReceive()
            .UpdateAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Test 9 ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task Delete_WhenProductExists_Returns204NoContent()
    {
        _repo.DeleteAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateController().Delete(1, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        await _repo.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
    }

    // ── Test 10 ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task Delete_WhenProductDoesNotExist_Returns404()
    {
        _repo.DeleteAsync(99, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateController().Delete(99, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
