namespace ManycoreProject.Models
{
    public interface IRepository
    {
        Task RenderVideo(IFormFile file, string path);
    }
}
