using BookLoop.Data;
using BookLoop.Helpers;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookSystem.Controllers
{
	[Area("Books")]
	public class CategoriesController : Controller
	{
		private readonly BookSystemContext _context; // ��Ʈw�s���Ϊ� DbContext

		// �غc�l�G�`�J DbContext
		public CategoriesController(BookSystemContext context)
		{
			_context = context;
		}

		#region �C��(Index)

		// GET: /Books/Categories
		// ��ܤ����M��
		public async Task<IActionResult> Index()
		{
			var list = await _context.Categories.ToListAsync();
			return View(list);
		}

		#endregion

		#region �s�W(Create)

		// GET: /Books/Categories/Create
		// ��ܡu�s�W�����v����歶��
		public IActionResult Create()
		{
			return View();
		}

		// POST: /Books/Categories/Create
		// �x�s�s�������
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Category category)
		{
			category.Slug = SlugHelper.Generate(category.CategoryName);
			ModelState.Remove("Slug"); // �O�I

			if (!ModelState.IsValid) return View(category);

			if (_context.Categories.Any(c => c.CategoryName == category.CategoryName))
			{
				ModelState.AddModelError("CategoryName", "�������w�s�b�I");
				return View(category);
			}

			category.CreatedAt = DateTime.Now;
			category.UpdatedAt = DateTime.Now;
			category.IsDeleted = false;

			_context.Add(category);
			await _context.SaveChangesAsync();

			TempData["Success"] = "�����s�W���\�I";//�s�W�����T��

			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region �ק�(Edit)

		// GET: /Books/Categories/Edit/5
		// �̷� CategoryID ���Ƨ�X�ӡA��ܦb�s����
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null) return NotFound();

			var category = await _context.Categories.FindAsync(id);
			if (category == null) return NotFound();

			return View(category);
		}

		// POST: /Books/Categories/Edit/5
		// ��s�������e
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Category category)
		{
			if (id != category.CategoryID) return NotFound();

			category.Slug = SlugHelper.Generate(category.CategoryName);
			ModelState.Remove("Slug");

			if (!ModelState.IsValid) return View(category);

			if (_context.Categories.Any(c => c.CategoryName == category.CategoryName && c.CategoryID != category.CategoryID))
			{
				ModelState.AddModelError("CategoryName", "�������w�s�b�I");
				return View(category);
			}

			category.UpdatedAt = DateTime.Now;
			_context.Update(category);
			await _context.SaveChangesAsync();

			TempData["Success"] = "�����ק令�\�I";//�ק粒���T��

			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region �R��(Delete)

		// GET: /Books/Categories/Delete/5
		// ��ܧR���T�{����
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();

			var category = await _context.Categories.FirstOrDefaultAsync(m => m.CategoryID == id);
			if (category == null) return NotFound();

			return View(category);
		}

		// POST: /Books/Categories/Delete/5
		// �T�{�R������
		[HttpPost, ActionName("Delete")] // �P GET �@�� URL
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var category = await _context.Categories.FindAsync(id);
			if(category != null)

			{
				_context.Categories.Remove(category);
				await _context.SaveChangesAsync();

				TempData["Success"] = "�����R�����\�I"; // �R�������T��
			}

		else
			{
				TempData["Error"] = "�R�����ѡA�������s�b�I"; // �R�����ѰT��
			}
			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region �ԲӸ�T(Details)

		// GET: /Books/Categories/Details/5
		// ��ܳ�@�����������T
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null) return NotFound();

			var category = await _context.Categories.FirstOrDefaultAsync(m => m.CategoryID == id);
			if (category == null) return NotFound();

			return View(category);
		}

		#endregion
	}
}
