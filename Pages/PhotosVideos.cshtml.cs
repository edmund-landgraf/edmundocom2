using Edmundocom.Photos;
using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class PhotosVideosModel : PageModel
{
    private readonly SharedMediaRepository _repository;

    public PhotosVideosModel(SharedMediaRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<SharedMediaItem> Media { get; private set; } = [];

    public void OnGet()
    {
        Media = _repository.GetAll();
    }
}
