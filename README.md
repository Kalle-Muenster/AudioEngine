AudioEngine
===========
Mix and Synthetize audio data - by parsing tone scripts,... independently from (supported) data types and channel constellations


### Supported data types:

- 8bit, 16bit, 24bit signed integer data
- 32bit, 64bit floating point data

*(I'm thinking of adding also half precission (16bit-float) support,.. 
but even these days today now.. there still is no hardware interface supporting this :(
...so using 24bit integers still seems best choice at least)

### Supported channel constellations:

- mono
- stereo
- quadro
- 5.1
- 7.1

### Supported file formats

- Windows (wav)
- Sun audio (au, snd)
- NetPbm (pam) ... (quite uncommon for transporting audiodata.. but really handy format for this)

## Not supported: 
- Anything which is using lossy data compression like: mp3, atrac, mpa, aac, ac3 ... these never will be supported by this library at all. this is not a compression library!
- Anything which is using lossles data compression,.. like flc, ape, other lossess formats, maybe will gain support later some days in the future.  

