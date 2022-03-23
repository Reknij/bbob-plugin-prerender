# Install
Use command:
```
bbob add -a https://github.com/Reknij/bbob-plugin-prerender/releases/download/v1.1.0/bbob-plugin-prerender.rar
```
Please change 'v1.1.0' to you expect version for install in address.

# How to use
This plugin require theme support. Default theme already support it. It will working which you installed.

# Requirement
If want prerender work, you must enable the `shortAddress` in the config `BuildWebArticleJson` plugin.
```
// ./configs/BuildWebArticleJson.config.json
{
    "shortAddress": true, //change to true
    "shortAddressEndWithSlash": false //If dont want your blog have 301 status redirect. Turn it on.
}
```

# Theme support(Developmemt)
If want your theme support this plugin, please insert `prerender` object to `theme.json`. And then make sure your theme have `articleBaseUrlShort`, and it is param url not the query url.
`articleBaseUrlShort` is let prerender know article web page of your theme. Example your article page url is 'https://myDomain/article/myFirstArticle', so `articleBaseUrlShort` value is `/article/`.

Example:
```
// ./themes/yourTheme/theme.json
{
    "name": "themeName",
    "description": "some description",
    "author": "authorName",
    "articleBaseUrlShort": "/article/",

    "prerender": {
        "enable": true,
        "otherUrls": [
            "/",
            "/aboutPage"
        ]
    }
}
```
prerender.otherUrls is optional.

In your theme project. Write some code to tell plugin when to prerender. Example, write `Bbob.meta.extra.prerenderNow` to true which you ajax get article json data.
