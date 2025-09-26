start "Identity Server" /D IdentityServerStandalone dotnet run .
start "Identity WebApi" /D Viking.Identity.Server.WebApi dotnet run .
start "Identity Management" /D IdentityServer dotnet run . 
 