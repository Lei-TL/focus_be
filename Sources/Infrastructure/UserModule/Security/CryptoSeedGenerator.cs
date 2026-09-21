using System.Security.Cryptography;
using Application.UserModule.Abstractions;
namespace Infrastructure.UserModule.Security;

public sealed class CryptoSeedGenerator : ISeedGenerator
{
    public long Create() => BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8));
}
