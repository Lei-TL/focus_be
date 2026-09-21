namespace Application.Authentication;
public interface ICurrentUser
{
    Guid? UserId { get; }
}
