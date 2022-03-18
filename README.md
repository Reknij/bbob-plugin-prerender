# Install
Use command:
```
bbob add -a https://github.com/Reknij/bbob-plugin-prerender/releases/download/v1.0.0/bbob-plugin-prerender.rar
```
Please change 'v1.0.0' to you expect version for install in address.

# How to use
This plugin require theme support. Default theme already support it. It will working which you installed.

# Theme support(Developmemt)
If want your theme support this plugin, please insert `prerender` object to `theme.json`.

Example:
```
{
    "name": "themeName",
    "description": "some description",
    "author": "authorName",

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