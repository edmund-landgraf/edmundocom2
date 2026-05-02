# edmundo.com

ASP.NET Core Razor Pages rebuild for edmundo.com.

## Contact email settings

The contact form sends messages to `edmund.landgraf@gmail.com` through SMTP. Do not commit SMTP credentials.

For local development, set user secrets:

```powershell
dotnet user-secrets set "ContactEmail:UserName" "your-smtp-username"
dotnet user-secrets set "ContactEmail:Password" "your-smtp-password-or-app-password"
```

For hosting, set environment variables:

```text
ContactEmail__UserName
ContactEmail__Password
```

The default SMTP host is `smtp.gmail.com` on port `587` with SSL enabled. Gmail usually requires an app password.
