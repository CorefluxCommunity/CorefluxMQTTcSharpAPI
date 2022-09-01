namespace CorefluxCSharpAPI.API.Services.Interfaces
{
    public interface ICommunicationService
    {
        string PostInformation(string url);
        string GetInformation(string url);
    }
}
