﻿#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookStoreApp.API.Data;
using AutoMapper;
using BookStoreApp.API.Models.Books;
using AutoMapper.QueryableExtensions;
using BookStoreApp.API.Static;
using Microsoft.AspNetCore.Authorization;

namespace BookStoreApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]

    public class BooksController : ControllerBase
    {
        private readonly BookStoreDBContext _context;
        private readonly IMapper mapper;
        private readonly ILogger<BooksController> logger;
        private readonly IWebHostEnvironment webHostEnvironment;

        public BooksController(BookStoreDBContext context, IMapper mapper, ILogger<BooksController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            this.mapper = mapper;
            this.logger = logger;
            this.webHostEnvironment = webHostEnvironment;
        }

        // GET: api/Books
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookReadOnlyDto>>> GetBooks()
        {
            try
            {
                var bookDtos = await _context.Books
             .Include(q => q.Author)
             .ProjectTo<BookReadOnlyDto>(mapper.ConfigurationProvider)
             .ToListAsync();
                //var bookDtos = mapper.Map<IEnumerable<BookReadOnlyDto>>(books);
                return Ok(bookDtos);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error Performing Get in {nameof(GetBooks)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }

        // GET: api/Books/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BookDetailsDto>> GetBook(int id)
        {
            try
            {
                var book = await _context.Books
            .Include(q => q.Author)
            .ProjectTo<BookDetailsDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(q => q.Id == id);

                if (book == null)
                {
                    return NotFound();
                }

                return book;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error Performing Get in {nameof(GetBook)} - ID:{id}");
                return StatusCode(500, Messages.Error500Message);
            }
        }

        // PUT: api/Books/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> PutBook(int id, BookUpdateDto bookDto)
        {
            try
            {
                if (id != bookDto.Id)
                {
                    return BadRequest();
                }
                var book = await _context.Books.FindAsync(id);
                if (book == null)
                {
                    logger.LogWarning($"Update ID Invalid in: {nameof(PutBook)} - ID: {id}");

                    return NotFound();
                }
                
                if (string.IsNullOrEmpty(bookDto.ImageData) == false)
                {
                    book.Image = CreateFile(bookDto.ImageData, bookDto.OriginalImageName);

                    var picName = Path.GetFileName(book.Image);
                    var path = $"{webHostEnvironment.WebRootPath}\\bookcoverimages\\{picName}";
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                }

                mapper.Map(bookDto, book);

                _context.Entry(book).State = EntityState.Modified;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await BookExistsAsync(id))
                    {
                        logger.LogWarning($"Record Not Found: {nameof(PutBook)} - ID: {id}");
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error Performing Put in {nameof(PutBook)} - ID:{id}");
                return StatusCode(500, Messages.Error500Message);
            }
        }

        // POST: api/Books
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult<BookCreateDto>> PostBook(BookCreateDto bookDto)
        {
            try
            {
                var book = mapper.Map<Book>(bookDto);
                book.Image = CreateFile(bookDto.ImageData, bookDto.OriginalImageName);
                _context.Books.Add(book);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetBook), new { id = book.Id }, book);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error Performing Post in {nameof(GetBook)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }

        // DELETE: api/Books/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteBook(int id)
        {
            try
            {
                var book = await _context.Books.FindAsync(id);
                if (book == null)
                {
                    logger.LogWarning($"Record Not Found: {nameof(DeleteBook)} - ID: {id}");
                    return NotFound();
                }

                _context.Books.Remove(book);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error Performing Delte in {nameof(DeleteBook)}");
                return StatusCode(500, Messages.Error500Message);
            }
        }

        private string CreateFile(string imageBase64, string imageName)
        {
            var url = HttpContext.Request.Host.Value;
            var ext = Path.GetExtension(imageName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var path = $"{webHostEnvironment.WebRootPath}\\bookcoverimages\\{fileName}";

            byte[] image = Convert.FromBase64String(imageBase64);

            var fileStream = System.IO.File.Create(path);
            fileStream.Write(image, 0, image.Length);
            fileStream.Close();

            return $"https://{url}/bookcoverimages/{fileName}";
        }
        private async Task<bool> BookExistsAsync(int id)
        {
            return await _context.Books.AnyAsync(e => e.Id == id);
        }
    }
}
