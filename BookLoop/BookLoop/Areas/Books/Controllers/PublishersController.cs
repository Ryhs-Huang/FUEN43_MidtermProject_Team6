using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Helpers;

namespace BookSystem.Controllers
{
	[Area("Books")]
	public class PublishersController : Controller
	{
		private readonly BookSystemContext _context; // ��Ʈw�s���Ϊ� DbContext

		// �غc�l�G�`�J DbContext
		public PublishersController(BookSystemContext context)
		{
			_context = context;
		}

		#region �C��(Index)

		// GET: /Books/Publishers
		// ��ܥX�����M��
		public async Task<IActionResult> Index()
		{
			var list = await _context.Publishers.ToListAsync();
			return View(list);
		}

		#endregion

		#region �s�W(Create)

		// GET: /Books/Publishers/Create
		// ��ܡu�s�W�X�����v����歶��
		public IActionResult Create()
		{
			return View();
		}

		// POST: /Books/Publishers/Create
		// ��ϥΪ̦b�����U�u�x�s�v��A�|�i��o��
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Publisher publisher)
		{
			publisher.Slug = BookLoop.Helpers.SlugHelper.Generate(publisher.PublisherName);

			if (!ModelState.IsValid) return View(publisher);

			if (_context.Publishers.Any(p => p.PublisherName == publisher.PublisherName))
			{
				ModelState.AddModelError("PublisherName", "���X�����w�s�b�I");
				return View(publisher);
			}

			publisher.CreatedAt = DateTime.Now;
			publisher.UpdatedAt = DateTime.Now;
			publisher.IsDeleted = false;

			_context.Add(publisher);
			await _context.SaveChangesAsync();

			TempData["Success"] = "�X�����s�W���\�I"; // �s�W�����T��

			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region �ק�(Edit)

		// GET: /Books/Publishers/Edit/5
		// �̷� PublisherID ���Ƨ�X�ӡA��ܦb�s����
		public async Task<IActionResult> Edit(int? id)
		{
			if (id == null) return NotFound();

			var publisher = await _context.Publishers.FindAsync(id);
			if (publisher == null) return NotFound();

			return View(publisher);
		}

		// POST: /Books/Publishers/Edit/5
		// ��s�X�������e
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, Publisher publisher)
		{
			if (id != publisher.PublisherID) return NotFound();
			publisher.Slug = BookLoop.Helpers.SlugHelper.Generate(publisher.PublisherName);
			if (!ModelState.IsValid) return View(publisher);

			if (_context.Publishers.Any(p => p.PublisherName == publisher.PublisherName && p.PublisherID != publisher.PublisherID))
			{
				ModelState.AddModelError("PublisherName", "���X�����w�s�b�I");
				return View(publisher);
			}
			publisher.UpdatedAt = DateTime.Now;
			_context.Update(publisher);
			await _context.SaveChangesAsync();

			TempData["Success"] = "�X�����ק令�\�I"; // �ק粒���T��

			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region �R��(Delete)

		// GET: /Books/Publishers/Delete/5
		// ��ܧR���T�{����
		public async Task<IActionResult> Delete(int? id)
		{
			if (id == null) return NotFound();

			var publisher = await _context.Publishers.FirstOrDefaultAsync(m => m.PublisherID == id);
			if (publisher == null) return NotFound();

			return View(publisher); // �Ǽҫ���R���T�{��
		}

		// POST: /Books/Publishers/Delete/5
		[HttpPost, ActionName("Delete")] // �P GET �@�� URL
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(int id)
		{
			var publisher = await _context.Publishers.FindAsync(id);
			if (publisher != null)
			{
				_context.Publishers.Remove(publisher);
				await _context.SaveChangesAsync();

				TempData["Success"] = "�X�����R�����\�I"; // �R�������T��
			}
			else
			{
				TempData["Error"] = "�R�����ѡA�X�������s�b�I"; // �R�����ѰT��
			}
			return RedirectToAction(nameof(Index));
		}

		#endregion

		#region �ԲӸ�T(Details)

		// GET: /Books/Publishers/Details/5
		// ��ܳ�@�X�����������T
		public async Task<IActionResult> Details(int? id)
		{
			if (id == null) return NotFound();

			var publisher = await _context.Publishers.FirstOrDefaultAsync(m => m.PublisherID == id);
			if (publisher == null) return NotFound();

			return View(publisher); // ��ҫ��ǵ� View
		}

		#endregion
	}
}
