# taiko_no_tatsujin_v (Taiko no Tatsujin V Tools)
These are tools that were developed for the Taiko no Tatsujin V PS Vita translation project.


## psvita-l7ctool
This tool is used by Bandai Namco for various games it seems. 
It works with the following games that I was able to find and test with:
- Taiko no Tatsujin V (PS Vita)
- Tales of Hearts R (iOS, although console version should work too)  
  
  
```
Extraction:
        psvita-l7ctool.exe x input.l7z

Creation:
        psvita-l7ctool.exe c input_foldername output.l7z

Decompress individual file:
        psvita-l7ctool.exe d input.bin output.bin

Compress individual file:
        psvita-l7ctool.exe e input.bin output.bin
```


## psvita-txptool
A tool to convert TXP -> PNG and PNG -> TXP.
NOTE: PNG -> TXP will result in a TXP file that is of RGBA format, regardless of what image compression the file originally was.
I am hoping that the TXP image loading library is smart enough to handle converting the image format to other formats when needed.
So far this assumping hasn't resulted in any issues as far so far, so it might be safe.

Tested games:
- Taiko no Tatsujin V

```
usage:

Extract:
        psvita-txptool.exe x filename.txp foldername

Create:
        psvita-txptool.exe c foldername filename.txp

Extract all TXPs in a folder:
        psvita-txptool.exe a foldername
```
