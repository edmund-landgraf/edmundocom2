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

## Blog sub-app

The blog is implemented as the reusable `Edmundocom.Blog` Razor Class Library. The host site wires it with:

```csharp
builder.Services.AddEdmundocomBlog(builder.Configuration.GetSection("Blog"));
```

Posts are Markdown files with front matter in `Content/Blog`. The host project copies those files to build and publish output.

Required config shape:

```json
"Blog": {
  "Title": "Blog",
  "Description": "Notes, updates, and things worth remembering.",
  "ContentPath": "Content/Blog",
  "RecentPostCount": 20
}
```

Routes provided by the sub-app:

```text
/Blog
/Blog/{slug}
```
