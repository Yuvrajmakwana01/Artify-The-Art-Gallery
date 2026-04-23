using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Repository.Interfaces;
using Repository.Models;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminCategoryApiController : ControllerBase
    {

        private readonly IAdmincategoiresInteface _categoryRepository;
    

        public AdminCategoryApiController(IAdmincategoiresInteface categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories([FromQuery] string? search, [FromQuery] string? status)
        {
            var data = await _categoryRepository.GetCategoriesAsync(search, status);
            return Ok(data);
        }

        [HttpGet("categories/stats")]
        public async Task<IActionResult> GetCategoryStats()
        {
            var data = await _categoryRepository.GetCategoryStatsAsync();
            return Ok(data);
        }

        [HttpGet("categories/{id:int}")]
        public async Task<IActionResult> GetCategoryById(int id)
        {
            var data = await _categoryRepository.GetCategoryByIdAsync(id);
            return data is null ? NotFound() : Ok(data);
        }

        [HttpPost("categories")]
        public async Task<IActionResult> AddCategory([FromBody] AdminCategoryUpsertRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CategoryName))
                return BadRequest("Category name is required.");

            try
            {
                var id = await _categoryRepository.AddCategoryAsync(request);
                var item = await _categoryRepository.GetCategoryByIdAsync(id);
                return CreatedAtAction(nameof(GetCategoryById), new { id }, item);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPut("categories/{id:int}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] AdminCategoryUpsertRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CategoryName))
                return BadRequest("Category name is required.");

            try
            {
                var ok = await _categoryRepository.UpdateCategoryAsync(id, request);
                return ok ? Ok(new { message = "Category updated successfully." }) : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpDelete("categories/{id:int}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var ok = await _categoryRepository.DeleteCategoryAsync(id);
                return ok ? Ok(new { message = "Category deleted successfully." }) : NotFound();
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                return Conflict("Cannot delete this category because artworks are linked to it.");
            }
        }
    }
}
