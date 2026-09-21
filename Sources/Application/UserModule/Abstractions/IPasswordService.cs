using Domain.Entities.UserModule;
namespace Application.UserModule.Abstractions;

public enum PasswordCheck { Failed, Valid, RehashNeeded }
public interface IPasswordService
{
    string Hash(User user, string password);
    PasswordCheck Verify(User user, string password);
}
