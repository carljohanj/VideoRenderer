using ManycoreProject.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ManycoreProject.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment hosting;
        private readonly Repository repo;

        public HomeController(Repository repo, IWebHostEnvironment hosting)
        {
            this.repo = repo;
            this.hosting = hosting;
        }

        public ViewResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult LoadVideoFile(IFormFile file, string renderingOption)
        {

            Console.WriteLine("\n" + "Rendering option: " + renderingOption);
            string fileUploads = Path.Combine(hosting.WebRootPath, "fileuploads");
            string extracts = Path.Combine(hosting.WebRootPath, "extracts");
            string renders = Path.Combine(hosting.WebRootPath, "renders");


            if (file != null)
            {
                repo.RenderVideo(file, fileUploads, extracts, renders, renderingOption);
            }
            else
            {
                return BadRequest("File can't be read");
            }

            return RedirectToAction("Index", "Home");
        }

   }
}
