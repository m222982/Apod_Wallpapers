# NASA APOD As Wallpaper 
The software downloads NASA Astronomical Picture of the Day and set it as desktop wallpaper.

## System Requirement
- The software was build under .NET 6.0.

## How to use it
Just run Apod_Wallpapers.exe, it will get the picture. Then resize the picture to desktop resolution to set it as wallpaper. The picture will be saved in Picture folder. It will also add the explaination text to the picture.
To auto update the wallpaper everyday, create the shortcut of the executing file to Start folder, it will run every time windows start up to check for an update of the picture.
## Customization
To customize the software, create a file Apod_Wallpapers.json under the same folder.
- Source of APOD : The software use the [NCKU mirror site](http://sprite.phys.ncku.edu.tw/astrolab/mirrors/apod/archivepix.html) as default. The explaination text is translated. To use [NASA site](https://apod.nasa.gov/) as default, add a setting NASA as 1 in the json file.
- To disable add the explaination to the picture, add a setting Explaination as false in the json file.

```json
{
    "NASA":true,
    "Explaination":false 
}
```