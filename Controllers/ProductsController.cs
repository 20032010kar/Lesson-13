using System.Runtime.CompilerServices;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using p48_lessons_asp.Data;
using p48_lessons_asp.Data.Entity;
using p48_lessons_asp.DTO.Product;
using p48_lessons_asp.Models;
using p48_lessons_asp.Models.ProductRequestModels;
using p48_lessons_asp.Services.Slug;

namespace p48_lessons_asp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly DataContext _dataContext;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductsController> _logger;
        private readonly ISlugService _slugService;

        public ProductsController(
            DataContext dataContext, 
            IMapper mapper, 
            ILogger<ProductsController> logger, 
            ISlugService slugService)
        {
            _dataContext = dataContext;
            _mapper = mapper;
            _logger = logger;
            _slugService = slugService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromForm] CreateProductModel model)
        {
            try
            {
                _logger.LogInformation("Спроба створення нового продукту з назвою: {ProductName}", model.Name);

                var category = await _dataContext.Categories.FindAsync(model.CategoryId);
                if (category == null)
                {
                    _logger.LogWarning("Категорію з ID {CategoryId} не знайдено в БД.", model.CategoryId);
                    return BadRequest(new ResponseModel<object>
                    {
                        StatusCode = 400,
                        Message = "Category not found"
                    });
                }

                var product = new Product
                {
                    Name = model.Name,
                    Description = model.Description,
                    Price = model.Price,
                    CategoryId = model.CategoryId,
                    Slug = _slugService.GetSlug(model.Name),
                    CreatedAt = DateTime.UtcNow
                };

                if (model.ProductImage != null && model.ProductImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}_{model.ProductImage.FileName}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ProductImage.CopyToAsync(fileStream);
                    }

                    var productImage = new ProductImage
                    {
                        Id = Guid.NewGuid(),
                        Url = $"/images/{uniqueFileName}",
                        ProductId = product.Id
                    };

                    _dataContext.ProductImages.Add(productImage);
                    _logger.LogInformation("Зображення для продукту успішно збережено: {ImageUrl}", productImage.Url);
                }

                _dataContext.Products.Add(product);
                await _dataContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Продукт успішно створено в БД! ID: {ProductId}, Назва: {ProductName}, Ціна: {Price}",
                    product.Id,
                    product.Name,
                    product.Price
                );

                var productDto = _mapper.Map<ProductDto>(product);
                return StatusCode(201, new ResponseModel<ProductDto>
                {
                    StatusCode = 201,
                    Message = "Product successfully created",
                    Data = productDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Сталася критична помилка під час створення продукту: {ErrorMessage}", ex.Message);
                return StatusCode(500, new ResponseModel<object>
                {
                    StatusCode = 500,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var products = await _dataContext.Products
                    .Include(p => p.Images)
                    .ProjectTo<ProductDto>(_mapper.ConfigurationProvider)
                    .ToListAsync();

                if (products == null || products.Count == 0)
                {
                    return StatusCode(404, new ResponseModel<object>
                    {
                        StatusCode = 404,
                        Message = "Products not found"
                    });
                }

                return StatusCode(200, new ResponseModel<List<ProductDto>>
                {
                    StatusCode = 200,
                    Message = "Ok",
                    Data = products
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при отриманні списку продуктів: {ErrorMessage}", ex.Message);
                return StatusCode(500, new ResponseModel<object>
                {
                    StatusCode = 500,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }
    }
} 