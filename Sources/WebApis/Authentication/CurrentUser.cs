using Application.Authentication;
namespace WebApis.Authentication;
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;
}
