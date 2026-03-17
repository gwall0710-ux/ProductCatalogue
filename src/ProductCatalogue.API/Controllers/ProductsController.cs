using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ProductCatalogue.Core.DTOs;
using ProductCatalogue.Core.Entities;
using ProductCatalogue.Core.Interfaces;

namespace ProductCatalogue.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("Products")]
public sealed class ProductsController(
    IProductRepository productRepository,
    IInventoryService inventoryService,
    IValidator<CreateProductRequest> createValidator,
    IValidator<UpdateProductRequest> updateValidator,
    ILogger<ProductsController> logger)
    : ControllerBase
{
    /// <summary>
    /// Returns all products. Local data only — no inventory enrichment.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var products = await productRepository.GetAllAsync(ct);
        return Ok(products.Select(ProductResponse.From));
    }

    /// <summary>
    /// Returns a single product enriched with real-time inventory data.
    /// If the inventory service is unavailable, the product is still returned
    /// with inventoryStatus: "Unavailable" and a warning message.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<EnrichedProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(id, ct);

        if (product is null)
        {
            logger.LogWarning("Product {Id} requested but not found", id);
            return NotFound(new ProblemDetails
            {
                Title  = "Product not found.",
                Detail = $"No product with ID {id} exists.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var inventory = await inventoryService.GetInventoryAsync(product.Id, ct);

        if (inventory.Status == InventoryStatus.Unavailable)
        {
            logger.LogWarning(
                "Returning partial response for product {Id}: {Reason}",
                id, inventory.Warning);
        }

        return Ok(EnrichedProductResponse.From(product, inventory));
    }

    /// <summary>
    /// Creates a new product with local data only.
    /// Does not contact the inventory provider.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return ValidationProblem(validation);

        var product = Product.Create(request.Name, request.Description);
        var created = await productRepository.AddAsync(product, ct);

        logger.LogInformation("Product created: ID {Id}, Name '{Name}'", created.Id, created.Name);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            ProductResponse.From(created));
    }

    /// <summary>
    /// Updates the name and description of an existing product.
    /// Does not contact the inventory provider.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken ct)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return ValidationProblem(validation);

        var updated = await productRepository.UpdateAsync(
            id, request.Name, request.Description, ct);

        if (updated is null)
        {
            logger.LogWarning("Update requested for product {Id} but it was not found", id);
            return NotFound(new ProblemDetails
            {
                Title  = "Product not found.",
                Detail = $"No product with ID {id} exists.",
                Status = StatusCodes.Status404NotFound
            });
        }

        logger.LogInformation("Product updated: ID {Id}, Name '{Name}'", updated.Id, updated.Name);

        return Ok(ProductResponse.From(updated));
    }

    /// <summary>
    /// Deletes a product by ID.
    /// Returns 204 No Content on success, or 404 if the product does not exist.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await productRepository.DeleteAsync(id, ct);

        if (!deleted)
        {
            logger.LogWarning("Delete requested for product {Id} but it was not found", id);
            return NotFound(new ProblemDetails
            {
                Title  = "Product not found.",
                Detail = $"No product with ID {id} exists.",
                Status = StatusCodes.Status404NotFound
            });
        }

        logger.LogInformation("Product {Id} deleted", id);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private IActionResult ValidationProblem(FluentValidation.Results.ValidationResult result)
    {
        return UnprocessableEntity(new ProblemDetails
        {
            Title  = "Validation failed.",
            Status = StatusCodes.Status422UnprocessableEntity,
            Extensions =
            {
                ["errors"] = result.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray())
            }
        });
    }
}
